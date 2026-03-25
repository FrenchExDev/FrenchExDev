function Get-VsCodeWorkspace {
    param(
        [parameter(mandatory=$true, position=0)] [string] $Name
    )

    Get-Content "$name.code-workspace" | ConvertFrom-Json
}
