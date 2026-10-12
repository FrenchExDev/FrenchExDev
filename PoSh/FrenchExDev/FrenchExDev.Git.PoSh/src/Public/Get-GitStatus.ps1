function Get-GitStatus {
    [CmdletBinding()]
    param(
        [string] $WorkingDirectory = "./"
    )

    # Escape and colour codes for use in virtual terminal escape sequences
    $esc = [char]27
    $red = '31'
    $green = '32'

    # Assume the parent folder of this .git folder is the working copy
    $workingCopy = resolve-path $WorkingDirectory

    if ([string]::IsNullOrEmpty($workingCopy.Path)) { return; }

    $repositoryName = Split-Path $(split-path $workingCopy.path -Parent) -leaf
    # Change the working folder to the working copy
    Push-Location $(split-path $workingCopy.Path -Parent)

    # Get a list of untracked/uncommitted changes
    [Array]$gitStatus = $(git status --porcelain) | 
    ForEach-Object { $_.Trim() }
    # Status includes VT escape sequences for coloured text
    $status = if ($gitStatus) { "$esc[$($red)mCHECK$esc[0m" } else { "$esc[$($green)mOK$esc[0m" }
    # Branch Name
    $gitBranchName = $(git branch)
    $branchName = if (![string]::IsNullOrEmpty($gitBranchName)) { $gitBranchName.Trim() } else { "N/A" }
    # For some reason, the git status --porcelain output returns 
    # two '?' chars for untracked changes, when all other statuses 
    # are one character... this just cleans it up so that it's 
    # nicer to scan visually in the terminal
    $details = ($gitStatus -replace '\?\?', '?' | Out-String).TrimEnd()
    $latestCommit = if (![string]::IsNullOrEmpty($gitBranchName)) { $(git log --oneline -n 1) } else { "N/A" }
    # Change back to the original directory
    Pop-Location
    # Return a simple 'row' object containing all the info
    [pscustomobject] @{ 
        'Working Copy'  = $repositoryName    
        Status          = $status
        Branch          = $branchName
        'Latest Commit' = $latestCommit
        Details         = $details
    }
}
