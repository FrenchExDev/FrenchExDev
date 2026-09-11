enum DotnetSolutionAction {
    Bump
    Build
    Clean
    Pack
    Publish
    Restore
    Test
}

function Get-DotnetSolution {
    [CmdletBinding()]
    param(
        [DotnetSolutionAction[]] $Action,
        [string] $Configuration = "Release",
        [string] $PublishPath = ".\bin\Release\",
        [ValidateSet("major", "minor", "patch")] [string] $Part,
        [switch] $SpecificVersion
    )

    foreach ($currentAction in $Action) {
        Write-Verbose "Executing action: $currentAction"

        switch ($currentAction) {
            ([DotnetSolutionAction]::Bump) {
                Bump-DotnetSolutionVersion -part:$part -SpecificVersion:$SpecificVersion
            }
            ([DotnetSolutionAction]::Build) {
                dotnet build --configuration $Configuration
            }
            ([DotnetSolutionAction]::Clean) {
                dotnet clean
            }
            ([DotnetSolutionAction]::Pack) {
                dotnet pack --configuration $Configuration
            }
            ([DotnetSolutionAction]::Publish) {
                $projects = List-DotnetProject -Recurse
                Push-Location src
                foreach ($project in $projects.src) {
                    Push-Location $project.DirectoryName
                    Get-DotnetProject Publish -Configuration $Configuration -PublishPath $PublishPath -Verbose
                    Pop-Location
                }
                Pop-Location
            }
            ([DotnetSolutionAction]::Restore) {
                dotnet restore
            }
            ([DotnetSolutionAction]::Test) {
                dotnet test
            }
            default {
                Write-Warning "Unknown action: $currentAction"
            }
        }
    }
}