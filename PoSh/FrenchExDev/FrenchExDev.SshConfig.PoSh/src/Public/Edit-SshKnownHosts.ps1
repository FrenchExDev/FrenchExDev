function Edit-SshKnownHosts {
    [CmdletBinding()]
    param()

    code "$env:userprofile/.ssh/known_hosts"
}