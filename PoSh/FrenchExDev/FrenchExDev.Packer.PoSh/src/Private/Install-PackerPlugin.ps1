function Install-PackerPlugin {
    param(
        [string[]] $PluginName
    )

    foreach ($plugin in $PluginName) {
        $packerCommand = @("packer", "plugins", "install", $plugin)
        $packerCommandStr = $($packerCommand -join " ")
        Write-Debug "Get-Packer > Install Plugin > Packer Command : '$packerCommandStr'"
        Invoke-Expression $packerCommandStr
    }
}