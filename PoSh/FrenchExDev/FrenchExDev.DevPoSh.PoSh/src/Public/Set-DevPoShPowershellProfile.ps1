function Set-DevPoShPowershellProfile {
    param(
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true)] [string] $MyPoShPath,
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $false)] [string] $Acme = "FrenchExDev",
        [ValidateNotNullOrWhiteSpace()] [string] $PowershellProfile = "$env:USERPROFILE\Documents\PowerShell\Microsoft.PowerShell_profile.ps1",
        [ValidateNotNullOrWhiteSpace()] [string] $VsCodePowershellProfile = "$env:USERPROFILE\Documents\PowerShell\Microsoft.VSCode_profile.ps1",
        [ValidateNotNullOrWhiteSpace()] [string] $WindowsPowershellProfile = "$env:USERPROFILE\Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1",
        [ValidateNotNullOrWhiteSpace()] [string] $VsCodeWindowsPowershellProfile = "$env:USERPROFILE\Documents\PowerShell\Microsoft.VSCode_profile.ps1",
        [ValidateNotNullOrWhiteSpace()] [string] $LinuxShellProfile = "$env:HOME/.config/powershell/Microsoft.Powershell_profile.ps1",
        [switch] $Append
    )

    $content = @'
$MyPoShClonePath = "$MyPoShClonePath$"

if ($IsLinux -or $IsMac) { $env:USERPROFILE = $env:HOME }

function Clean-DevPoShModuleCache {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]
    [CmdletBinding()]
    param([string] $Acme, [string] $Module)
    $expression = "remove-item -recurse -force $env:USERPROFILE/.posh.posh.compilations/$Acme/$Module"
    write-debug "Clean-PoShModuleCache > Cleaning $acme $module"
    Invoke-Expression $expression
}

function Import-DevPoShModule {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]
    [CmdletBinding()]
    param([switch] $Clean, [switch] $Force, [string] $Acme, [string] $Module)
    if ($Clean) { Clean-DevPoshModuleCache }
    Get-ChildItem -Filter "*.psd1" -Recurse "$MyPoShClonePath/$Acme/$Module" | ForEach-Object { write-debug "Import-PoShModule > $($_.Name) >  Importing"; Import-Module -DisableNameChecking $_.FullName -Force:$Force.IsPresent }
}

Import-DevPoShModule

'@.Replace('$MyPoShClonePath$', $MyPoShPath)

    $profilesToEdit = if ($IsWindows) {
        @($PowershellProfile, $VsCodePowershellProfile, $WindowsPowershellProfile, $VsCodeWindowsPowershellProfile) 
    }
    else {
        @($LinuxShellProfile)
    }

    foreach($profileToEdit in $profilesToEdit) {
        Write-Host $profileToEdit
        $dir = Split-Path $profileToEdit -Parent
        if (!(test-path $dir)) {
            New-Item $dir -ItemType Directory -Force
        }

        $content | Out-File $profileToEdit -Append:$Append.IsPresent -Encoding ascii -Force

        get-content $profileToEdit
    }
}
