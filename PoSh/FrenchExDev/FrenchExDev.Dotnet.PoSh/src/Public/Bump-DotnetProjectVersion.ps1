function Bump-DotnetProjectVersion {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $true, position = 0, ValueFromPipelineByPropertyName = $true)] [string] $Project,
        [parameter(Mandatory = $false, position = 1, ValueFromPipelineByPropertyName = $true)] [ValidateSet($null, "", "major", "minor", "patch", "premajor", "preminor", "prepatch", "prerelease")] [string] $Part,
        [parameter(Mandatory = $false, ValueFromPipelineByPropertyName = $true)][string] $SpecificVersion		
    )
    
    if (![string]::IsNullOrEmpty($Part)) {
        dotnet version --project-file src\$Project\$Project.csproj -s $Part --skip-vcs
    }
    elseif (![string]::IsNullOrEmpty($SpecificVersion)) {
        dotnet version --project-file src\$Project\$Project.csproj --skip-vcs $SpecificVersion
    }
}
