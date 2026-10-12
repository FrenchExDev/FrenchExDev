function Write-DockerComposeContext {
    [CmdletBinding()]
    param(
        [object] $Contexts,
        [string] $File = "dc-contexts.yaml"
    )

    Invoke-Command $Contexts | ConvertTo-Yaml | Out-File $File -Encoding ascii
}