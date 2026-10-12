enum VagrantBoxAction {
    Remove
    Add
}
function Get-VagrantBox {
    [CmdletBinding()]
    param(
        [VagrantBoxAction[]] $Action,
        [string] $Architecture,
        [string] $Provider,
        [string] $BoxVersion,
        [string] $BoxName,
        [string] $BoxFileName,
        [switch] $Force,
        [switch] $Clean
    )

    $forceArg = if ($Force) { "--force" }

    foreach ($currentAction in $Action) {
        switch ([VagrantBoxAction]$currentAction) {
            ([VagrantBoxAction]::Remove) {
                $expression = "vagrant box remove $forceArg --architecture $Architecture --provider $Provider ""$BoxName"""
                Write-Debug "Get-VagrantBox >  Remove > Command: '$expression'"
                Invoke-Expression $expression
            }
            ([VagrantBoxAction]::Add) {
                $cmd = @("vagrant", "box", "add", $BoxFileName)

                if (![string]::IsNullOrEmpty($BoxName)) {
                    $cmd += @($BoxName)
                }

                if (![string]::IsNullOrEmpty($Architecture)) {
                    $cmd += @("--architecture", $Architecture)
                }

                if(![string]::IsNullOrEmpty($BoxVersion)) {
                    $cmd += @("--box-version", "$BoxVersion")
                }

                if ($Clean) {
                    $cmd += @("--clean")
                }

                if ($force) {
                    $cmd += @("--force")
                }

                if (![string]::IsNullOrEmpty($Provider)) {
                    $cmd += @("--provider", $Provider)
                }

                $expression = $cmd -join " "
                Write-Debug "Get-VagrantBox >  Add > Command: '$expression'"
                Invoke-Expression $expression
            }
            default {
                throw "Get-VagrantBox > Action '$currentAction' is not yet implemented."
            }
        }
    }
}
