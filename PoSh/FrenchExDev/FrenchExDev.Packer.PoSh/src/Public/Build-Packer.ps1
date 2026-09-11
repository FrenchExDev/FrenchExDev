function Build-Packer {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrEmpty()] [string] $Name,
        [switch] $Force,
        [ValidateNotNull()] [hashtable] $Var
    )

    $forceArg = if ($Force) { "-force" }
    $packerCommand = @("packer", "build", $forceArg)

    foreach ($currentVar in $Var.GetEnumerator()) {
        $packerCommand += @("-var '$($currentVar.Key)=$($currentVar.Value)'")
    }

    $packerCommand += @("$Name.json")

    $packerCommandStr = $($packerCommand -join " ")

    Write-Debug "Build-Packer > Packer Command : '$packerCommandStr'"
    Invoke-Expression $packerCommandStr
}
