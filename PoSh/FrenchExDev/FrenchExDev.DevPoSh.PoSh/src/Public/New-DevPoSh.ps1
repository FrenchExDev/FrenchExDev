function New-DevPoSh {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true)] [string] $Acme,
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true)] [string] $Name,
        [ValidateNotNullOrWhiteSpace()] [string] $Author,
        [ValidateNotNullOrWhiteSpace()] [string] $Copyright,
        [ValidateNotNullOrWhiteSpace()] [string] $WorkingDirectory
    )

    $poshDir = "$WorkingDirectory/$Acme/$Name.PoSh"

    if (Test-Path $poshDir) {
        throw "New-DevPosh > $poshDir exists"
    }

    New-Item $poshDir -ItemType Directory
    
    New-Item "$poshDir/doc" -ItemType Directory
    New-Item "$poshDir/src" -ItemType Directory
    New-Item "$poshDir/test" -ItemType Directory

    foreach ($directory in @("Classes", "Completers", "Private", "Public", "Variables")) {
        New-Item "$poshDir/src/$directory" -ItemType Directory
    }

    Copy-Item "$PSScriptRoot/../../.gitignore" $poshDir
    $devPoshPsd1Tpl = Get-Content "$PSScriptRoot/../../resources/DevPoSh.PoSh.psd1.tpl" -Raw
    $devPoshPsm1Tpl = Get-Content "$PSScriptRoot/../../resources/DevPoSh.PoSh.psm1.tpl" -Raw

    $tpls = @(@{
            content = $devPoshPsd1Tpl
            path    = "$poshDir/$Name.PoSh.psd1"
        }, @{
            content = $devPoshPsm1Tpl
            path    = "$poshDir/$Name.PoSh.psm1"
        })

    $guid = New-Guid

    foreach ($tpl in $tpls) {
        Write-Verbose "New-DevPoSh > Tpl $($tpl.name)"
        $tpl.content = $tpl.content.Replace("##DevPoSh##", $Name)
        $tpl.content = $tpl.content.Replace("##DATE##", $(Get-Date -Format "dd-MM-yy"))
        $tpl.content = $tpl.content.Replace('##GUID##', $guid)
        $tpl.content = $tpl.content.Replace('##AUTHOR##', $Author)
        $tpl.content = $tpl.content.Replace('##ACME##', $Acme)
        $tpl.content = $tpl.content.Replace('##COPYRIGHT##', $Copyright)
        $tpl.content | Out-File $tpl.path -Encoding ascii -NoNewline
    }

    Copy-Item "$PSScriptRoot/../../LICENSE" "$poshDir/LICENSE"

    Out-File $poshDir/README -Encoding ascii -NoNewline

    Push-Location $poshDir
    New-Git
    Pop-Location
}
