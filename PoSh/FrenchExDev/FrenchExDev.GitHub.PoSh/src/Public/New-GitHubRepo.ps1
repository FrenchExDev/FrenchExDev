function New-GitHubRepo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $Name,
        [string] $Path,
        [switch] $AddReadme,
        [switch] $Clone,
        [string] $Description,
        [switch] $DisableIssues,
        [switch] $DisableWiki,
        [string] $GitIgnore,
        [string] $Homepage,
        [ValidateSet('public', 'private', 'internal')]
        [string] $Visibility = 'public',
        [string] $Template,
        [switch] $Push,
        [string] $Source
    )

    if (![string]::IsNullOrEmpty($Path) -and !(Test-Path $Path)) {
        Write-Error "The specified path '$Path' does not exist."
        return
    }
    else {
        Push-Location $Path 
    }

    $cmd = @('gh', 'repo', 'create', $Name)

    if (![string]::IsNullOrEmpty($Visibility)) {
        $cmd += $("--$Visibility")
    }

    if ($AddReadme) {
        $cmd += $( "--add-readme")
    }

    if ($Clone) {
        $cmd += $("--clone")
    }

    if ($Push) {
        $cmd += $("--push")
    }

    if (![string]::IsNullOrEmpty($Description)) {
        $cmd += @('--description', """$Description""")
    }

    if ($DisableIssues) {
        $cmd += $("--disable-issues")
    }

    if ($DisableWiki) {
        $cmd += $("--disable-wiki")
    }

    if (![string]::IsNullOrEmpty($GitIgnore)) {
        $cmd += @('--gitignore', """$GitIgnore""")
    }

    if (![string]::IsNullOrEmpty($Homepage)) {
        $cmd += @('--homepage', """$Homepage""")
    }

    if (![string]::IsNullOrEmpty($Template)) {
        $cmd += @('--template', """$Template""")
    }

    if (![string]::IsNullOrEmpty($Source)) {
        $cmd += @('--source', """$Source""")
    }

    Write-Debug "New-GitHubRepo: Executing command: $($cmd -join ' ')"

    $($cmd -join ' ')

    try {
        Push-Location $Path
        $commandString = $($cmd -join ' ')
        Write-Information $commandString
        Invoke-Expression -Command $commandString
        Pop-Location
    }
    catch {
        Write-Error "Failed to create GitHub repository: $_"
    }

    if (![string]::IsNullOrEmpty($Path) -and (Test-Path $Path)) {
        Pop-Location
    }
}