function New-GitLabRunnerDockerComposeService {
    [CmdletBinding()]
    param(

    )

    @{
        build       = @{
            context    = "./images/gitlab-runner"
            dockerfile = "Dockerfile"
        }
        environment = @(
            "DOMAIN=`${ACME_NAME}.`${ACME_TLD}",
            "DOCKER_HOST=tcp://gitlab-dind:2376",
            "RUNNER_EXECUTOR=docker",
            "CI_SERVER_URL=https://gitlab.`${ACME_NAME}.`${ACME_TLD}/ci"
        )
        links       = @("gitlab")
        depends_on  = @{
            gitlab = @{
                condition = "service_healthy"
            }
        }
        networks    = @(
            "gitlab-runners"
        )
        extra_hosts = @(
            "gitlab.`${ACME_NAME}.`${ACME_TLD}:`${GITLAB_FRONTEND_IP}"
        )
    }
}
