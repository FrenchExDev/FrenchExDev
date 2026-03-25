$poShRoot = "C:\code\FrenchExDev.MyDev.PoSh\FrenchExDev.MyDev.PoSh_i7\PoSh\FrenchExDev"
$dirs = Get-ChildItem -Path $poShRoot -Directory

foreach ($dir in $dirs) {
    $name = $dir.Name
    $repoName = "FrenchExDev.$name"
    $description = "PowerShell module: $name"
    $readme = $(Test-Path "$($dir.FullName)\README") -or $(Test-Path "$($dir.FullName)\README.md")

    $NewGitHubRepoConfig = @{
        Name        = "FrenchEx-Dev/${repoName}"
        Description = $description
        Visibility  = 'public'
        Push        = $true
        Source      = $dir.FullName
    }
    if ($readme) { $params.AddReadme = $true }

    Write-Debug "New-GitHubRepo @$(ConvertTo-Json $params -Compress)"
    New-GitHubRepo @NewGitHubRepoConfig
}