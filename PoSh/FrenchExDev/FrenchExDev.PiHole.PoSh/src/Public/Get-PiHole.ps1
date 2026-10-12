enum PiHoleAction {
    Upgrade
    Reboot
    Ssh
}

function Get-PiHole {
    [CmdletBinding()]
    param(
        [PiHoleAction[]] $Action,
        [switch] $WaitAvailable,
        [string] $SshHostName = "pihole",
        [string] $RebootCommand = "doas reboot",
        [string] $UpgradeCommand = "doas apk update; doas apk upgrade;"
    )

    foreach ($currentAction in $Action) {
        switch ([PiHoleAction] $currentAction) {
            ([PiHoleAction]::Ssh) {
                ssh $SshHostName
            }
            ([PiHoleAction]::Upgrade) {
                ssh $SshHostName -C "$UpgradeCommand"
            }
            ([PiHoleAction]::Reboot) {
                ssh $SshHostName -C "$RebootCommand"

                if ($WaitAvailable) {
                    Start-Sleep -Seconds 2
                    do {
                        ssh $SshHostName -C "echo 'PiHole is available'"
                    } while ($LASTEXITCODE -ne 0)
                }
            }
            default {
                throw "Get-PiHole > Action '$currentAction' is not yet implemented."
            }
        }
    }
}
