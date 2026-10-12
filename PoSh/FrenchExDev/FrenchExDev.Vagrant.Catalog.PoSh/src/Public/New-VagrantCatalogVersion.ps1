function New-VagrantCatalogVersion {
    [CmdletBinding()]
    param(
        [string] $Version,
        [scriptblock] $Providers
    )

    @{
        version   = "$Version"
        providers = @(Invoke-Command $Providers)
    }
}
