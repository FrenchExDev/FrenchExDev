enum VagrantAction {
    Halt
    Up
    SshConfig
    Ssh
    SshCommand
    SnapshotPush
    SnapshotPop
    SnapshotSave
    SnapsotRestore
    Destroy
    Reload
    Provision
    Powershell
    PowershellCommand
    Status
}

function Get-Vagrant {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $true, Position = 0)] [VagrantAction[]] $Action,
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $false, Position = 1)] [string] $MachineName,
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $false)] [string] $Command,
        [ValidateNotNullOrWhiteSpace()] [parameter(Mandatory = $false)] [string] $Snapshot,
        [string[]] $ProvisionWith,
        [switch] $NoProvision,
        [switch] $Force,
        [switch] $Elevated,
        [switch] $NoColor,
        [switch] $DebugVagrant
    )

    $ForceArg = if ($Force) { "--force" }
    $DebugArg = if ($DebugVagrant) { "--debug" }
    $NoProvisionArg = if ($NoProvision) { " --no-provision " }

    foreach ($currentAction in $Action) {
        switch ([VagrantAction]$currentAction) {
            ([VagrantAction]::Provision) {
                $ProvisionWithArg = if ([string[]] $ProvisionWith -gt 0) { 
                    "--provision-with $(join-string -Separator "," -InputObject $ProvisionWith)"
                }
                $expression = "vagrant provision $MachineName $ProvisionWithArg $ForceArg $DebugArg"
                if ($ProvisionWith -gt 0) {
                    Write-Verbose "Get-Vagrant > $MachineName > Provision > Command: '$expression'" 
                }
                else {
                    Write-Verbose "Get-Vagrant > $MachineName > Provision All > Command: '$expression'" 
                }
                Invoke-Expression $expression
            }
            ([VagrantAction]::SnapshotPush) {
                $expression = "vagrant snapshot push $MachineName"
                Write-Verbose "Get-Vagrant > $MachineName > Snapshot > Push > Command: '$expression'"
                Invoke-Expression $expression
            }
            ([VagrantAction]::SnapshotPop) {
                $expression = "vagrant snapshot pop $MachineName"
                Write-Verbose "Get-Vagrant > $MachineName > Snapshot > Pop > Command: '$expression'"
                Invoke-Expression $expression
            }
            ([VagrantAction]::SnapshotSave) {
                $expression = "vagrant snapshot save $MachineName '$Snapshot'"
                Write-Verbose "Get-Vagrant > $MachineName > Snapshot > Save > Command: '$expression'"
                Invoke-Expression $expression
            }
            ([VagrantAction]::SnapshotRestore) {
                $expression = "vagrant snapshot restore $MachineName '$Snapshot'"
                Write-Verbose "Get-Vagrant > $MachineName > Snapshot > Restore > Command: '$expression'"
                Invoke-Expression $expression
            }
            ([VagrantAction]::Status) {
                $expression = "vagrant status"
                Write-Verbose "Get-Vagrant > $MachineName > Status > Command: '$expression'"
                Invoke-Expression $expression
            }
            ([VagrantAction]::Halt) {
                $expression = "vagrant halt $ForceArg $DebugArg $MachineName"
                Write-Verbose "Get-Vagrant > $MachineName > Halt > Command: '$expression'"
                Invoke-Expression $expression
            }
            ([VagrantAction]::Up) {
                $expression = "vagrant up $DebugArg $NoProvisionArg $MachineName"
                Write-Verbose "Get-Vagrant > $MachineName > Up > Command: '$expression'"
                Invoke-Expression $expression
            }
            ([VagrantAction]::SshConfig) {
                $expression = "vagrant ssh-config $MachineName"
                Write-Verbose "Get-Vagrant > $MachineName > SshConfig > Command: '$expression'"
                Invoke-Expression $expression
            }
            ([VagrantAction]::Ssh) {
                $expression = "vagrant ssh $MachineName"
                Write-Verbose "Get-Vagrant > $MachineName > Ssh > Command: '$expression'"
                Invoke-Expression $expression
            }
            ([VagrantAction]::SshCommand) {
                $noTtyArg = if ($NoTty) { "--no-tty" }
                $noColorArg = if ($NoColor) { "--no-color" }
                $expression = "vagrant ssh $MachineName $noTtyArg $noColorArg --comand ""$Command"""
                Write-Verbose "Get-Vagrant > $MachineName > Ssh > Command: '$expression'"
                Invoke-Expression $expression
            }
            ([VagrantAction]::Powershell) {
                $expression = "vagrant powershell $MachineName"
                Write-Verbose "Get-Vagrant > $MachineName > Powershell > Command: '$expression'"
                Invoke-Expression $expression
            }
            ([VagrantAction]::PowershellCommand) {
                $elevatedArg = if ($Elevated) { "--elevated" }
                $colorArg = if (!$NoColor) { "--color" }
                $expression = "vagrant powershell $MachineName $elevatedArg $colorArg --command ""$Command"""
                Write-Verbose "Get-Vagrant > $MachineName > Powershell > Command: '$expression'"
                Invoke-Expression $expression
            }
            ([VagrantAction]::Destroy) {
                $expression = "vagrant destroy $ForceArg $MachineName"
                Write-Verbose "Get-Vagrant > $MachineName > Destroy > Command: '$expression'"
                Invoke-Expression $expression
            }
            ([VagrantAction]::Reload) {
                $expression = "vagrant reload $ForceArg $MachineName"
                Write-Verbose "Get-Vagrant > $MachineName > Reload > Command: '$expression'"
                Invoke-Expression $expression
            }
            default {
                throw "Get-Vagrant > Action '$currentAction' is not yet implemented."
            }
        }
    }
}
