function New-VagrantCatalog {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Name,
        [ValidateNotNullOrWhiteSpace()] [string] $Description,
        [scriptblock] $Versions
    )

    $NewVagrantCatalog = @{
        name        = $Name
        description = $Description
        versions    = if ($null -ne $Versions) { @(Invoke-Command $Versions) }
    }

    $NewVagrantCatalog | ConvertTo-Json -Depth 10 | Out-File "${name}.json"

    $NewVagrantCatalog
}
