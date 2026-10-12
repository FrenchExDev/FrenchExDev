function New-Packer {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Description,
        [scriptblock] $BuildersScript,
        [scriptblock] $ProvisionersScript,
        [scriptblock] $PostProcessorsScript,
        [scriptblock] $VariablesScript,
        [scriptblock] $VagrantFilesScript,
        [scriptblock] $HttpFilesScript,
        [scriptblock] $ProvisioningFilesScript,
        [string] $WorkingDirectory,
        [string] $PackerFile
    )

    $WorkingDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($WorkingDirectory)

    $packerObject = [pscustomobject] @{
        description = $Description
    }

    if ($null -ne $BuildersScript) {
        $packerObject | Add-Member -MemberType NoteProperty -Name "builders" -Value $(Invoke-Command $BuildersScript)
    }

    if ($null -ne $ProvisionersScript) {
        $packerObject | Add-Member -MemberType NoteProperty -Name "provisioners" -Value $(Invoke-Command $ProvisionersScript)
    }

    if ($null -ne $PostProcessorsScript) {
        $packerObject | Add-Member -MemberType NoteProperty -Name "post-processors" -Value $(Invoke-Command $PostProcessorsScript)
    }

    if ($null -ne $VariablesScript) {
        $packerObject | Add-Member -MemberType NoteProperty -Name "variables" -Value $(Invoke-Command $VariablesScript)
    }

    if (!(Test-Path $WorkingDirectory)) {
        New-Item $WorkingDirectory -ItemType Directory | Out-Null
    }

    if ($null -ne $VagrantFilesScript) {
        New-PackerFile -WorkingDirectory $WorkingDirectory -Kind "vagrant" -Files $VagrantFilesScript | Out-Null
    }

    if ($null -ne $HttpFilesScript) {
        New-PackerFile -WorkingDirectory $WorkingDirectory -Kind "http" -Files $HttpFilesScript | Out-Null
    }

    if ($null -ne $ProvisioningFilesScript) {
        New-PackerFile -WorkingDirectory $WorkingDirectory -Kind "scripts" -Files $ProvisioningFilesScript | Out-Null
    }

    if (![string]::IsNullOrEmpty($PackerFile)) {
        Write-Packer -File "$WorkingDirectory/$PackerFile" -Packer { $packerObject } -Depth 100  | Out-Null
        return
    }

    $packerObject
}
