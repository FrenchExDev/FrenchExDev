enum DotnetProjectAction {
    Build
    Clean
    Pack
    Publish
    Restore
    Test
}

function Get-DotnetProject {
    [CmdletBinding()]
    param(
        [DotnetProjectAction[]] $Action,
        [string] $Configuration = "Release",
        [string] $PublishPath = ".\bin\Release\"
    )

    $Config = Get-Content ./../../.projects.yaml | ConvertFrom-Yaml

    $LocalNugetRegistryPath = $Config.publishTargetPath

    foreach ($currentAction in $Action) {
        Write-Verbose "Executing action: $currentAction"

        switch ($currentAction) {
            ([DotnetProjectAction]::Build) {
                dotnet build --configuration $Configuration
            }
            ([DotnetProjectAction]::Clean) {
                dotnet clean
            }
            ([DotnetProjectAction]::Pack) {
                dotnet pack --configuration $Configuration
            }
            ([DotnetProjectAction]::Publish) {
                Remove-Item -Force "${PublishPath}\*.nupkg" -ErrorAction Stop -Verbose
                dotnet publish --configuration $Configuration -o $PublishPath
                Copy-Item -Path "${PublishPath}\*.nupkg" -Destination $LocalNugetRegistryPath -Force -Verbose
            }
            ([DotnetProjectAction]::Restore) {
                dotnet restore
            }
            ([DotnetProjectAction]::Test) {
                dotnet test
            }
            default {
                Write-Warning "Unknown action: $currentAction"
            }
        }
    }
}