function New-PackerVirtualBoxManageConfig {
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Cpus = "4",
        [ValidateNotNullOrWhiteSpace()] [string] $Memory = "256",
        [ValidateNotNullOrWhiteSpace()] [string] $VideoMemory = "64",
        [ValidateNotNullOrWhiteSpace()] [string] $Chipset = "ich9",
        [ValidateNotNullOrWhiteSpace()] [string] $ParaVirtProvider = "kvm",
        [ValidateNotNullOrWhiteSpace()] [string] $OsType = "Linux_64",
        [ValidateNotNullOrWhiteSpace()] [string] $GraphicsController = "vboxsvga",
        [switch] $IoApic,
        [switch] $HwVirtEx,
        [switch] $HPet,
        [switch] $LargePages,
        [switch] $VtxUx,
        [switch] $VtxVPid,
        [switch] $Pae,
        [switch] $Acpi,
        [switch] $PageFusion,
        [switch] $Vrde,
        [switch] $Usb,
        [switch] $NestedHwVirt,
        [switch] $NestedPaging,
        [switch] $Nat1DnsProxy,
        [switch] $Nat1LocalHostReachable,
        [switch] $Nat1DnsHostResolver,
        [switch] $HwVirtExclusive,
        [switch] $Sata,
        [switch] $SataHostIoCache,
        [switch] $SataNonRotational,
        [switch] $SataDiscard
    )

    $base = @(
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.Memory)" -Value $Memory),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.Cpus)" -Value $Cpus),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.VRam)" -Value $VideoMemory),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.chipset)" -Value $Chipset),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.OsType)" -Value "$OsType"),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.GraphicsController)" -Value "$GraphicsController"),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.ParaVirtProvider)" -Value "$ParaVirtProvider"),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.NatDnsProxy)1" -Value $(Convert-VirtualBoxBoolToOnOff $Nat1DnsProxy)),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.NatLocalhostReachable)1" -Value $(Convert-VirtualBoxBoolToOnOff $Nat1LocalHostReachable)),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.NatDnsHostResolver)1" -Value $(Convert-VirtualBoxBoolToOnOff $Nat1DnsHostResolver)),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.IoApic)" -Value $(Convert-VirtualBoxBoolToOnOff $($IoApic))),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.HwVirtex)" -Value $(Convert-VirtualBoxBoolToOnOff $HwVirtEx)),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.HPet)" -Value $(Convert-VirtualBoxBoolToOnOff $HPet)),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.LargePages)" -Value $(Convert-VirtualBoxBoolToOnOff $LargePages)),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.VtxUx)" -Value $(Convert-VirtualBoxBoolToOnOff $VtxUx)),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.VtxVPid)" -Value $(Convert-VirtualBoxBoolToOnOff $VtxVPid)),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.Pae)" -Value $(Convert-VirtualBoxBoolToOnOff $Pae)),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.Acpi)" -Value $(Convert-VirtualBoxBoolToOnOff $Acpi)),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.PageFusion)" -Value $(Convert-VirtualBoxBoolToOnOff $PageFusion)),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.Vrde)" -Value $(Convert-VirtualBoxBoolToOnOff $Vrde)),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.Usb)" -Value $(Convert-VirtualBoxBoolToOnOff $Usb)),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.NestedHwVirt)" -Value $(Convert-VirtualBoxBoolToOnOff $NestedHwVirt)),
        $(New-PackerVirtualBoxManageModifyVm -Key "$($VirtualBoxSymbols.ModifyVm.NestedPaging)" -Value $(Convert-VirtualBoxBoolToOnOff $NestedPaging)),
        $(New-PackerVirtualBoxManageSetProperty -Key "$($VirtualBoxSymbols.Properties.HwVirtExclusive)" -Value $(Convert-VirtualBoxBoolToOnOff $HwVirtExclusive))
    )

    if ($Sata) {
        $localNonRotationational = if ($SataNonRotational) { 1 } else { 0 }
        $base += @(
            $(New-PackerVirtualBoxManageStorageCtl -Name "$($VirtualBoxSymbols.Storage.SATAController)" -Key "hostiocache" -Value $(Convert-VirtualBoxBoolToOnOff $SataHostIoCache)),
            $(New-PackerVirtualBoxManageStorageAttach -StorageCtl "$($VirtualBoxSymbols.Storage.SATAController)" -Port 0 -Key "$($VirtualBoxSymbols.Storage.NonRotational)" -Value $(Convert-VirtualBoxBoolToOnOff $SataNonRotational)),
            $(New-PackerVirtualBoxManageStorageAttach -StorageCtl "$($VirtualBoxSymbols.Storage.SATAController)" -Port 0 -Key "$($VirtualBoxSymbols.Storage.Discard)" -Value $(Convert-VirtualBoxBoolToOnOff $SataDiscard)),
            $(New-PackerVirtualBoxManageSetExtraData -Key "VBoxInternal/Devices/ahci/0/Config/Port0/NonRotational" -Value $localNonRotationational)
        )
    }

    $base
}
