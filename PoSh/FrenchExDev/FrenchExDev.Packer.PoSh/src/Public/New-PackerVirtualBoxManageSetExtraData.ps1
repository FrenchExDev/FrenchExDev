function New-PackerVirtualBoxManageSetExtraData {
    param(
        [string] $Key,
        [string] $Value
    )
    @(
        "setextradata",
        "{{ .Name }}",
        "$Key",
        "$Value"
    )
}
