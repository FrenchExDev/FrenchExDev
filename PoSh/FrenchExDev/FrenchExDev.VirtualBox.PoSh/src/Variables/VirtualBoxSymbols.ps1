$VirtualBoxSymbols = @{
    Architectures           = @{
        amd64 = "amd64"
    }
    Values                  = @{
        On  = "on"
        Off = "off"
    }
    NicPromisc              = @{
        AllowAll = "allow-all"
    }
    ModifyVm                = @{
        Accelerate3d          = "accelerate3d"
        CpuExecutionCap       = "cpuexecutioncap"
        Memory                = "memory"
        Cpus                  = "cpus"
        NicPromisc            = "nicpromisc"
        NicType               = "nic-type"
        NatLocalhostReachable = "nat-localhostreachable"
        NatDnsHostResolver    = "natdnshostresolver"
        NatDnsProxy           = "natdnsproxy"
        VRam                  = "vram"
        BiosApic              = "biosapic"
        IoApic                = "ioapic"
        X2Apic                = "x2apic"
        HwVirtex              = "hwvirtex"
        HPet                  = "hpet"
        LargePages            = "largepages"
        VtxUx                 = "vtxux"
        Pae                   = "pae"
        Acpi                  = "acpi"
        PageFusion            = "pagefusion"
        Chipset               = "chipset"
        Vrde                  = "vrde"
        Usb                   = "usb"
        NestedHwVirt          = "nested-hw-virt"
        NestedPaging          = "nested-paging"
        OsType                = "ostype"
        GraphicsController    = "graphicscontroller"
        ParaVirtProvider      = "paravirtprovider"
        VtxVPid               = "vtx-vpid"
        VirtVmSaveVmLoad      = "virt-vmsave-vmload"
        VmProcessPriority     = "vm-process-priority"
    }
    Properties              = @{
        HwVirtExclusive = "hwvirtexclusive"
    }
    Storage                 = @{
        StorageCtl     = "storagectl"
        NMVEController = "NVME Controller"
        SATAController = "SATA Controller"
        NonRotational  = "nonrotational"
        Discard        = "discard"
        Sata           = "sata"
        HostIoCache    = "hostiocache"
    }
    ParaVirtProvider        = @{
        None    = "none"
        Default = "default"
        Legacy  = "legacy"
        Minimal = "minimal"
        HyperV  = "hyperv"
        Kvm     = "kvm"
    }
    Chipsets                = @{
        Ich9  = "ich9"
        Piix3 = "piix3"
    }
    OsTypes                 = @{
        Linux64 = "Linux_64"
    }
    Graphics                = @{
        None     = "none"
        VBoxVga  = "vboxvga"
        VBoxSvga = "vboxsvga"
        VmSvga   = "vmsvga"
    }
    Networking              = @{
        NicKind = @{
            None        = "none"
            Null        = "null"
            Nat         = "nat"
            Bridged     = "bridged"
            Intnet      = "intnet"
            HostOnly    = "hostonly"
            HostOnlyNet = "hostonlynet"
            Generic     = "generic"
            NatNetwork  = "natnetwork"
            Cloud       = "cloud"
        }
        NicType = @{
            Am79C970A = "Am79C970A"
            Am79C973  = "Am79C973"
            "82540EM" = "82540EM"
            "82543GC" = "82543GC"
            "82545EM" = "82545EM"
            virtio    = "virtio"
        }
    }
    VmProcessPriorityValues = @{
        Default = "default"
        Flat    = "flat"
        Low     = "low"
        Normal  = "normal"
        High    = "high"
    }
}
