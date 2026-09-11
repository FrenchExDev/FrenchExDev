function New-VosMachine {
    [CmdletBinding()]
    param(
        [string] $BoxName,
        [string] $BoxVersion,
        [double] $Memory,
        [double] $VideoMemory,
        [switch] $Video3D,
        [int] $Cpus,
        [object[]] $Instances
    )

    @{
        box_name    = $BoxName
        box_version = $BoxVersion
        ram_mb      = [int] $($Memory / 1MB)
        video_mb    = [int] $($VideoMemory / 1MB)
        video_3d    = [bool] $Video3D
        vcpus       = $Cpus
        instances   = $instances
    }
}
