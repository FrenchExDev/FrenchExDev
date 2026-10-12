function New-File {
    [CmdletBinding()]
    param(
        [string] $Path,
        [string] $Name,
        [string] $Extension,
        [scriptblock] $Content
    )

    New-Directory $Path

    $fullPath = if (![string]::IsNullOrEmpty($path)) {
        "${path}/${name}.${extension}"
    }
    else {
        "${name}.${extension}"
    }

    Invoke-Command $content | out-file $fullPath -Encoding ascii -NoNewline | Out-Null
}
