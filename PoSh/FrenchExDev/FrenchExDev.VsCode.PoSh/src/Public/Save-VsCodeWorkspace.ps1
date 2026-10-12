function Save-VsCodeWorkspace {
    param(
        [parameter(Mandatory = $true, Position = 0)] [object] $VsCodeWorkspace,
        [parameter(Mandatory = $true, Position = 1)] [string] $Name
    )

    $VsCodeWorkspace | ConvertTo-Json | Out-File "$Name.code-workspace" -Encoding ascii
}
