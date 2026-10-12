[CmdletBinding()]
param(
    [ValidateNotNullOrWhiteSpace()] [string] $NetworkConfig = "home",
    [ValidateNotNullOrWhiteSpace()] [string] $VosInstancePrefix = "fexdev-infra",
    [ValidateNotNullOrWhiteSpace()] [string] $Domain = "frenchexdev.lab",
    [switch] $Force
)

$InternalVerboseDebugConfig = @{
    Verbose = $VerbosePreference
    Debug   = $DebugPreference
}

$NetworkConfigObject = @{
    Configuration    = $NetworkConfig
    ScriptArgs       = "-VmPrefix $VosInstancePrefix"
    WorkingDirectory = $(Resolve-Path ./..)
}

$Network = Get-DevNetwork @NetworkConfigObject @InternalVerboseDebugConfig

$NewMkCertConfigGeneratorConfigs = @()

$kubernetesMachines = Get-VosAlpineKubernetesMachineIpConfig -Network $Network @InternalVerboseDebugConfig

foreach ($kubernetesMachine in $kubernetesMachines.GetEnumerator()) {

    $ssh = @{
        Force     = $Force
        Path      = "./data/certs"
        CertFile  = "$($kubernetesMachine.Hostname).crt"
        KeyFile   = "$($kubernetesMachine.Hostname).key"
        IpAddress =  @(
            $kubernetesMachine.Ip.eth1
            $kubernetesMachine.Ip.eth2
        )
        HostName  = @(
            "$($kubernetesMachine.Hostname)" 
            "$($kubernetesMachine.Fqdn)"
        )
    }

    $NewMkCertConfigGeneratorConfigs += $($ssh)
}

foreach ($NewMkCertConfigGeneratorConfig in $NewMkCertConfigGeneratorConfigs) {
    New-MkCertGenerator @NewMkCertConfigGeneratorConfig @InternalVerboseDebugConfig
}
