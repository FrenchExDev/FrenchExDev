# Validation Dotnet Design — 2026-09-09

- SDK hôte : `11.0.100-rc.1.26425.128`, exécution du Design sous `net10.0`.
- SDK collecté : `8.0.100`, image de base `alpine:3.19`, archive `linux-musl-x64`.
- Le pipeline a construit l'image, lancé le conteneur, vérifié `dotnet --version`
  et parcouru les aides. Le conteneur et l'image de collecte ont été supprimés
  par les middlewares. L'image de base préexistante est conservée.
- 104 aides brutes conservées dans `src/FrenchExDev.Net.Dotnet/scrape/help/8.0.100/`.
- Le JSON relu avec le parser final contient 104 nœuds, 82 commandes feuilles,
  633 options cumulées par nœud et 69 arguments cumulés par nœud.
- Compilation de `FrenchExDev.Net.Dotnet.slnx` : net10.0 et net11.0,
  zéro erreur et zéro avertissement.
- Tests sous net10.0 : 30 tests Design et 6 tests runtime, tous réussis.
- Le mode `--reparse --list --min-version 8.0.100` retrouve la version du cache.
- `--version 8.0.100 --missing --list` interroge le fournisseur GitHub et
  retourne zéro version manquante après collecte.

Le jeton du `.env` du dépôt était rejeté par GitHub (HTTP 401). Pour valider les
tags publics sans modifier ce fichier, le lancement a été effectué depuis un
répertoire temporaire contenant un `.env` sans jeton. La configuration du dépôt
n'a pas été modifiée. Le Program affiche désormais une erreur explicite sur un
jeton rejeté.

Commandes reproductibles depuis `Dotnet/` (avec un jeton valide ou aucun jeton) :

```powershell
$design = './src/FrenchExDev.Net.Dotnet.Design/FrenchExDev.Net.Dotnet.Design.csproj'
dotnet run --project $design --framework net10.0 -- --version 8.0.100 --parallel 1 --scrape-parallel 2
dotnet run --project $design --framework net10.0 -- --reparse
dotnet build ./FrenchExDev.Net.Dotnet.slnx
dotnet test ./FrenchExDev.Net.Dotnet.slnx --framework net10.0 --no-build
./scripts/Find-Missing.ps1 -Reparse -List -MinVersion 8.0.100
```

Cette validation d'une version ne garantit pas que chaque SDK présent dans les
tags GitHub puisse fonctionner sur Alpine 3.19. Les différences de grammaire
non représentables sont précisées dans le README.

## Correctif des déconnexions Podman — 2026-09-09

Le processus observé utilisait `--parallel 12 --scrape-parallel 4`. Les appels
Podman du même processus partagent désormais une limite de quatre sous Windows,
y compris les builds. Les aides interrompues par une erreur de connexion Podman
(code 125) sont retentées deux fois, après une puis deux secondes. Un code 125
persistant remonte jusqu'au runner sans créer ni remplacer le JSON de la version.

Contrôles effectués en configuration `Release` :

- Dotnet, `net10.0` : 41 tests Design et 6 tests runtime réussis.
- Dotnet, `net11.0` : 41 tests Design et 5 tests runtime réussis ; le sixième test
  runtime (`Descriptor_UsesExpectedExecutableAndSerialization`) est bloqué au
  chargement de `FrenchExDev.Net.BinaryWrapper.Attributes.dll` par Windows
  Application Control (`0x800711C7`). Un second passage sans recompilation
  reproduit le blocage ; les événements Code Integrity 3033 et 3077 le confirment.
- BinaryWrapper, pour chacun des frameworks `net10.0` et `net11.0` : 483 tests
  Design, 158 tests Design.Lib, 273 tests SourceGenerator et 311 tests runtime
  réussis. Deux tests optionnels de conteneurs sont ignorés par framework.
- Les nouvelles régressions couvrent la concurrence entre appels Podman,
  la libération des places après échec, la récupération après déconnexion,
  l'épuisement des tentatives et la conservation du fichier existant en cas
  d'erreur du runtime à la racine ou dans une sous-commande.

```powershell
dotnet test ./FrenchExDev.Net.Dotnet.slnx --framework net10.0 --configuration Release --no-restore
dotnet test ./FrenchExDev.Net.Dotnet.slnx --framework net11.0 --configuration Release --no-restore
dotnet test ../BinaryWrapper/FrenchExDev.Net.BinaryWrapper.slnx --configuration Release --no-restore
```

Un processus `.Design` déjà démarré conserve l'ancien code : arrêter puis relancer
le script pour utiliser le correctif. La limite est propre à chaque processus ;
elle ne coordonne pas des collecteurs lancés séparément.

Contrôle réel : un conteneur temporaire sans réseau, créé depuis l'image en cache
SDK `8.0.203`, a répondu à `dotnet --version`. Les seize demandes concurrentes
`dotnet nuget --help` ont bien été limitées à quatre processus Podman actifs, mais
le contrôle a échoué sur un timeout de 60 secondes pendant que la VM devenait à
nouveau non réactive. Le conteneur temporaire a ensuite été supprimé avec succès.
La collecte complète en conditions réelles n'est donc pas validée par ce contrôle.
Le processus de collecte déjà actif utilisait encore l'ancien exécutable.
