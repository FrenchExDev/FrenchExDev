# Migration vers les images hiérarchiques BinaryWrapper

Le runner résout une recette complète par version, prépare les bases de dépendances
nécessaires, puis construit chaque image de logiciel depuis sa base. Les neuf clients
CLI utilisent `ImagePlanResolver`. Les recettes identiques partagent leur cache.
Les anciennes API `UseImageBuild` et `UseInlineContainer` restent compatibles :
la migration est nécessaire pour bénéficier du nouveau cache, pas pour compiler.

## Clients migrés

Inventaire du checkout : neuf wrappers CLI référencent BinaryWrapper et ont un
projet de collecte `.Design`. Les neuf clients utilisent désormais les images hiérarchiques.

| Client / entrée | État | Dépendances communes extraites | Installation conservée par version |
|---|---|---|---|
| [Dotnet](../../Dotnet/src/FrenchExDev.Net.Dotnet.Design/Program.cs) | Migré | Alpine 3.19, bash, curl, certificats, dépendances natives du SDK | SDK via `dotnet-install.sh`, version exacte |
| [Docker](../../Docker/src/FrenchExDev.Net.Docker.Design/Program.cs) | Migré | Alpine 3.19, curl, tar | Archive Docker x86_64, extraction du client |
| [DockerCompose](../../DockerCompose/src/FrenchExDev.Net.DockerCompose.Design/Program.cs) | Migré | Alpine 3.19, curl | Exécutable Compose x86_64 |
| [Podman](../../Podman/src/FrenchExDev.Net.Podman.Design/Program.cs) | Migré | Alpine 3.19, curl, tar | Archive Podman ; conserver le choix d’asset avant/après 4.4.0 |
| [PodmanCompose](../../PodmanCompose/src/FrenchExDev.Net.PodmanCompose.Design/Program.cs) | Migré | Alpine 3.19, python3, pip, yaml, dotenv | `pip install podman-compose==<version>` |
| [GitLab.Cli](../../GitLab.Cli/src/FrenchExDev.Net.GitLab.Cli.Design/Program.cs) | Migré | Alpine 3.19, curl, tar | Archive glab depuis GitLab |
| [Git](../../Git/src/FrenchExDev.Net.Git.Design/Program.cs) | Migré | Debian bookworm, compilateur, make, curl et bibliothèques de développement | Téléchargement, compilation et installation de Git |
| [Vagrant](../../Vagrant/src/FrenchExDev.Net.Vagrant.Design/Program.cs) | Migré | Debian bookworm, curl et index apt nécessaires | Installation du paquet Vagrant versionné |
| [Packer](../../Packer/src/FrenchExDev.Net.Packer.Design/Program.cs) | Migré | Alpine 3.19, curl, unzip | Archive Packer, puis `UseContainer()` |

Les projets runtime et leurs générateurs de commandes ne demandent aucune
modification pour cette migration. Conserver le collecteur de versions, les
parseurs, le format des JSON et les adaptations de `RunHelp`, notamment pour Git.
Packer conserve `helpFlag: "-h"`. Git et Vagrant conservent leur base Debian.

Les projets `Packer.Bundle.Design`, `DockerCompose.Bundle.Design`,
`Traefik.Bundle.Design`, `GitLab.DockerCompose.Design` et
`GitLab.Ci.Yaml.Design` utilisent les pipelines génériques de Wrapper.Versioning :
ils ne construisent pas ces images de CLI et ne sont pas concernés.

Le [générateur PowerShell](../scripts/New-BinaryWrapperSolution.ps1) garde une
collecte locale comme point de départ ; son README généré renvoie vers ce guide
pour passer à une collecte en conteneur.

## Exemple complet : migrer Docker.Design

Remplacer le `UseImageBuild(...)` existant par `UseVersionImage()`.
Déclarer une `DesignImagePlan` avec les dépendances communes et le script
d'installation versionné, puis la passer au runner via
`ImagePlanResolver = new SingleDesignImagePlanResolver(images)`.

Voici l’entrée Docker.Design après migration (exemple équivalent au fichier Docker
du checkout) :

```csharp
using FrenchExDev.Net.BinaryWrapper.Design;
using FrenchExDev.Net.BinaryWrapper.Design.Lib;
using FrenchExDev.Net.Wrapper.Versioning;
using Microsoft.Extensions.Logging;

var env = DotEnvLoader.Load();
env.TryGetValue("GITHUB_TOKEN", out var token);
Func<string, ILogger, IHelpParser> parser = (_, _) => HelpParsers.Create("cobra");

var images = new DesignImagePlan
{
    ImageName = "docker-cli",
    BaseImage = "alpine:3.19",
    Platform = "linux/amd64",
    BaseInstallScript = "apk add --no-cache curl tar",
    InstallScript = v =>
        $"curl -fsSL https://download.docker.com/linux/static/stable/x86_64/docker-{v}.tgz -o /tmp/docker.tgz && " +
        "tar xzf /tmp/docker.tgz --no-same-owner -C /tmp docker/docker && " +
        "mv /tmp/docker/docker /usr/local/bin/docker && " +
        "chmod +x /usr/local/bin/docker && " +
        "rm -rf /tmp/docker.tgz /tmp/docker",
};

var pipeline = new DesignPipeline()
    .UseVersionImage()
    .UseContainer()
    .UseScraper("docker", parser)
    .Build();

var reparse = new DesignPipeline()
    .UseCachedHelp()
    .UseScraper("docker", parser)
    .Build();

return await new DesignPipelineRunner
{
    ImagePlanResolver = new SingleDesignImagePlanResolver(images),
    VersionCollector = new GitHubTagsVersionCollector("docker", "cli", token: token),
    Pipeline = pipeline,
    ReparsePipeline = reparse,
    DefaultMinVersion = "23.0.0",
    OutputFilePattern = "docker-{version}.json",
    OutputDir = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "FrenchExDev.Net.Docker", "scrape")),
}.RunAsync(args);
```

Pour Packer, remplacer `UseInlineContainer(...)` par **les deux appels**
`UseVersionImage().UseContainer()`. Pour Git, conserver `Shell = "bash"`
dans le plan et le middleware spécifique qui suit `UseContainer()`.

Les scripts d’installation sont du shell maintenu par le développeur du wrapper :
conserver la validation et le filtrage des versions du collecteur avant de les
interpoler. Ne pas mettre de jetons ou de fichiers `.env` dans ces scripts.

Après migration, compléter l’aide CLI et le script PowerShell du client avec les
trois nouvelles options. Dotnet et son `scripts/Find-Missing.ps1` servent d’exemple.

## Exécuter les étapes séparément

Depuis le répertoire du wrapper migré :

```powershell
$design = './src/FrenchExDev.Net.Docker.Design/FrenchExDev.Net.Docker.Design.csproj'

# Base et dépendances uniquement : ni collecte de tags, ni conteneur de scraping.
dotnet run --project $design --framework net10.0 -- --build-base

# Examiner la sélection des versions.
dotnet run --project $design --framework net10.0 -- --list --min-version 28.0.0

# Installer les versions dans leurs images sans scraper.
dotnet run --project $design --framework net10.0 -- --build-images --min-version 28.0.0 --parallel 2

# Réutiliser ces images pour collecter.
dotnet run --project $design --framework net10.0 -- --missing --min-version 28.0.0 --parallel 2

# Supprimer explicitement le cache d’images de ce wrapper, enfants avant base.
dotnet run --project $design --framework net10.0 -- --clean-images
```

Le fonctionnement normal reste une seule commande : les versions sont résolues
après filtrage et les bases nécessaires sont préparées avant les workers.
Chacun construit ou réutilise son image, démarre son conteneur et
collecte les aides, puis supprime son conteneur et son image de version.
`--parallel` limite les versions, `--scrape-parallel` les appels d’aide simultanés
par version. Ajouter `--keep-images` pour conserver les images après collecte.

`--build-images` utilise la sélection habituelle du collecteur et les filtres.
Avec `--missing`, « manquant » désigne toujours un JSON absent, pas une image
absente. `--list` reste une prévisualisation sans construction.

`--build-base` prépare toutes les bases du catalogue, sans appeler `Resolve`.
`--clean-images` nettoie tout le namespace du wrapper, y compris ses anciennes recettes.
Ces deux commandes ne contactent pas le collecteur de versions.
Ces deux opérations ne se combinent pas avec `--list` ou `--missing`.
Les trois opérations d’images sont mutuellement exclusives et ne se combinent
pas avec `--reparse` ou la modification de la liste des versions indisponibles.
`--reparse`, `--list` et une sélection vide ne construisent aucune image.

## Résoudre une recette selon la version

`IDesignImagePlanResolver` déclare les recettes possibles et sélectionne l'objet
complet : image système, plateforme, shell, dépendances et script d'installation.

```csharp
public interface IDesignImagePlanResolver
{
    IReadOnlyList<DesignImagePlan> Plans { get; }
    DesignImagePlan Resolve(string version);
}
```

`Plans` est un catalogue non vide et stable pendant l'exécution. Tous ses objets
partagent un même `ImageName`. `Resolve` retourne une instance de ce catalogue :
les objets reconstruits à chaque appel ne sont pas acceptés. Le runner résout
chaque version distincte après les filtres, avant de préparer les bases et les
workers. Une recette non déclarée ou un catalogue incohérent échoue avant toute
commande au moteur.

- Git utilise `GitImagePlanResolver` : avant `2.55.0`, les dépendances C historiques ;
  à partir de `2.55.0`, les mêmes dépendances plus `cargo rustc`. Les deux bases
  restent sur `debian:bookworm`. Rust reste activé et les sorties de `make` sont
  conservées dans `build.log`.
- Podman utilise `PodmanImagePlanResolver` : le nom d'archive change au seuil
  `4.4.0`, avec une même base Alpine pour les deux recettes.
- Docker, DockerCompose, Dotnet, GitLab.Cli, Packer, PodmanCompose et Vagrant
  utilisent `SingleDesignImagePlanResolver` avec leurs recettes existantes.

`ImagePlan` reste compatible et est adapté en resolver unique. Renseigner
simultanément `ImagePlan` et `ImagePlanResolver` est une erreur. `--list`,
`--reparse` et une sélection vide n'accèdent pas au catalogue du resolver.

Git 2.55 remplace l'activation explicite `WITH_RUST` par l'exclusion `NO_RUST` :
[Makefile 2.54](https://github.com/git/git/blob/v2.54.0/Makefile),
[Makefile 2.55](https://github.com/git/git/blob/v2.55.0/Makefile).
Le [manifeste Rust 2.55](https://github.com/git/git/blob/v2.55.0/Cargo.toml)
annonce Rust 1.49 minimum ; les paquets Bookworm suffisent.

## Configuration, cache et nettoyage

| Propriété de DesignImagePlan | Rôle / défaut |
|---|---|
| `ImageName` | Nom du cache du wrapper, obligatoire, minuscules sans registre ni tag |
| `BaseImage` | Image système ou autre base préparée, obligatoire |
| `BaseInstallScript` | Préparation commune, exécutée dans la base ; défaut `:` |
| `InstallScript` | Fonction version → script d’installation du logiciel, obligatoire |
| `Platform` | Architecture explicite ; défaut `linux/amd64` |
| `Shell` | Shell qui exécute les scripts avec `-ec` ; défaut `sh` |

Les scripts sont encodés en forme JSON `RUN ["sh", "-ec", "..."]` dans des
Dockerfiles générés. Les contextes de construction contiennent uniquement les
recettes générées. Le `.env` du wrapper reste côté hôte.

Les artefacts locaux sont sous `<output>/.images/<ImageName>/<empreinte>/` :

- `Dockerfile` : recette de la base préparée ou de l’image du logiciel.
- `.dockerignore` : contexte limité au Dockerfile.
- `image.json` : tag, identité de l’image, parent, plateforme, version et empreinte.
- `build.log` : sorties de la dernière tentative de construction, conservées même en cas d'échec.

Avec le lanceur standard, stdout et stderr du build sont affichés en direct avec
le nom du wrapper et sa version. Un message toutes les 30 secondes indique que
le processus tourne encore, même si le téléchargement est silencieux. Un lanceur
`RunProcess` personnalisé garde son contrat de capture complète ; le suivi
périodique reste actif et sa sortie est enregistrée à la fin.

Les conteneurs de construction Buildah se voient avec `podman ps -a --external`.
Ils peuvent être absents de `podman ps` et de la vue habituelle des conteneurs.
Voir la [documentation Podman](https://docs.podman.io/en/stable/markdown/podman-ps.1.html#external).

Les tags sont `localhost/binarywrapper/<ImageName>:base-<sha256>` et
`localhost/binarywrapper/<ImageName>:version-<sha256>`. Le suffixe dépend de
l’identité réelle du parent, de la plateforme, du shell, du script, de l’étape
et de la version. Une modification de l’installation ne reconstruit pas la base ;
une modification de la base invalide seulement ses images descendantes.
Le choix du resolver n'ajoute rien à l'empreinte : les recettes uniques migrées
conservent leurs tags et deux recettes ayant les mêmes dépendances partagent la
même image de base. Chaque recette garde son propre parent préparé.

Une image disponible est réutilisée après inspection. Un verrou interprocessus
par tag couvre l’inspection, la construction et les écritures de recette, de log
et de métadonnées. Les demandes identiques réinspectent donc le moteur après
le constructeur précédent, même avec des sorties ou des checkouts différents.
Les recettes différentes restent parallèles.

Avant de libérer le verrou de construction, chaque collecte prend également
un verrou d’utilisation partagé. Il reste ouvert pendant le démarrage du
conteneur, le scraping et le retrait du conteneur. Plusieurs collectes peuvent
ainsi utiliser la même image simultanément. Le nettoyage automatique reprend
le verrou de construction et ne supprime l’image que si aucun autre utilisateur
ne garde son verrou partagé ; le dernier utilisateur effectue la suppression.

Les fichiers de verrou sont sous le dossier applicatif local de l’utilisateur,
dans `FrenchExDev/BinaryWrapper/image-locks/`. Ils restent présents, mais les
handles ouverts constituent les verrous : la fin du processus les libère aussi
en cas d’arrêt brutal. La coordination concerne les lanceurs de ce même
utilisateur et de cette même machine. Elle ne synchronise pas les sorties de
collecte, la commande explicite `--clean-images` ni des machines différentes
utilisant un même moteur distant.

Les images système déjà présentes sont utilisées localement. Pour actualiser une
base système, lancer par exemple `podman pull --platform linux/amd64 alpine:3.19`
avant le runner ; sa nouvelle identité invalidera le cache. Une URL distante
dont le contenu change sans changement de recette n’est pas détectée par ce
mécanisme. Pour actualiser explicitement ce contenu, modifier la recette
(par exemple avec un commentaire de révision) ou reconstruire avec `--no-cache`.

Après chaque collecte, `UseVersionImage` demande la suppression de l’image de
version, après le nettoyage du conteneur par `UseContainer`, y compris si le
scraping échoue. La base partagée, les JSON, les aides et les recettes locales
sont conservés. La suppression utilise `rmi` sans `--force` ; un refus du moteur
produit un avertissement sans remplacer le résultat de collecte.

`--keep-images` désactive cette suppression automatique. `--build-images`
conserve toujours les images préparées, puisqu’il ne lance pas de collecte.
`--reparse` utilise les fichiers d’aide sans accéder aux images. Les anciennes
API conservent leur fonctionnement antérieur, y compris leur nettoyage.

`--clean-images` ne vise que les tags de ce wrapper portant les labels du cache.
Il supprime toutes les images versionnées avant toutes les bases, sans `--force`, et échoue si
le moteur refuse une suppression. Il ne supprime ni les bases système, ni les
images des autres wrappers, ni les JSON/aides collectés. Les recettes locales
restent disponibles ; les couches intermédiaires du cache du moteur peuvent
subsister. Aucun `prune` global n’est exécuté.

Il n’y a pas de désinstallation du logiciel : chaque version dérive directement
de la base préparée et le conteneur est supprimé à la fin.

Références moteur :
[cache Docker](https://docs.docker.com/build/cache/optimize/),
[construction Podman](https://docs.podman.io/en/stable/markdown/podman-build.1.html),
[suppression Podman](https://docs.podman.io/en/latest/markdown/podman-rmi.1.html).

## Vérifier une migration

1. Compiler le `.Design` sur les frameworks du dépôt.
2. Exécuter `--build-base`, puis `--build-images` sur une sélection contrôlée.
3. Relancer : les logs doivent annoncer `Reusing`, sans nouvelle installation.
4. Collecter et comparer les commandes du JSON au résultat antérieur.
5. Vérifier le mode `--reparse`, indépendant du moteur de conteneurs.
6. Exécuter le nettoyage explicite et contrôler que les conteneurs ont disparu.

Les tests de `BinaryWrapper.Design.Lib.Tests` couvrent l’invalidation, les erreurs,
les modes séparés, l’ordre des constructions et le nettoyage limité au wrapper.
Un test Podman réel construit deux versions d’une petite CLI factice, réutilise
les images pour scraper puis nettoie ses propres ressources :

```powershell
$env:BINARYWRAPPER_CONTAINER_TESTS = '1'
dotnet test ./BinaryWrapper/test/FrenchExDev.Net.BinaryWrapper.Design.Lib.Tests/FrenchExDev.Net.BinaryWrapper.Design.Lib.Tests.csproj --framework net10.0 --filter FullyQualifiedName~DesignImageContainerTests
Remove-Item Env:BINARYWRAPPER_CONTAINER_TESTS
```


## Validation initiale de BinaryWrapper et de Dotnet

- Compilation de Dotnet.Design et de ses dépendances sur net10.0 et net11.0 : aucune erreur ni avertissement.
- Sous net10.0 : 468 tests BinaryWrapper.Design, 158 tests BinaryWrapper.Design.Lib et 30 tests Dotnet.Design réussis.
- Test Podman réel réussi : une base, deux installations versionnées, réutilisation sans nouvelle construction, collecte des JSON puis nettoyage des images du test.
- Vraie recette de dépendances Dotnet sur Alpine 3.19 : construction puis réutilisation vérifiées. Cette base reste en cache pour les prochaines collectes.
- Générateur PowerShell : 64 contrôles réussis ; liens locaux du présent guide vérifiés.

Docker est couvert par les tests des arguments de construction ; aucun essai avec
un moteur Docker réel n'a été exécuté. Le nouveau parcours a été testé avec une
CLI factice et la base native Dotnet ; l'installation complète d'un SDK puis la
collecte de toutes ses aides n'a pas été relancée dans cette modification.

## Validation de la migration des huit autres clients (10 septembre 2026)

- Compilation des huit projets `.Design`, de leurs dépendances et des projets de tests concernés sur `net10.0` et `net11.0` : aucune erreur ni avertissement.
- Tests existants des clients et de BinaryWrapper.Design/Design.Lib : **987 réussis par framework**. Le projet `GitLab.Cli.Tests` ne contient actuellement aucun test ; son client a été vérifié par la collecte réelle ci-dessous.
- Les deux tests Podman réels de `DesignImageContainerTests` ont également réussi sous `net10.0`, notamment la suppression automatique des images versionnées et la capture des sorties de construction.
- Lanceurs PowerShell : 88 contrôles de paramètres et de propagation des erreurs, puis huit appels réels à `dotnet` depuis la racine du dépôt. Les deux frameworks restent sélectionnables ; le lanceur Git conserve son défaut `net11.0` et transmet désormais son parallélisme et son filtre minimal.
- Aide CLI et découverte réelle des versions vérifiées pour les huit clients ; liens locaux du guide et des nouveaux renvois des README vérifiés.

Essais des vraies recettes avec le moteur **Podman** :

| Client | Version testée | Chemins de commandes, racine comprise | Comparaison avec le JSON antérieur |
|---|---|---:|---|
| Docker | 29.8.0 | 122 | Identiques |
| DockerCompose | 5.1.1 | 40 | Identiques |
| Podman | 4.2.1 | 166 | Identiques |
| Podman | 5.8.1 | 203 | Identiques |
| PodmanCompose | 1.5.0 | 23 | Identiques |
| GitLab.Cli | 1.90.0 | 235 | Identiques |
| Git | 2.54.0 | 215 | Identiques |
| Vagrant | 2.4.9 | 72 | Identiques |
| Packer | 1.15.1 | 15 | Identiques |

Chaque essai a préparé la base, construit l'image versionnée puis vérifié leur
réutilisation, collecté les aides et comparé l'ensemble des chemins de commandes
avec le JSON déjà présent dans le dépôt. Le `--reparse` a reproduit exactement le
JSON frais, avec un nom de moteur inexistant pour vérifier son indépendance.
Le nettoyage explicite a ensuite supprimé les seules images du client testé.
Les sorties de validation sont séparées du corpus de collecte, sous
`.fake/binarywrapper-image-migration/` à la racine du dépôt.

La recette Podman extrait maintenant l'archive dans un dossier distinct de son
fichier téléchargé : la recherche du binaire ne peut plus sélectionner l'archive.
Un appel à `podman --version` fait échouer la construction si l'exécutable installé
ne démarre pas. Les deux noms d'archive, avant et après 4.4.0, ont été exercés.
Les collecteurs, parseurs, formats de JSON et adaptations de `RunHelp` restent
inchangés dans les clients livrés.

Pour Git, Podman et PodmanCompose, les essais d'images ont utilisé une copie locale
du point d'entrée avec un `StaticVersionCollector`, après l'épuisement du quota
GitHub anonyme. Seul le collecteur a été remplacé dans ces lanceurs temporaires ;
les recettes et pipelines correspondent au code livré. La valeur `GITHUB_TOKEN`
chargée depuis `Net/FrenchExDev/.env` a été refusée avec HTTP 401 pendant la
validation ; ce fichier n'a pas été modifié. Packer a été relancé après une
interruption TLS transitoire du serveur HashiCorp.

Ces essais portent sur les versions indiquées et sur le moteur Podman. Le run
Dotnet déjà en cours et ses ressources ne font pas partie de cette validation.
