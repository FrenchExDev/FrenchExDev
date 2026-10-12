function New-TraefikDockerComposeOpenConfigItem {
    [CmdletBinding()]
    param(
        [string] $Name,
        [int] $Port,
        [int] $PublishPort,
        [string] $Proto,
        [string] $Mode
    )

    @{
        name    = $Name
        port    = $Port
        publish = $PublishPort
        proto   = $Proto
        mode    = $Mode
    }
}