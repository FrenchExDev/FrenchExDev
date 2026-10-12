# .NET CLI

La CLI .NET sélectionne le SDK selon le répertoire de travail et global.json. Toujours donner Target et WorkingDirectory explicitement. Les pompes nʼajoutent pas de parallélisme ; build et test peuvent écrire leurs artefacts habituels. NoRestore et NoBuild ne sont activés que sur demande.

Build désactive lʼaffichage terminal dynamique (`--tl:off`) et conserve les diagnostics de compilateur structurés : chemin, ligne, sévérité, code, message. Les compteurs portent sur les diagnostics uniques capturés ; un format de diagnostic inconnu nʼest pas transformé en log de secours. Ce profil suppose un SDK supportant `--tl:off`.

Tests : détecter DOTNET_TEST_RUNNER, puis le global.json le plus proche, sinon VSTest. Les arguments diffèrent entre VSTest et Microsoft.Testing.Platform. Le profil MTP demande une extension TRX disponible ; son absence produit Unsupported. Les rapports sont isolés par invocation, lus puis supprimés. Seuls compteurs et échecs utiles sont rendus. Aucun rapport, rapport trop grand ou malformé : aucun succès supposé et aucun compteur inventé. Un rapport sans test nʼest pas une preuve de tests réussis.

Une commande personnalisée (`dotnet run`, QualityGate, mutation testing…) nécessite sa propre pompe adaptée à son rapport. Un résultat de tests ordinaires ne prouve pas la couverture ni la mutation.

Version vérifiée : SDK 11.0.100-rc.1.26425.128 ; profils VSTest/MTP, vérifié le 2026-10-10.

Documentation officielle :

- <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build>
- <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test>
- <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-vstest>
- <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp>

Module : `../FrenchExDev.OPumper.Dotnet.PoSh/Dotnet-OPumper.psm1`.

Appels disponibles :

- Route : `dotnet build` ; identifiant `dotnet.build`.
- `Invoke-PumpDotnetBuild` : Compiler une cible et sélectionner les diagnostics.
  Paramètres du besoin : `Target, WorkingDirectory, Configuration, Framework, NoRestore, Environment`.
  Exemple : `Invoke-PumpDotnetBuild -WorkingDirectory $repo -Target 'app.csproj'`.
- Route : `dotnet test` ; identifiant `dotnet.test`.
- `Invoke-PumpDotnetTest` : Exécuter des tests avec compteurs TRX. Sous MTP, fournir MtpFilterOption avec Filter selon la documentation du framework.
  Paramètres du besoin : `Target, WorkingDirectory, Configuration, Framework, Filter, MtpFilterOption, NoRestore, NoBuild, Environment`.
  Exemple : `Invoke-PumpDotnetTest -WorkingDirectory $repo -Target 'tests.csproj'`.

Paramètres communs : `MaxItems` (20), `MaxOutputBytes` (8192, minimum 1024), `TimeoutSeconds` (600). Objets uniquement ; aucun affichage terminal.
