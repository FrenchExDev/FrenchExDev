# Invoke-BinaryClient

Guide du script [Invoke-BinaryClients.ps1](../../Net/FrenchExDev/scripts/Invoke-BinaryClients.ps1) : compilation, tests, collecte et nettoyage des clients BinaryWrapper par lots.

Depuis `Net/FrenchExDev` :

```powershell
.\scripts\Invoke-BinaryClients.ps1
```

Depuis la racine du dépôt :
`.\Net\FrenchExDev\scripts\Invoke-BinaryClients.ps1`.

Le lanceur reprend `dotnet build && .\scripts\Find-Missing.ps1 -Parallel 4 -Missing -ScrapeParallel 12`
sur les clients CLI, avec compilation, tests, collecte par lots, nouvelle
compilation des sources générées, tests et nettoyage avant le lot suivant.

Il découvre les projets qui référencent BinaryWrapper.Design.Lib et possèdent
un lanceur Find-Missing : Docker, DockerCompose, Git, GitLab.Cli, Packer, Podman,
PodmanCompose, Vagrant et Dotnet. Dotnet est exclu par défaut. Les collecteurs de
schémas Bundle ne sont pas sélectionnés.

## Paramètres

| Paramètre | Défaut | Effet |
|---|---|---|
| `-Client` | Tous | Sous-ensemble ; accepte aussi les noms séparés par des virgules |
| `-ExcludeClient` | `@('Dotnet')` | Exclusions ; `@()` inclut Dotnet |
| `-ClientParallel` / `-BatchSize` | `2` | Clients simultanés par lot |
| `-Parallel` | `4` | Versions simultanées par client |
| `-ScrapeParallel` | `12` | Commandes d’aide simultanées par version |
| `-Framework` | `net10.0` | Exécution et tests ; accepte `net11.0` |
| `-Configuration` | `Debug` | Accepte aussi `Release` |
| `-Runtime` | `podman` | Ou `docker` |
| `-MinVersion` | `@{}` | Hashtable client → version minimale |
| `-AllVersions` | Non | Omet `-Missing` et recollecte |
| `-RetryKnownMissing` | Non | Réessaie les exclusions de `_known_missing.txt` en mode Missing |
| `-SkipTests` | Non | Omet les tests ; conserve les compilations |
| `-KeepImages` | Non | Conserve les images et désactive le nettoyage |
| `-OutputRoot` | Corpus des clients | Sorties sous `<OutputRoot>/<Client>` |
| `-LogRoot` | `.fake/binary-clients` à la racine | Rapports, journaux et résultats TRX |
| `-Resume` | Aucun | Rapport JSON de l’exécution à reprendre |
| `-List` | Non | Liste les clients sans exécution |
| `-WhatIf` | Non | Prévisualise sans fichiers, réseau ni nettoyage |

La concurrence maximale vaut `ClientParallel × Parallel × ScrapeParallel` :
le défaut peut donc demander 96 appels d’aide répartis dans huit versions.
Les compilations sont sérialisées pour éviter les conflits sur les dépendances communes.

```powershell
.\scripts\Invoke-BinaryClients.ps1 -WhatIf
.\scripts\Invoke-BinaryClients.ps1 -BatchSize 3 -Parallel 2 -ScrapeParallel 6 -ExcludeClient Dotnet,Git
.\scripts\Invoke-BinaryClients.ps1 -Client Docker,Git -MinVersion @{
    Docker = '29.8.0'
    Git = '2.54.0'
}
.\scripts\Invoke-BinaryClients.ps1 -ExcludeClient @()
```

## Arrêt, nettoyage et reprise

Le moteur est vérifié, puis BinaryWrapper est compilé et testé. Chaque lot passe
par une compilation et des tests avant collecte, puis une compilation et des tests
après collecte. Les compilations couvrent les frameworks déclarés par les projets ;
les tests s’exécutent sur `-Framework`. Un projet annoncé sans tests ne constitue
pas une couverture unitaire, même si `dotnet test` retourne zéro.

Une erreur empêche de lancer de nouvelles versions dès que le signal partagé
est observé ; les versions déjà engagées finissent leurs blocs de nettoyage.
Aucun lot suivant ne démarre. Après la sortie de tous les workers du lot, les
nettoyages de tous ses clients sont tentés, même si le premier échoue. Une erreur de nettoyage
fait échouer le lot.

Le nettoyage utilise `-CleanImages` : images versionnées puis base BinaryWrapper,
avec les labels du client et sans forcer. Il conserve images système, clients exclus,
JSON, aides et recettes locales. Des couches intermédiaires peuvent subsister.
Aucun nettoyage global du moteur n’est exécuté.

Un verrou empêche deux instances de ce lanceur dans le même checkout. Il ne couvre
pas les commandes Find-Missing lancées directement ni les autres checkouts.
Les enfants travaillent dans le répertoire du client, comme une commande manuelle,
et y résolvent leur `.env`. Les identifiants ne sont pas modifiés par le lanceur.

Chaque run conserve `report.json`, stdout/stderr par étape et les résultats TRX.
La console indique le rapport et affiche un suivi toutes les 30 secondes.

Les sorties stdout et stderr des processus sont décodées explicitement en UTF-8
avant affichage et écriture dans les journaux, même si la console Windows utilise
la page de codes 850 ou 1252. L’option `DOTNET_CLI_FORCE_UTF8_ENCODING=1` est limitée
aux processus enfants ; l’encodage de la console appelante reste inchangé.
La correction s’applique au prochain lancement ; les anciens journaux ne sont
pas réécrits. Voir la [configuration UTF-8 de .NET](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_cli_force_utf8_encoding).
Après correction du code ou de la cause externe :

```powershell
.\scripts\Invoke-BinaryClients.ps1 -Resume 'D:\...\report.json'
```

La reprise restaure les paramètres, ignore les lots terminés avec succès et
rejoue les clients du lot incomplet, avec compilation, tests et nouveaux journaux.
Les trois niveaux de concurrence peuvent être ajustés. Un changement de corpus,
framework, mode de collecte ou validation nécessite un nouveau run sans Resume.

En mode `-FailFast`, un échec n’est pas automatiquement ajouté à
`_known_missing.txt` : il reste rejouable. Les exclusions antérieures continuent
de s’appliquer sauf `-RetryKnownMissing`. Les collectes directes sans FailFast
conservent leur comportement historique.

Les neuf Find-Missing acceptent `-NoBuild`, `-Configuration`, `-FailFast`,
`-StopFile` et `-RetryKnownMissing`. Les collectes des lots utilisent NoBuild,
après la compilation explicite. Avec OutputRoot, la validation compile le corpus
habituel du dépôt ; elle n’intègre pas automatiquement les JSON externes.

Une erreur PowerShell produit un code de sortie non nul avec `pwsh -File`.
Le script n’édite pas automatiquement le code après une panne : corriger, tester,
puis reprendre.

## Vérification

```powershell
.\scripts\Test-BinaryClients.ps1
.\scripts\Test-BinaryClientEncoding.ps1 -Integration
dotnet test .\BinaryWrapper\test\FrenchExDev.Net.BinaryWrapper.Design.Lib.Tests --framework net10.0
dotnet test .\BinaryWrapper\test\FrenchExDev.Net.BinaryWrapper.Design.Lib.Tests --framework net11.0
```

Les tests PowerShell combinent des processus enfants réels pour les flux et codes
de sortie avec des commandes simulées pour les pannes de compilation, tests,
collecte, démarrage et nettoyage. Ils ne contactent aucun moteur ni réseau.
Les tests C# couvrent l’arrêt de la file, la coordination, la fin des versions
actives et leur reprise Missing.

Pour les images et les tests Podman réels, consulter
[le guide BinaryWrapper](../../Net/FrenchExDev/BinaryWrapper/doc/UPGRADE-IMAGE-PIPELINES.md).
