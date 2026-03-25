function Get-DotnetProjectConfiguration {
    [cmdletbinding()]
    param(
        [string] $ProjectName
    )

    $config = get-content .projects.yaml | ConvertFrom-Yaml

    $config.publish[$ProjectName].targetPath
}