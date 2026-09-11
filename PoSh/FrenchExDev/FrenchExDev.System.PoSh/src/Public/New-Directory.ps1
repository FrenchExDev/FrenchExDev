function New-Directory {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [parameter(position = 0)] [string] $FullPath,
        [switch] $Delete,
        [scriptblock] $File
    )

    if ($Delete -and $(Test-Path $FullPath)) {
        Remove-Item -Recurse -Force $FullPath | Out-Null
    }

    if (!$(Test-Path $FullPath)) {
        New-Item $FullPath -ItemType Directory | Out-Null
    }
    

    if ($null -ne $File) {
        Invoke-Command $File
    }
}