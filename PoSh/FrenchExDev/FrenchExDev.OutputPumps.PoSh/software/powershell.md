# PowerShell Core

PowerShell Core est le moteur dʼexécution de la bibliothèque. Windows PowerShell 5.1 est exclu. Le minimum 7.4 permet les API .NET modernes utilisées pour les processus et lʼannulation. Si un alias WindowsApps échoue dans le sandbox, employer une installation Core accessible ou les permissions dʼexécution autorisées ; ne pas remplacer le moteur par 5.1.

Inventaire : parcours explicite via Path, Filter, Recurse et Force. Lecture : Path littéral, StartLine et LineCount ; Filter est une expression régulière avec limite de temps. Les fichiers sont lus progressivement, avec 512 caractères conservés par ligne ; une ligne plus longue est signalée. Les clés privées multilignes sont supprimées. La lecture reconnaît les BOM et utilise UTF-8 par défaut ; préciser un autre besoin dʼencodage exige une extension de la pompe.

Logs : seules les dernières lignes correspondant au filtre sont retenues. Le champ Window décrit le périmètre ; TailWindow signale que lʼobjet nʼest pas le journal complet. Les compteurs concernent la fenêtre et le filtre, jamais implicitement tous les fichiers.

Pour une aide locale longue, importer uniquement `Core.psm1` et appeler `Get-PumpHelp -Executable $outil -Arguments @('--help') -Filter 'option utile'`. Il renvoie des sections sélectionnées, sans déverser le manuel.

Version vérifiée : PowerShell Core 7.6.6 ; minimum 7.4, vérifié le 2026-10-10.

Documentation officielle :

- <https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/get-childitem>
- <https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/get-content>
- <https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/get-help>

Module : `../FrenchExDev.OPumper.PowerShell.PoSh/PowerShell-OPumper.psm1`.

Appels disponibles :

- Route : `pwsh directory` ; identifiant `powershell.directory`.
- `Get-PumpDirectory` : Inventaire borné de fichiers et répertoires.
  Paramètres du besoin : `Path, Filter, Recurse, Force`.
  Exemple : `Get-PumpDirectory -Path $repo -Filter '*.md'`.
- Route : `pwsh file` ; identifiant `powershell.file`.
- `Get-PumpFile` : Fenêtre de lignes numérotées.
  Paramètres du besoin : `Path, StartLine, LineCount, Filter, Tail`.
  Exemple : `Get-PumpFile -Path $file -StartLine 100 -LineCount 20`.
- Route : `pwsh log` ; identifiant `powershell.log`.
- `Get-PumpLog` : Derniers événements correspondant au filtre.
  Paramètres du besoin : `Path, Filter`.
  Exemple : `Get-PumpLog -Path $log -Filter 'error|warning' -MaxItems 10`.

Paramètres communs : `MaxItems` (20), `MaxOutputBytes` (8192, minimum 1024), `TimeoutSeconds` (600). Objets uniquement ; aucun affichage terminal.
