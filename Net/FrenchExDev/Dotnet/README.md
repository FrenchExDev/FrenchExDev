# FrenchExDev.Net.Dotnet

Wrapper C# de la CLI .NET. Le projet `.Design` collecte les aides des versions
stables du SDK dans des conteneurs **Alpine 3.19**, puis BinaryWrapper génère
les commandes, builders et clients depuis les JSON.

## Pipeline Design

`GitHubTagsVersionCollector("dotnet", "sdk")` récupère les tags du SDK.
`DotnetSdk.VersionFromTag` conserve les versions stables à feature band
(`8.0.100`, `10.0.401`, etc.) et exclut les tags de runtime tels que
`v11.0.0`, ainsi que les préversions.

Avant les versions, `DesignImagePlan` prépare une base depuis `alpine:3.19`
avec les dépendances natives du SDK. Cette base est partagée par toutes les versions.

Pour chaque version sélectionnée :

1. `UseVersionImage` construit ou réutilise une image dérivée de cette base,
   avec le SDK `linux-musl-x64` installé via le script officiel `dotnet-install.sh`.
2. `UseContainer` démarre un conteneur dans lequel le SDK est vérifié avec
   `dotnet --version`. La langue anglaise, la désactivation de la télémétrie et
   des notifications de workloads sont définies pour les commandes exécutées.
3. `UseScraper("dotnet", ...)` parcourt les aides avec `DotnetHelpParser`.
   L'aide brute est conservée dans `scrape/help/<version>/`.
4. Le JSON est écrit dans `src/FrenchExDev.Net.Dotnet/scrape/dotnet-<version>.json`.
   Le conteneur puis l’image SDK de cette version sont supprimés, y compris en cas
   d’échec du scraping. La base commune et les fichiers collectés sont conservés.
   Ajouter `--keep-images` pour conserver les images SDK après collecte.

Les Dockerfiles et les métadonnées des images sont générés sous `scrape/.images/dotnet-sdk/`.
Le [guide de migration BinaryWrapper](../BinaryWrapper/doc/UPGRADE-IMAGE-PIPELINES.md)
détaille les empreintes du cache, le nettoyage et la migration des autres clients.

Le pipeline `UseCachedHelp → UseScraper` permet de corriger le parser et de
régénérer les JSON sans télécharger le SDK, démarrer de conteneur ou interroger GitHub.

Sources de l'installation : [tags dotnet/sdk](https://github.com/dotnet/sdk/tags),
[script officiel et option linux-musl](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script).
Alpine 3.19 est la base demandée ; la validation complète de ce wrapper porte
sur le SDK 8.0.100, pas sur toutes les versions publiées.

## Lancer le .Design

Prérequis : SDK du checkout (`global.json`), dépendances NuGet du dépôt et Podman
ou Docker en fonctionnement. La CLI collectée est installée dans le conteneur :
elle ne dépend pas du SDK choisi pour compiler et exécuter le `.Design`.

Depuis `Dotnet/` :

```powershell
$design = './src/FrenchExDev.Net.Dotnet.Design/FrenchExDev.Net.Dotnet.Design.csproj'

dotnet run --project $design --framework net10.0 -- --help
dotnet run --project $design --framework net10.0 -- --list --min-version 8.0.100

# Préparer uniquement les dépendances, sans GitHub.
dotnet run --project $design --framework net10.0 -- --build-base

# Installer un SDK dans son image, sans collecter les aides.
dotnet run --project $design --framework net10.0 -- --build-images --version 8.0.100

# Collecter une seule version pour commencer.
dotnet run --project $design --framework net10.0 -- --version 8.0.100 --parallel 1

# Collecter les versions manquantes.
dotnet run --project $design --framework net10.0 -- --missing --min-version 8.0.100 --parallel 2 --runtime podman

# Retraiter les aides déjà présentes, sans GitHub ni conteneur.
dotnet run --project $design --framework net10.0 -- --reparse

# Conserver les images SDK après collecte pour les réutiliser plus tard.
dotnet run --project $design --framework net10.0 -- --missing --keep-images

# Supprimer explicitement les images du cache Dotnet, sans toucher aux JSON.
dotnet run --project $design --framework net10.0 -- --clean-images

dotnet build ./FrenchExDev.Net.Dotnet.slnx
dotnet test ./FrenchExDev.Net.Dotnet.slnx --framework net10.0 --no-build
```

Le runner accepte aussi `--scrape-parallel`, `--output`, `--dashboard`,
`--add-known-missing`, `--remove-known-missing` et `--list-known-missing`.
La version minimale par défaut est `8.0.100`, la concurrence entre SDK est 2.
`--version` filtre les tags GitHub ; `--min-version` reste un filtre supplémentaire.
Les opérations `--reparse` découvrent les versions du cache.

Sous Windows, les appels Podman de ce processus partagent une limite de quatre
clients simultanés, y compris les builds et les aides de toutes les versions.
Les déconnexions Podman pendant une aide sont retentées deux fois, après une puis
deux secondes. Un échec du runtime (code 125) interrompt la collecte de la version
avant l'écriture du JSON ; les aides ordinaires absentes restent journalisées.
Cette limite ne coordonne pas plusieurs processus `.Design` lancés séparément.

Le jeton optionnel `GITHUB_TOKEN` est lu depuis le `.env` découvert par
`DotEnvLoader`, puis depuis l'environnement en l'absence de valeur dans le fichier.
Pour un accès public sans jeton, laisser cette valeur vide ou absente. Un jeton
rejeté par GitHub doit être corrigé ou retiré ; il n'est jamais transmis au conteneur.

Le script PowerShell expose les mêmes opérations courantes :

```powershell
./scripts/Find-Missing.ps1 -BuildBase
./scripts/Find-Missing.ps1 -BuildImages -Version 8.0.100
./scripts/Find-Missing.ps1 -CleanImages
./scripts/Find-Missing.ps1 -List -MinVersion 8.0.100
./scripts/Find-Missing.ps1 -Version 8.0.100 -Missing
./scripts/Find-Missing.ps1 -Missing -MinVersion 8.0.100 -Parallel 2 -Runtime docker
./scripts/Find-Missing.ps1 -Reparse
```

## Parser et représentation des commandes

`DotnetHelpParser` reconnaît les sections anglaises et françaises, les commandes
du SDK et les outils groupés, les alias séparés par virgule ou `|`, les descriptions
sur plusieurs lignes et les arguments requis, optionnels ou variadiques.
Les noms comme `<PROJECT | SOLUTION>` deviennent des identifiants C# valides.

Les aides historiques de NuGet, `watch` et `user-secrets` omettent certains marqueurs de valeur ;
ces options sont reconnues explicitement. Les aides renvoyées sur stderr, ou
accompagnées d'un code de sortie non nul, sont acceptées uniquement si elles
contiennent une section d'utilisation. Les échecs de collecte d'une sous-commande
sont journalisés : vérifier ces avertissements avant d'exploiter une nouvelle version.

Limites de représentation :

- `msbuild`, `vstest` et `fsi` exposent une liste d'arguments bruts pour préserver
  leurs syntaxes spécifiques (`-property:Name=Value`, etc.).
- Les arguments placés avant une sous-commande, comme `dotnet sln <fichier> add`,
  ne peuvent pas être intercalés par le générateur actuel. Ils sont omis du modèle
  de la commande enfant ; utiliser la résolution du projet/solution depuis son
  répertoire de travail.
- Les options propres aux templates de `dotnet new` passent par les arguments
  variadiques du template. Le scraper ne crée pas de projets pour les découvrir.
- `dotnet help`, qui ouvre la documentation, est exclu du parcours.
- Les options de la racine restent dans le JSON ; le générateur émet actuellement
  les commandes feuilles et ne crée pas une méthode pour chaque option racine.

## Utiliser l'API

```csharp
using FrenchExDev.Net.BinaryWrapper;

var binding = new BinaryBinding
{
    Identifier = new BinaryIdentifier("dotnet"),
    ExecutablePath = "dotnet",
};
var client = global::FrenchExDev.Net.Dotnet.Dotnet.Create(binding);
var command = await client.BuildAsync(b => b.WithConfiguration("Release"));
var executor = new CommandExecutor(new DictionaryBinaryResolver([binding]));
var result = await executor.ExecuteAsync(binding.Identifier, command);
```

Les fichiers générés se trouvent sous `obj/<configuration>/<framework>/Generated`.
Le bootstrap vide est automatiquement exclu dès qu'un JSON de collecte existe.

## Tests

`test/FrenchExDev.Net.Dotnet.Design.Tests` contient les tests des aides réellement
capturées sous Windows et Alpine, des règles de sélection des tags GitHub et du
traitement stdout/stderr. `test/FrenchExDev.Net.Dotnet.Tests` vérifie le runtime et
la sérialisation des commandes générées.

```powershell
dotnet test ./FrenchExDev.Net.Dotnet.slnx --framework net10.0
```

Les références de projets et les versions NuGet restent celles du checkout ;
copier uniquement `Dotnet/` ne produit pas une solution autonome.

Résultat du contrôle en conteneur et commandes de reproduction : [validation](doc/VALIDATION.md).

## Voir les constructions en cours

Les installations de SDK ont lieu dans les conteneurs de construction Buildah.
Pour les afficher pendant un lancement :

```powershell
podman ps -a --external
```

Le runner affiche stdout et stderr du build avec la version, et conserve un
`build.log` sous `scrape/.images/dotnet-sdk/<empreinte>/`. Il affiche aussi un
message toutes les 30 secondes pendant les phases silencieuses. Cette sortie
progressive s'applique aux nouveaux lancements après recompilation ; elle ne
modifie pas l'affichage d'un processus déjà démarré.
