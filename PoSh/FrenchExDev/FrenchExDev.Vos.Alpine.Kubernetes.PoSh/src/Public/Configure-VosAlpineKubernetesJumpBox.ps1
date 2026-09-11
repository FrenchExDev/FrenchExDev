enum VosAlpineKubernetesJumpBoxConfigureAction {
    Backup
    SshKeyGen
    CreateNetworkMap
    SetNetworkMap
}

function Configure-VosAlpineKubernetesJumpBox {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]
    [CmdletBinding()]
    param(
        [parameter(Position = 0)] [VosAlpineKubernetesJumpBoxConfigureAction[]] $Action,
        [string] $EtcHostsFile = "/etc/hosts",
        [string] $Start = "## ETCHOSTS JUMPBOX START",
        [string] $Stop = "## ETCHOSTS JUMPBOX STOP",
        [string] $VosNetConfig = "/vagrant/config-etc-hosts",
        [string] $VmPrefix = "fexdev-infra",
        [string] $Domain = "frenchexdev.lab",
        [string] $NetworkConfiguration = "home",
        [string] $NetworkScriptName = "./00-myDevNetwork",
        [string] $NetworkScriptArgs = "-VmPrefx 'fexdev-infra'",
        [string] $NetworkWorkingDirectory,
        [string] $JumpBoxName = "jb",
        [string] $JumpBoxId = "00"
    )

    $DevNetworkConfig = @{
        Configuration    = $NetworkConfiguration
        ScriptName       = $NetworkScriptName
        ScriptArgs       = $NetworkScriptArgs
        WorkingDirectory = $NetworkWorkingDirectory
    }

    $Network = Get-DevNetwork @DevNetworkConfig

    foreach ($currentAction in $Action ) {
        switch ([VosAlpineKubernetesJumpBoxConfigureAction] $currentAction) {
            ([VosAlpineKubernetesJumpBoxConfigureAction]::Backup) {
                if (!(Test-Path $(Split-Path $EtcHostsFile -Parent))) {
                    continue
                }
                if (!(Test-Path "${EtcHostsFile}.backup")) {
                    Copy-Item $EtcHostsFile "${EtcHostsFile}.backup"
                }
            }
            ([VosAlpineKubernetesJumpBoxConfigureAction]::SshKeyGen) {
                Get-VosMachine $JumpBoxName $JumpBoxId SshCommand -Command "[[ -f ~/.ssh/id_rsa ]] || ssh-keygen -t rsa -f ~/.ssh/id_rsa -N ''"
            }
            ([VosAlpineKubernetesJumpBoxConfigureAction]::CreateNetworkMap) {
                Get-VosAlpineKubernetesNetworkMap Create -Network $Network
            }
            ([VosAlpineKubernetesJumpBoxConfigureAction]::SetNetworkMap) {
                Get-VosAlpineKubernetesNetworkMap Set -Network $Network
            }
            ([VosAlpineKubernetesJumpBoxConfigureAction]::Configure) {
                Get-VosMachine jb 00 SshCommand -Command "sudo pwsh -L -C 'Configure-VosAlpineKubernetesJumpBox'"
            }
            default {
                throw "Configure-VosAlpineKubernetesJumpBox > '$currentAction' is not yet implemented."
            }
        }
    }
}
