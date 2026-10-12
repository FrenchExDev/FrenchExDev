function New-PackerVirtualBoxIsoBuilder {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $BootWait = "{{ user ``boot_wait`` }}",
        [ValidateNotNullOrWhiteSpace()] [string] $DiskSize = "{{ user ``disk_size`` }}",
        [ValidateNotNullOrWhiteSpace()] [string] $Communicator = "ssh",
        [ValidateNotNullOrWhiteSpace()] [string] $Format = "ova",
        [ValidateNotNullOrWhiteSpace()] [string] $HttpDirectory = "http",
        [ValidateNotNullOrWhiteSpace()] [string] $IsoChecksum = "{{ user ``iso_checksum_type``}}:{{ user ``iso_checksum`` }}",
        [ValidateNotNullOrWhiteSpace()] [string] $SshPasswd = "{{ user ``root_password`` }}",
        [ValidateNotNullOrWhiteSpace()] [string] $SshTimeout = "{{ user ``ssh_timeout`` }}",
        [ValidateNotNullOrWhiteSpace()] [string] $SshUserName = "root",
        [ValidateNotNullOrWhiteSpace()] [string] $GuestOsType = "Linux_64",
        [ValidateNotNullOrWhiteSpace()] [string] $VmName = "{{ user ``vm_name`` }}-v{{ user ``box_version`` }}_d{{ user ``date`` }}",
        [ValidateNotNullOrWhiteSpace()] [string] $GuestAdditionsUrl = "https://download.virtualbox.org/virtualbox/{{ user ``vbox_version`` }}/VBoxGuestAdditions_{{ user ``vbox_version`` }}.iso",
        [ValidateNotNullOrWhiteSpace()] [string] $GuestAdditionsSha256 = "{{ user ``vbox_guest_additions_iso_sha256`` }}",
        [ValidateNotNullOrWhiteSpace()] [string] $GuestAdditionsPath = "/root/VBoxGuestAdditions.iso",
        [ValidateNotNullOrWhiteSpace()] [string] $VirtualBoxVersionFile = "VBoxVersion.txt",
        [ValidateNotNullOrWhiteSpace()] [string] $GuestAdditionsMode = "upload",
        [ValidateNotNullOrWhiteSpace()] [string] $ShutdownCommand = "/sbin/poweroff",
        [ValidateNotNullOrWhiteSpace()] [string] $HardDriveInterface = "sata",
        [ValidateNotNullOrWhiteSpace()] [string[]] $IsoUrls = @("{{ user ``iso_local_url`` }}", "{{ user ``iso_download_url`` }}"),
        [switch] $Headless,
        [switch] $HardDriveDiscard,
        [switch] $HardDriveNonRotational,
        [switch] $NestedVirt,
        [scriptblock] $BootCommandScript,
        [scriptblock] $VBoxManageScript
    )

    @{
        boot_command             = $(Invoke-Command $BootCommandScript)
        boot_wait                = $BootWait
        communicator             = "$Communicator"
        disk_size                = "$DiskSize"
        format                   = "$Format"
        headless                 = [bool] $Headless.IsPresent
        http_directory           = "$HttpDirectory"
        iso_checksum             = "$IsoChecksum"
        iso_urls                 = $IsoUrls
        keep_registered          = $false
        shutdown_command         = "$ShutdownCommand"
        ssh_password             = "$SshPasswd"
        ssh_timeout              = "$SshTimeout"
        ssh_username             = "$SshUserName"
        type                     = "virtualbox-iso"
        guest_os_type            = "$GuestOsType"
        guest_additions_url      = "$GuestAdditionsUrl"
        guest_additions_sha256   = "$GuestAdditionsSha256"
        guest_additions_path     = "$GuestAdditionsPath"
        guest_additions_mode     = "$GuestAdditionsMode"
        virtualbox_version_file  = "$VirtualBoxVersionFile"
        nested_virt              = [bool] $NestedVirt.IsPresent
        vboxmanage               = if ($null -ne $VBoxManageScript) { $(Invoke-Command $VBoxManageScript) }
        hard_drive_interface     = "$HardDriveInterface"
        hard_drive_discard       = [bool] $HardDriveDiscard.IsPresent
        hard_drive_nonrotational = [bool] $HardDriveNonRotational.IsPresent
        vm_name                  = $VmName
    }
}