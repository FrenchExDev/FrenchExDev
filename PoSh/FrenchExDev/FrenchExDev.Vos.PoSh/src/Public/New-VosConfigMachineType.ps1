function New-VosConfigMachineType {
    [CmdletBinding()]
    param(
        [double] $VideoMemory,
        [double] $Ram,
        [int] $Vcpus,
        [switch] $IsPrimary,
        [string] $BoxName,
        [string] $BoxVersion,
        [string] $BoxUrl,
        [ValidateNotNullOrWhiteSpace()] [string] $OsType,
        [switch] $Enable3d,
        [switch] $Gui,
        [string] $Provider,
        [ValidateNotNullOrWhiteSpace()] [string] $ProvisioningPath,
        [scriptblock] $Commands,
        [scriptblock] $Variables,
        [scriptblock] $SharedFolders,
        [scriptblock] $Provisioning,
        [scriptblock] $Files
    )

    $config = [pscustomobject] @{
        base = [pscustomobject] @{
            is_primary        = [bool] $($IsPrimary)
            box_name          = $BoxName
            box_version       = if (![string]::IsNullOrEmpty($BoxVersion)) { $BoxVersion }
            os_type           = $OsType
            enabled_3D        = [bool] $($Enable3d)
            vram_mb           = [int] $($VideoMemory / 1MB)
            ram_mb            = [int] $($Ram / 1MB)
            vcpus             = $Vcpus
            gui               = [bool] $($Gui)
            files             = if ($null -ne $Files) { Invoke-Command $Files }
            provisioning      = if ($null -ne $Provisioning) { Invoke-Command $Provisioning }
            provider          = $Provider
            commands          = if ($null -ne $Commands) { Invoke-Command $Commands }
            variables         = if ($null -ne $Variables) { Invoke-Command $Variables }
            shared_folders    = if ($null -ne $SharedFolders) { Invoke-Command $SharedFolders }
            provisioning_path = "$ProvisioningPath/$BoxVersion"
        }
    }

    $config
}
