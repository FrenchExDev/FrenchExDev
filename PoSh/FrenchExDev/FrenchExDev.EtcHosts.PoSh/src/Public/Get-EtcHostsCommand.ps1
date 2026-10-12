function Get-EtcHostsCommand {
    [CmdletBinding()]
    param(
        [EtcHostsAction] $Action
    )

    switch([EtcHostsAction]$Action) {
        ([EtcHostsAction]::Add) {
            'Get-EtcHosts Add -Hostname ''#HostName#'' -Ip ''#Ip#'''
        }
        ([EtcHostsAction]::Remove) {
            'Get-EtcHosts Remove -HostName ''#HostName#'' -Ip ''#Ip#'''
        }
    }
}
