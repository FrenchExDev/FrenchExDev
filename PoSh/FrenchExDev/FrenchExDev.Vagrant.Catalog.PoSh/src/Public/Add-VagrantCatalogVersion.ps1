function Add-VagrantCatalogVersion {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $BoxVersion,
        [ValidateNotNullOrWhiteSpace()] [string] $CatalogName,
        [ValidateNotNullOrWhiteSpace()] [string] $Provider,
        [ValidateNotNullOrWhiteSpace()] [string] $BoxFilePath,
        [ValidateNotNullOrWhiteSpace()] [string] $Architecture,
        [scriptblock] $NewVagrantCatalog
    )

    $existingCatalog = $null

    if (test-path "$CatalogName.json") {
        $existingCatalog = Get-VagrantCatalog -Name $CatalogName
    }    

    if ($null -eq $existingCatalog) {
        $existingCatalog = Invoke-Command $NewVagrantCatalog
    }

    $NewVagrantCatalogVersionProviderConfig = @{
        Name         = "$Provider"
        Url          = "$BoxFilePath"
        ChecksumType = "sha1"
        Checksum     = $(Get-FileHash "$($BoxFilePath.Replace("file://", ''))" -Algorithm SHA1).Hash
        Architecture = "$Architecture"
    }

    $versions =     [System.Collections.ArrayList]$existingCatalog.versions

    $existingVersion = $versions | Where-Object { $_.Version -eq $BoxVersion }

    if ($null -ne $existingVersion) {
        $versions.Remove($existingVersion)
    }

    $existingCatalog.versions = $versions

    $NewVersionConfig = @{
        Version   = "$BoxVersion"
        Providers = {
            $NewProviderVersion = New-VagrantCatalogVersionProvider @NewVagrantCatalogVersionProviderConfig

            @($NewProviderVersion)
        }.GetNewClosure()
    }

    $NewVersion = New-VagrantCatalogVersion @NewVersionConfig

    $existingCatalog.versions += @($NewVersion)

    $existingCatalog | ConvertTo-Json -Depth 10 | Out-File "${CatalogName}.json"

}