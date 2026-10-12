function Write-VosConfigLocal {
    [CmdletBinding()]
    param(
        [parameter(Position = 0)] [string] $VosConfigFile = $VosConfigSymbols.LocalConfigFile,
        [scriptblock] $Configuration,
        [int] $Depth = 10
    )

    if (!(test-path $(Split-Path $VosConfigFile -parent))) {
        mkdir $(Split-Path $VosConfigFile -parent) | Out-Null
    }

    $ConfigurationObject = Invoke-Command $Configuration

    if ($null -ne $ConfigurationObject) {
        $ConfigurationObject | ConvertTo-Yaml | Out-File $VosConfigFile -Encoding ascii -force | Out-Null
    }
}
