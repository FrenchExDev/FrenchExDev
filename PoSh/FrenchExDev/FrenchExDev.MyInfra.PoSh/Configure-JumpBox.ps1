[CmdletBinding()]
param()

Push-Location src

$NetworkConfig = @{
    NetworkConfiguration = "home"
    NetworkScriptArgs = "-VmPrefix 'fexdev-infra'"
    NetworkWorkingDirectory = $(Resolve-Path ./..).Path
    NetworkScriptName = "./00-myDevNetwork"
}

Configure-VosAlpineKubernetesJumpBox CreateNetworkMap,SshKeyGen,SetNetworkMap @NetworkConfig

Pop-Location
