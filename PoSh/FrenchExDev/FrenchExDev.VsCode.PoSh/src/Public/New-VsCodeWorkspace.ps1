function New-VsCodeWorkspace {
    [CmdletBinding()]
    param(
        [parameter(mandatory = $true, position = 0)] [string] $Name,
        [switch] $NoSave
    )

    $configuration = [pscustomobject] @{
        folders = @(
            @{
                path = "."
            }
        )
    }

    if (!$NoSave) {
        $configuration | ConvertTo-Json | Out-File "$name.code-workspace" -Encoding ascii
    }
}
