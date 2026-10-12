function Write-VosConfig {
    [CmdletBinding()]
    param(
        [parameter(Position = 0)] [string] $VosConfigFile = $VosConfigSymbols.GlobalConfigFile,
        [scriptblock] $Configuration,
        [int] $Depth = 100
    )

    $configObject = Invoke-Command $Configuration
    $configObject | ConvertTo-Yaml | out-file $VosConfigFile -Encoding ascii | Out-Null
}
