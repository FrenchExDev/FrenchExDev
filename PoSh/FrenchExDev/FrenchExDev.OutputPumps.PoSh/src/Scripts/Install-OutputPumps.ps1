<#
.SYNOPSIS
Install the managed OutputPumps agent instruction block.
.DESCRIPTION
Preserve unrelated user instructions and keep profile activation a separate, explicit user action.
#>


#requires -Version 7.4
#requires -PSEdition Core
[CmdletBinding()]
param([string]$CodexHome=$(if($env:CODEX_HOME){$env:CODEX_HOME}else{Join-Path $HOME '.codex'}),[string]$LibraryRoot=(Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))
Import-Module (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Core.psm1') -ErrorAction Stop
$ErrorActionPreference='Stop'
try {
    $library=(Resolve-Path -LiteralPath $LibraryRoot).ProviderPath
    if(!(Test-Path -LiteralPath (Join-Path $library 'index.md'))){throw 'Library index missing'}
    $index=Join-Path $library 'index.md'
    $block=@'
<!-- output-pumps:begin -->
## Pompes de sortie — obligatoire

PowerShell 7.4+ Core uniquement. Bibliothèque commune : __INDEX__

Avant toute commande dont la sortie peut être longue :
1. Rechercher sa documentation, les options réduisant la sortie et son format exploitable. Réutiliser une fiche vérifiée lorsquʼelle couvre la commande/version. Extraire lʼaide locale via Get-PumpHelp si elle est longue.
2. Lire lʼindex ci-dessus, puis uniquement la fiche du logiciel retenu (index + 1). Nʼouvrir le code dʼun module quʼaprès sélection et si nécessaire.
3. Initialiser le registre avec Setup-OutputPumps et ~/.pumps-config.yaml, puis vérifier la couverture avec Get-OutputPump avant Pump-Me. Une commande sans route est un passthrough natif brut, interdit pour une commande bruyante de l’agent. Réutiliser les modules déjà chargés ; si la pompe manque, créer un fournisseur OPumper paramétré à partir de sa documentation.
4. Générer un script PowerShell dʼappel (New-OutputPumpScript), lʼexécuter et ne lire que son objet JSON compact. Ne jamais afficher Invoke-PumpProcess, stdout, stderr ou un log brut.
5. Si Truncation.Applied est vrai, resserrer chemin, filtre, fenêtre ou plafond. Ne pas revenir à la commande brute. TotalCount nul signifie inconnu.
6. Toute pompe générale réutilisable appartient à un fournisseur indépendant FrenchExDev.OPumper.*.PoSh, avec manifeste et src/Setup-OutputPumps.ps1 utilisant des closures .GetNewClosure(). Register-OutputPump -Descriptor valide et actualise sa documentation ; l’inscription live en mémoire ne persiste pas. Fiche et index à jour font partie du travail. Aucun script ponctuel dans le catalogue.

Objets : résumé en une ligne, données utiles, succès/échec/expiration distincts, conseil de relance, troncature explicite et masquage des secrets. Défauts : 20 éléments, 8 Kio de JSON UTF-8 compact, 600 secondes. Aucune relance automatique. Les permissions normales restent applicables.

Pour plusieurs appels, générer un script avec New-OutputPumpScript -TaskScriptBlock et utiliser Invoke-OutputPumpStep pour chaque résultat intermédiaire consommé localement. Invoke-OutputPumpTask doit émettre une seule enveloppe bornée, conservant les échecs, expirations et troncatures des étapes. Une limite par appel ne constitue pas un budget global.

Une commande connue comme courte peut être lancée directement. Cette règle concerne aussi git, rg, builds, tests, inventaires, aide et logs. Importer uniquement les modules retenus. Ne pas charger le catalogue complet ni toutes les fiches dans le contexte.
<!-- output-pumps:end -->
'@
    $block=$block.Replace('__INDEX__',$index)
    [void][IO.Directory]::CreateDirectory($CodexHome)
    $path=Join-Path $CodexHome 'AGENTS.md'
    $old=if([IO.File]::Exists($path)){[IO.File]::ReadAllText($path)}else{''}
    $pattern='(?s)<!-- output-pumps:begin -->.*?<!-- output-pumps:end -->'
    if(([regex]::Matches($old,$pattern)).Count -gt 1){throw 'Multiple managed instruction blocks'}
    if(($old.Contains('<!-- output-pumps:begin -->') -or $old.Contains('<!-- output-pumps:end -->')) -and $old -notmatch $pattern){throw 'Incomplete managed instruction block'}
    $updated=if($old -match $pattern){[regex]::Replace($old,$pattern,[Text.RegularExpressions.MatchEvaluator]{param($m)$block})}else{$old+$(if($old.Length){"`n`n"}else{''})+$block+"`n"}
    $backup=$null
    if($updated -cne $old){
        if($old.Length){$backup=$path+'.backup-'+[guid]::NewGuid().ToString('N');[IO.File]::Copy($path,$backup)}
        $temporary=$path+'.tmp-'+[guid]::NewGuid().ToString('N')
        try{[IO.File]::WriteAllText($temporary,$updated,[Text.UTF8Encoding]::new($false));[IO.File]::Move($temporary,$path,$true)}
        finally{if([IO.File]::Exists($temporary)){[IO.File]::Delete($temporary)}}
    }
    New-PumpResult -Summary 'User instruction installed; unrelated instructions preserved.' -Data @{Items=@(@{Path=$path;Changed=($updated -cne $old);Backup=$backup;OverridePresent=(Test-Path -LiteralPath (Join-Path $CodexHome 'AGENTS.override.md'));Index=$index})}
} catch {New-PumpResult -Summary 'User instruction installation failed.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='InstallationFailed';Message=$_.Exception.Message}}
