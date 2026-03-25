function New-PackerVirtualBoxManageStorageAttach {
    param(
        [string] $StorageCtl,
        [int] $Port,
        [string] $Key,
        [string] $Value
    )
    @(
        "storageattach",
        "{{ .Name }}",
        "--storagectl=$StorageCtl",
        "--port=$Port",
        "--$Key",
        "$Value"
    )
}
