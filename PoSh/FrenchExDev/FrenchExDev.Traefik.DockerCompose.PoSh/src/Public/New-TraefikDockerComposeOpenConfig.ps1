function New-TraefikDockerComposeOpenConfig {
    [CmdletBinding()]
    param(
        [scriptblock] $Openness
    )

    $config = @{
        openess = @()
    }

    $config.openess = Invoke-Command $Openness -ArgumentList $config

    $config
}

