function New-VagrantCatalogVersionProvider {
    [CmdletBinding()]
    param(
        [string] $Name,
        [string] $Url,
        [string] $ChecksumType,
        [string] $Checksum,
        [string] $Architecture
    )

    @{
        name = "$Name"
        url = "$Url"
        checksum_type = "$ChecksumType"
        checksum = "$Checksum"
        architecture = "$Architecture"
    }
}
