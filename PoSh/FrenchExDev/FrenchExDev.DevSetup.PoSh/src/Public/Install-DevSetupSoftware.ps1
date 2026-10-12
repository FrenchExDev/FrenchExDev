function Install-DevSetupSoftware {
    [CmdletBinding()]
    param(
        [string] $WingetSoftwaresFile = "./softwares-winget-source-winget.dat",
        [string] $MsStoreSoftwaresFile = "./softwares-winget-source-msstore.dat"
    )

    $sources = [pscustomobject] @{
        winget  = Get-Content $WingetSoftwaresFile
        msstore = Get-Content $MsStoreSoftwaresFile
    }

    foreach ($source in @("winget", "msstore")) {
        foreach ($software in $sources.$source) {
            if ([string]::isnullorempty($software)) { continue; } 
            Write-Verbose "Install-DevSetupSoftwares> winget install --id $software --source $source"
            winget install --id $software --source winget
        }
    }
}
