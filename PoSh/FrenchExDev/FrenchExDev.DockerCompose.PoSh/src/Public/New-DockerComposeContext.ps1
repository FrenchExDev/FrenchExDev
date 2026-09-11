function New-DockerComposeContext {
    [CmdletBinding()]
    param(
        [string] $Active = "default",
        [scriptblock[]] $Context,
        [string] $File
    )

    $contexts = @()

    foreach ($currentContext in $Context) {
        $contexts += $(Invoke-Command $currentContext)
    }

    $dockerComposeContext = [pscustomobject] @{
        active   = $Active
        contexts = $contexts
    }

    if (![string]::IsNullOrEmpty($File)) {
        Write-DockerComposeContext -File "./docker-compose/dc-contexts.yaml" -Contexts { $dockerComposeContext }
        return
    }

    $dockerComposeContext
}
