function New-GitLabRunnerDockerCompose {
    [CmdletBinding()]
    param(
        [string] $File
    )

    New-DockerCompose -File "$File" -Services {
        @{
            "gitlab-runner" = New-GitLabRunnerDockerComposeService
        }
    } -Volumes {
        @{
            "gitlab-runner-data"   = @{}
            "gitlab-runner-config" = @{}
        }
    } -Networks {
        @{
            gitlab           = @{}
        }
    }
}
