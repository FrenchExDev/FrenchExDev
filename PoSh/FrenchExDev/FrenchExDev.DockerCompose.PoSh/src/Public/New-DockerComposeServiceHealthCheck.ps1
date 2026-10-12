function New-DockerComposeServiceHealthCheck{
    param(
        [string] $Test,
        [string] $Interval,
        [int] $Retries,
        [string] $StartPeriod,
        [string] $Timeout
    )

    [pscustomobject] @{
        test         = $Test
        interval     = $Interval
        retries      = $Retries
        start_period = $StartPeriod
        timeout      = $Timeout
    }
}
