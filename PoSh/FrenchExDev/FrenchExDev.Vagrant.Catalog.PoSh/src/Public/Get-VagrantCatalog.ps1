function Get-VagrantCatalog {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Name
    )

    Get-Content "${name}.json" -Raw | ConvertFrom-Json
}