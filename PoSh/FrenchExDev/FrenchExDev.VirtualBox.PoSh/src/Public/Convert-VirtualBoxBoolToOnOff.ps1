function Convert-VirtualBoxBoolToOnOff {
    [CmdletBinding()]
    param(
        [parameter(Mandatory=$true, Position=0)] [bool] $Value
    )

    if ($Value) {
        $VirtualBoxSymbols.Values.On
    }
    else {
        $VirtualBoxSymbols.Values.Off
    }
}
