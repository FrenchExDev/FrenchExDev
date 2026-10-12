function New-Git {
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $Branch = "main",
        [ValidateNotNullOrWhiteSpace()] [string] $Message = "Initial commit",
        [string[]] $Ignore,
        [string] $RemoteBranch,
        [string] $RemoteName,
        [switch] $Push,
        [switch] $SetUpstream,
        [switch] $Force
    )

    Write-Verbose "New-Git > Branch: '$branch', Message: '$message'"

    $forceArg = if ($Force) { "--force" }

    if ($Ignore -gt 0) {
        $IgnoreString = if ($Ignore.Count -gt 0) { $Ignore -split "\r\n" }
        $IgnoreString | Out-File .gitignore -Encoding ascii
    }

    if (!(test-path .git)) {
        git init -b $Branch
        git add -A
        git commit -m"$Message"    
    }

    if (![string]::IsNullOrEmpty($RemoteName) -and ![string]::IsNullOrEmpty($RemoteBranch)) {
        git remote remove $RemoteName | Out-Null
        git remote add $RemoteName $RemoteBranch
    }

    if ($SetUpstream -or $Push) {
        git push $forceArg --set-upstream $RemoteName $Branch
    }

    if ($Push) {
        git push $forceArg $RemoteName $Branch
    }
}
