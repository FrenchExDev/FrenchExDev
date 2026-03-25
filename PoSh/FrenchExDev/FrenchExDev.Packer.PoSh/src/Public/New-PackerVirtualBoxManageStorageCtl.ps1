function New-PackerVirtualBoxManageStorageCtl {
    param(
        [string] $Name,
        [string] $Key,
        [string] $Value
    )
    @(
        "storagectl",
        "{{ .Name }}",
        "--name=$name",
        "--$Key",
        "$Value"
    )
}
