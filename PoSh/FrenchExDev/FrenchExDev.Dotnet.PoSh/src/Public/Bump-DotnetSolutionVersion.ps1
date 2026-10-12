
function Bump-DotnetSolutionVersion {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $false, position = 1, ValueFromPipelineByPropertyName = $true)] [ValidateSet($null, "", "major", "minor", "patch", "premajor", "preminor", "prepatch", "prerelease")] [string] $Part,
        [parameter(Mandatory = $false, ValueFromPipelineByPropertyName = $true)][string] $SpecificVersion		
    )
    
    foreach ($project in $(List-DotnetProject).Src) {
        Bump-DotnetProjectVersion -Project $($project.DirectoryName) -Part $Part -SpecificVersion $SpecificVersion
    }
}
