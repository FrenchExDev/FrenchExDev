function Write-Packer {
    [CmdletBinding()]
    param(
        [string] $File = "packer.json",
        [scriptblock] $Packer,
        [int] $Depth = 100
    )

    Invoke-Command $Packer | ConvertTo-Json -Depth $Depth | Out-File $File -Encoding ascii -NoNewline
}
