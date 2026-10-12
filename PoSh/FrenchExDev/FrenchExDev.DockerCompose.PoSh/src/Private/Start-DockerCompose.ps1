function Start-DockerCompose {
    [CmdletBinding()]
    param(
        [parameter(mandatory = $true, position = 1)] [string] $Command,
        [parameter(mandatory = $false, position = 2)] [string[]] $CommandArgs,
        [string] $Context
    )

    $contextConfigName = if ([string]::IsNullOrEmpty($context)) { Get-DockerComposeContext GetActive -Verbose:$VerbosePreference -Debug:$DebugPreference } else { $Context }
    $contextConfig = Get-DockerComposeContext Get -Name $contextConfigName -Verbose:$VerbosePreference -Debug:$DebugPreference
    
    $dockerComposeFilesArr = [System.Collections.ArrayList]::new()

    foreach ($dockerComposeFileItem in $contextConfig.files) {
        $dockerComposeFilesArr.Add("-f") | out-null
        $dockerComposeFilesArr.Add($DockerComposeFileItem) | out-null    
    }

    $dockerComposeFilesArg = $dockerComposeFilesArr -join " "

    $projectNameArg = if (![string]::IsNullOrEmpty($contextConfig.project_name)) { "--project-name $($contextConfig.project_name)" }

    $cmd = "docker compose $dockerComposeFilesArg $projectNameArg $command $commandArgs"

    Write-Verbose "Start-DockerCompose > $cmd"

    Invoke-Expression $cmd
}
