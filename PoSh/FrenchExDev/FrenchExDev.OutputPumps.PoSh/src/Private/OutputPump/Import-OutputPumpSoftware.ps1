<#
.SYNOPSIS
Resolve the module that implements a specialized pump.
.DESCRIPTION
Define Import-OutputPumpSoftware for the OutputPumps runtime. Reuse a matching loaded provider and import the selected module only when necessary.
#>


function Import-OutputPumpSoftware {
    <#
    .SYNOPSIS
    Resolve the module that implements a specialized pump.
    .DESCRIPTION
    Reuse a matching loaded provider and import the selected module only when necessary.
    #>
    
    param($Entry)
    $path=if([IO.Path]::IsPathRooted($Entry.Module)){$Entry.Module}else{Join-Path $Entry.Root $Entry.Module}
    $path=[IO.Path]::GetFullPath($path)
    $module=Get-Module -All | Where-Object Path -eq $path | Select-Object -First 1
    if(!$module){$module=Import-Module -Name $path -PassThru -DisableNameChecking -ErrorAction Stop}
    $module
}
