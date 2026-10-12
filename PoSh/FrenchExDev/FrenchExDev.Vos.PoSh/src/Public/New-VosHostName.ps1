function New-VosHostName {
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $HostName,
        [object] $Network
    )
    @{
        name       = $HostName
        networking = @(
            @{
                name = "eth1"
                kind = "private"
                ip   = $($Network.GetHostInterfaceAddress($HostName, "eth1", "ip"))
                mac  = $($Network.GetHostInterfaceAddress($HostName, "eth1", "mac"))
            },
            @{
                name = "eth2"
                kind = "public"
                ip   = $($Network.GetHostInterfaceAddress($HostName, "eth2", "ip"))
                mac  = $($Network.GetHostInterfaceAddress($HostName, "eth2", "mac"))
            }
        )
    }
}
