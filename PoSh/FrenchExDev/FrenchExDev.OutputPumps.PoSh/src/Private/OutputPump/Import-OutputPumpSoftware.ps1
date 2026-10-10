function Import-OutputPumpSoftware {
    param($Entry)
    $path=if([IO.Path]::IsPathRooted($Entry.Module)){$Entry.Module}else{Join-Path $Entry.Root $Entry.Module}
    $path=[IO.Path]::GetFullPath($path)
    $module=Get-Module -All | Where-Object Path -eq $path | Select-Object -First 1
    if(!$module){$module=Import-Module -Name $path -PassThru -DisableNameChecking -ErrorAction Stop}
    $module
}
