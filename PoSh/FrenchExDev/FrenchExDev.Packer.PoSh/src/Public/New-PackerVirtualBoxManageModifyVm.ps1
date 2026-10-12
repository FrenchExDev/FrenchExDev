function New-PackerVirtualBoxManageModifyVm {
    param(
        [string] $Key,
        [string] $Value
    )
    @(
        "modifyvm",
        "{{ .Name }}",
        "--$Key",
        "$Value"
    )
}
