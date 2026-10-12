function List-DotnetProject {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]
    [CmdletBinding()]
    param(
        [string] $Path = ".\",
        [switch] $Recurse
    )

    Get-Content .projects.yaml | ConvertFrom-Yaml
}