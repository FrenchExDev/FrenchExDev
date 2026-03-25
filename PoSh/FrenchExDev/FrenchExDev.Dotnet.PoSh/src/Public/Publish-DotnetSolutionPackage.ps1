function Publish-DotnetSolutionPackage {
    [CmdletBinding()]
    param(

    )

    foreach($project in $(List-DotnetProject).Src) {
        Push-Location "src\$($project.DirectoryName)"
        Publish-DotnetProjectPackage
        Pop-Location
    }
}