function Get-TestProject {
    [CmdletBinding()]
    param()
    Get-ChildItem ./test/ -Recurse -Filter "*.csproj" | ForEach-Object { $(Split-Path $_ -leaf).Replace(".csproj", "") }
}
