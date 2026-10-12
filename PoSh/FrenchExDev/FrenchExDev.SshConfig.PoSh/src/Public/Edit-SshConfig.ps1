function Edit-SshConfig { 
    [CmdletBinding()]
    param()

    code "$env:userprofile/.ssh/config" 
}
