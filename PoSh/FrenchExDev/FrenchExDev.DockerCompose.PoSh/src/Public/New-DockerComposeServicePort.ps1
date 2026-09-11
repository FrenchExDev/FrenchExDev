function New-DockerComposeServicePort {
    [CmdletBinding()]
    param(
        [int] $Target,
        [int] $Published,
        [ValidateSet("tcp", "udp")] [string] $Proto,
        [string] $Mode = "host"
    )

    @{
        target    = $Target
        published = $Published
        protocol  = "$Proto"
        mode      = "$Mode"
    }
}
