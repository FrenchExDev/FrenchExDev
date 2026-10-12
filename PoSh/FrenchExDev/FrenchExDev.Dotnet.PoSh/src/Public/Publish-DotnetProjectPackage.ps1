function Publish-DotnetProjectPackage {
    [CmdletBinding()]
    param(
        [string] $DestinationPath
    )

    Write-Verbose "Publishing .NET project package..."
    $Config = Get-Content ./../../.projects.yaml | ConvertFrom-Yaml

    $DestinationPath = $Config.publishTargetPath

    dotnet pack --configuration Release
    
    if (-not (Test-Path -Path $DestinationPath)) {
        New-Item -ItemType Directory -Path $DestinationPath | Out-Null
    }
    
    copy-item -Path ".\bin\Release\*.nupkg" -Destination $DestinationPath -Force

    Write-Information ".NET project package published successfully to '$DestinationPath'."

}
