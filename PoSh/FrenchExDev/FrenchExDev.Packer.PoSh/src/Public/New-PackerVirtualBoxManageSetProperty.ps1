function New-PackerVirtualBoxManageSetProperty {
    param(
        [string] $Key,
        [string] $Value
    )
    @(
        "setproperty",
        "$Key",
        "$Value"
    )
}
