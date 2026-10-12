function Import-DevPoShModule {
    [CmdletBinding()]
    param([string[]]$Path=@($env:FRENCHEXDEV_MYDEVPOSH_PATH),[string]$Acme,[string]$Module,[switch]$Force)
    if(!$Path -or [string]::IsNullOrWhiteSpace($Path[0])){throw 'Configure FRENCHEXDEV_MYDEVPOSH_PATH or specify -Path.'}
    $projects=@(Get-DevPoShProjects $Path)
    foreach($project in $projects) {
        if($Acme -and $project.Acme -ne $Acme){continue}
        if($Module -and $project.Name -ne $Module -and (Split-Path $project.Path -Leaf) -ne $Module){continue}
        $rootModule=if($project.Data.RootModule){$project.Data.RootModule}else{$project.Data.ModuleToProcess}
        $modulePath=[IO.Path]::GetFullPath((Join-Path $project.Path $rootModule))
        if(!$Force -and (Get-Module | Where-Object {$_.Path -eq $modulePath -or $_.Path -eq $project.Manifest})){continue}
        try{Import-Module -Name $project.Manifest -Global -DisableNameChecking -Force:$Force -ErrorAction Stop}
        catch{Write-Warning "DevPoSh: failed to import $($project.Name); no automatic retry."}
    }
}
