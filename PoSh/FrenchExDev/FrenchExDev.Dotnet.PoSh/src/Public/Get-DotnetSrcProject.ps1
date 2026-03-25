function Get-SrcProject {
    [CmdletBinding()]
    param()
    Get-ChildItem ./src/ -Recurse -Filter "*.csproj" | ForEach-Object { $(Split-Path $_ -leaf).Replace(".csproj", "") }
}
