#requires -Version 7.4
#requires -PSEdition Core
[CmdletBinding()]
param()
Import-Module (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'OutputPumps.psd1') -ErrorAction Stop
Import-Module (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Core.psm1') -ErrorAction Stop

function New-CommandEntry($Function,$When,$Parameters,$Example){@{Function=$Function;When=$When;Parameters=@($Parameters);Example=$Example}}
$descriptors=@(
    @{
        Id='git';Name='Git';Purpose='État, changements et historique dʼun dépôt';Module='Modules/Git.psm1'
        VerifiedVersion='Git 2.55.0.windows.3, commandes locales vérifiées le 2026-10-10'
        Documentation=@('https://git-scm.com/docs/git-status','https://git-scm.com/docs/git-diff','https://git-scm.com/docs/git-log')
        IdentityMarkdown=@'
Git gère les commits, lʼindex et les modifications du répertoire de travail. Ces pompes sont en lecture seule et ne font aucun fetch : ahead/behind décrit uniquement les références locales connues.

Status utilise `--porcelain=v2 --branch -z --untracked-files=all`. Les chemins sont groupés par état (Staged, Unstaged, Untracked, Conflict) et les renommages conservent les deux noms. `Path` réduit le périmètre ; une capture trop large exige un chemin plus précis. Les branches détachées ou sans commit restent identifiables.

Diff utilise `--numstat -z`, sans pager, textconv ni diff externe. `Staged` sélectionne lʼindex, `Revision` une base explicite. Les fichiers binaires nʼont pas de compteurs de lignes. Pour inspecter du contenu, `Get-PumpGitChanges` exige un chemin et renvoie des lignes changées structurées, pas le patch brut. Log demande un élément supplémentaire pour détecter la troncature ; le total de lʼhistorique peut rester inconnu. Les sujets sont assainis ; auteurs et emails ne sont pas exportés.
'@
        Commands=@(
            (New-CommandEntry 'Get-PumpGitStatus' 'Branche, ahead/behind et chemins groupés.' @('WorkingDirectory','Path') "Get-PumpGitStatus -WorkingDirectory `$repo -MaxItems 10")
            (New-CommandEntry 'Get-PumpGitDiff' 'Statistiques des changements par chemin.' @('WorkingDirectory','Path','Staged','Revision') "Get-PumpGitDiff -WorkingDirectory `$repo -Path 'src/'")
            (New-CommandEntry 'Get-PumpGitChanges' 'Lignes changées dʼun chemin ciblé.' @('WorkingDirectory','Path','Staged') "Get-PumpGitChanges -WorkingDirectory `$repo -Path 'src/app.cs'")
            (New-CommandEntry 'Get-PumpGitLog' 'Historique limité, sujets et dates.' @('WorkingDirectory','Path','Revision') "Get-PumpGitLog -WorkingDirectory `$repo -MaxItems 5")
        )
    }
    @{
        Id='ripgrep';Name='ripgrep (rg)';Purpose='Recherche de texte et découverte de fichiers';Module='Modules/Ripgrep.psm1'
        VerifiedVersion='ripgrep 15.2.0, commandes locales vérifiées le 2026-10-10'
        Documentation=@('https://github.com/BurntSushi/ripgrep/blob/master/GUIDE.md','https://github.com/BurntSushi/ripgrep/blob/master/doc/rg.1.txt.tpl')
        IdentityMarkdown=@'
Ripgrep recherche dans les fichiers en respectant par défaut les règles dʼignorance ; il évite normalement les fichiers cachés et binaires. Utiliser Path et Glob pour cibler le besoin, Literal pour une chaîne exacte et IgnoreCase si nécessaire.

La recherche lit `rg --json` : chemins, numéros de ligne et extraits sont sélectionnés. La découverte de fichiers utilise `--files --null` pour préserver les noms inhabituels. Code 0 : résultats ; 1 : aucun résultat, succès fonctionnel ; 2 : erreur, potentiellement accompagnée de résultats partiels. Une erreur de lecture ne prouve jamais lʼabsence de correspondance. Les événements à encodage non représentable sont omis et signalés. Les extraits longs ou sensibles sont limités et assainis.

Ne pas utiliser `--max-count` comme plafond global : cette option est par fichier. Le budget final est imposé par la pompe. Si la capture entière dépasse son plafond, réduire chemins ou filtres, sans afficher le flux JSON source.
'@
        Commands=@(
            (New-CommandEntry 'Find-PumpText' 'Correspondances structurées, filtres et compteurs.' @('WorkingDirectory','Pattern','Path','Glob','Literal','IgnoreCase') "Find-PumpText -WorkingDirectory `$repo -Pattern 'TODO' -Path 'src/' -MaxItems 10")
            (New-CommandEntry 'Find-PumpFiles' 'Chemins de fichiers ciblés.' @('WorkingDirectory','Path','Glob') "Find-PumpFiles -WorkingDirectory `$repo -Glob '*.cs' -Path 'src/'")
        )
    }
    @{
        Id='powershell';Name='PowerShell Core';Purpose='Inventaires, lectures ciblées, aide et logs';Module='Modules/PowerShell.psm1'
        VerifiedVersion='PowerShell Core 7.6.6 ; minimum 7.4, vérifié le 2026-10-10'
        Documentation=@('https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/get-childitem','https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/get-content','https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/get-help')
        IdentityMarkdown=@'
PowerShell Core est le moteur dʼexécution de la bibliothèque. Windows PowerShell 5.1 est exclu. Le minimum 7.4 permet les API .NET modernes utilisées pour les processus et lʼannulation. Si un alias WindowsApps échoue dans le sandbox, employer une installation Core accessible ou les permissions dʼexécution autorisées ; ne pas remplacer le moteur par 5.1.

Inventaire : parcours explicite via Path, Filter, Recurse et Force. Lecture : Path littéral, StartLine et LineCount ; Filter est une expression régulière avec limite de temps. Les fichiers sont lus progressivement, avec 512 caractères conservés par ligne ; une ligne plus longue est signalée. Les clés privées multilignes sont supprimées. La lecture reconnaît les BOM et utilise UTF-8 par défaut ; préciser un autre besoin dʼencodage exige une extension de la pompe.

Logs : seules les dernières lignes correspondant au filtre sont retenues. Le champ Window décrit le périmètre ; TailWindow signale que lʼobjet nʼest pas le journal complet. Les compteurs concernent la fenêtre et le filtre, jamais implicitement tous les fichiers.

Pour une aide locale longue, importer uniquement `Core.psm1` et appeler `Get-PumpHelp -Executable $outil -Arguments @('--help') -Filter 'option utile'`. Il renvoie des sections sélectionnées, sans déverser le manuel.
'@
        Commands=@(
            (New-CommandEntry 'Get-PumpDirectory' 'Inventaire borné de fichiers et répertoires.' @('Path','Filter','Recurse','Force') "Get-PumpDirectory -Path `$repo -Filter '*.md'")
            (New-CommandEntry 'Get-PumpFile' 'Fenêtre de lignes numérotées.' @('Path','StartLine','LineCount','Filter','Tail') "Get-PumpFile -Path `$file -StartLine 100 -LineCount 20")
            (New-CommandEntry 'Get-PumpLog' 'Derniers événements correspondant au filtre.' @('Path','Filter') "Get-PumpLog -Path `$log -Filter 'error|warning' -MaxItems 10")
        )
    }
    @{
        Id='dotnet';Name='.NET CLI';Purpose='Build et tests avec diagnostics et résultats structurés';Module='Modules/Dotnet.psm1'
        VerifiedVersion='SDK 11.0.100-rc.1.26425.128 ; profils VSTest/MTP, vérifié le 2026-10-10'
        Documentation=@('https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build','https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test','https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-vstest','https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp')
        IdentityMarkdown=@'
La CLI .NET sélectionne le SDK selon le répertoire de travail et global.json. Toujours donner Target et WorkingDirectory explicitement. Les pompes nʼajoutent pas de parallélisme ; build et test peuvent écrire leurs artefacts habituels. NoRestore et NoBuild ne sont activés que sur demande.

Build désactive lʼaffichage terminal dynamique (`--tl:off`) et conserve les diagnostics de compilateur structurés : chemin, ligne, sévérité, code, message. Les compteurs portent sur les diagnostics uniques capturés ; un format de diagnostic inconnu nʼest pas transformé en log de secours. Ce profil suppose un SDK supportant `--tl:off`.

Tests : détecter DOTNET_TEST_RUNNER, puis le global.json le plus proche, sinon VSTest. Les arguments diffèrent entre VSTest et Microsoft.Testing.Platform. Le profil MTP demande une extension TRX disponible ; son absence produit Unsupported. Les rapports sont isolés par invocation, lus puis supprimés. Seuls compteurs et échecs utiles sont rendus. Aucun rapport, rapport trop grand ou malformé : aucun succès supposé et aucun compteur inventé. Un rapport sans test nʼest pas une preuve de tests réussis.

Une commande personnalisée (`dotnet run`, QualityGate, mutation testing…) nécessite sa propre pompe adaptée à son rapport. Un résultat de tests ordinaires ne prouve pas la couverture ni la mutation.
'@
        Commands=@(
            (New-CommandEntry 'Invoke-PumpDotnetBuild' 'Compiler une cible et sélectionner les diagnostics.' @('Target','WorkingDirectory','Configuration','Framework','NoRestore') "Invoke-PumpDotnetBuild -WorkingDirectory `$repo -Target 'app.csproj'")
            (New-CommandEntry 'Invoke-PumpDotnetTest' 'Exécuter des tests avec compteurs TRX. Sous MTP, fournir MtpFilterOption avec Filter selon la documentation du framework.' @('Target','WorkingDirectory','Configuration','Framework','Filter','MtpFilterOption','NoRestore','NoBuild') "Invoke-PumpDotnetTest -WorkingDirectory `$repo -Target 'tests.csproj'")
        )
    }
    @{
        Id='podman';Name='Podman';Purpose='Conteneurs, images et événements ciblés';Module='Modules/Podman.psm1'
        VerifiedVersion='Podman CLI 6.1.2 ; disponibilité du service non présumée, vérifié le 2026-10-10'
        Documentation=@('https://docs.podman.io/en/latest/markdown/podman-ps.1.html','https://docs.podman.io/en/latest/markdown/podman-images.1.html','https://docs.podman.io/en/latest/markdown/podman-logs.1.html')
        IdentityMarkdown=@'
Podman gère les conteneurs et images OCI. Sous Windows, la CLI peut dépendre dʼune machine et dʼun service accessibles. Ces pompes ne démarrent pas de machine, ne téléchargent pas dʼimage et ne modifient pas de conteneur.

Les inventaires utilisent `--format json`, puis sélectionnent identifiants, noms, images, états et tailles. Ils nʼexportent ni variables dʼenvironnement ni commandes complètes de conteneurs. Filter transmet les filtres natifs documentés, All inclut les conteneurs arrêtés.

Les logs exigent Container. Utiliser Since et Filter pour cibler le besoin ; seule une fin de journal plafonnée est interrogée, jamais follow. Chaque événement conserve sa date, son flux et son message assaini. Les flux sont triés par date ; lʼordre exact entre événements de même date nʼest pas garanti. Le total global reste inconnu. Un service inaccessible est un échec distinguable dʼun inventaire vide.
'@
        Commands=@(
            (New-CommandEntry 'Get-PumpPodmanContainers' 'Inventaire de conteneurs sélectionnés.' @('WorkingDirectory','Filter','All') "Get-PumpPodmanContainers -Filter 'status=running' -MaxItems 10")
            (New-CommandEntry 'Get-PumpPodmanImages' 'Inventaire dʼimages sélectionnées.' @('WorkingDirectory','Filter') "Get-PumpPodmanImages -Filter 'dangling=true'")
            (New-CommandEntry 'Get-PumpPodmanLog' 'Derniers événements dʼun conteneur.' @('WorkingDirectory','Container','Since','Filter') "Get-PumpPodmanLog -Container `$container -Since '10m' -Filter 'error'")
        )
    }
    @{
        Id='pnpm';Name='pnpm';Purpose='Inventaire limité des dépendances Node';Module='Modules/Pnpm.psm1'
        VerifiedVersion='pnpm 12.8.1, exécutable direct et list --json vérifiés le 2026-10-10'
        Documentation=@('https://pnpm.io/cli/list')
        IdentityMarkdown=@'
pnpm gère les dépendances et workspaces Node. Cette première pompe inspecte les dépendances installées avec `list --json --depth`, sans installation, réparation ou modification du lockfile.

WorkingDirectory choisit le projet ; Package filtre les noms ; Recursive parcourt les projets du workspace. Depth vaut 0 par défaut, maximum 5 pour éviter une expansion incontrôlée. La sortie contient nom/version/type/projet/profondeur, sans chemins internes du store ni configuration dʼauthentification.

Sous Windows, invoquer directement pnpm.exe. Si Get-Command résout un launcher pnpm.ps1, chercher son exécutable pnpm voisin ; ne pas utiliser de couche cmd ou modifier lʼExecutionPolicy. Un exécutable direct introuvable produit Unsupported. Une dépendance absente ou un store déplacé ne déclenche aucune réinstallation automatique.

Les commandes install, run et exec nécessitent des pompes adaptées. Un script pnpm lançant un runner de tests doit fournir le rapport structuré de ce runner ; ne pas présenter son log comme un résultat de tests analysé.
'@
        Commands=@((New-CommandEntry 'Get-PumpPnpmDependencies' 'Dépendances installées et versions sélectionnées.' @('WorkingDirectory','Package','Recursive','Depth') "Get-PumpPnpmDependencies -WorkingDirectory `$repo -Depth 0 -MaxItems 10"))
    }
)
$failed=[Collections.Generic.List[object]]::new()
foreach($descriptor in $descriptors){$result=Register-OutputPump -Descriptor $descriptor;if($result.Outcome -ne 'Success'){$failed.Add(@{Id=$descriptor.Id;Error=$result.Error})}}
if($failed.Count){New-PumpResult -Summary 'Initial catalogue registration failed.' -Outcome Failed -Data @{Items=$failed.ToArray()} -RetryAdvice CorrectRequest;exit 1}
Test-OutputPumpCatalogue
