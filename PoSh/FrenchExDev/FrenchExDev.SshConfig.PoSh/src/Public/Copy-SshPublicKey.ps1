function Copy-SshPublicKey {
    [CmdletBinding()]
	param([string] $name = "id_rsa")

	get-content $env:userprofile\.ssh\$name.pub | Set-Clipboard 
}
