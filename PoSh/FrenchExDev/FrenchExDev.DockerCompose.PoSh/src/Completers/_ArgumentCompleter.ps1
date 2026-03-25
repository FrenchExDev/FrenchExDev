$getDockerComposeContextNames = {
    param($commandName, $parameterName, $stringMatch, $commandAst, $fakeBoundParameters)

    Get-DockerComposeContexts 
}

$getDockerComposeFilesNames = {
    param($commandName, $parameterName, $stringMatch, $commandAst, $fakeBoundParameters)

    get-childitem -depth 0 -filter "docker-compose*.yaml" | ForEach-Object { $_.leaf }
}

$getDockerComposeContextsNames = {
    param($commandName, $parameterName, $stringMatch, $commandAst, $fakeBoundParameters)
    Get-DockerComposeContexts
}

$getDockerComposeServicesNames = {
    param($commandName, $parameterName, $stringMatch, $commandAst, $fakeBoundParameters)
    $context = if ($fakeBoundParameters["Context"]) { $fakeBoundParameters["Context"] } else { "default" }
    Get-DockerComposeConfig -Context $context  | yq '.services | keys[]'
}

Register-ArgumentCompleter -CommandName Set-CurrentDockerComposeContext -ParameterName Name -ScriptBlock $getDockerComposeContextNames
Register-ArgumentCompleter -CommandName Get-DockerComposeContext -ParameterName Name -ScriptBlock $getDockerComposeContextNames

Register-ArgumentCompleter -CommandName Get-DockerComposeContext -ParameterName Name -ScriptBlock $getDockerComposeContextNames
Register-ArgumentCompleter -CommandName Get-DockerComposeConfig -ParameterName Context -ScriptBlock $getDockerComposeContextNames

Register-ArgumentCompleter -CommandName Get-RawDockerComposeConfig -ParameterName DockerComposeFile -ScriptBlock $getDockerComposeFilesNames

Register-ArgumentCompleter -CommandName Up-DockerCompose -ParameterName Context -ScriptBlock $getDockerComposeContextNames
Register-ArgumentCompleter -CommandName Up-DockerCompose -ParameterName Service -ScriptBlock $getDockerComposeServicesNames

Register-ArgumentCompleter -CommandName Down-DockerCompose -ParameterName Context -ScriptBlock $getDockerComposeContextsNames
Register-ArgumentCompleter -CommandName Down-DockerCompose -ParameterName Service -ScriptBlock $getDockerComposeServicesNames

Register-ArgumentCompleter -CommandName Logs-DockerCompose -ParameterName Context -ScriptBlock $getDockerComposeContextsNames
Register-ArgumentCompleter -CommandName Logs-DockerCompose -ParameterName Service -ScriptBlock $getDockerComposeServicesNames

Register-ArgumentCompleter -CommandName Rm-DockerCompose -ParameterName Context -ScriptBlock $getDockerComposeContextsNames
Register-ArgumentCompleter -CommandName Rm-DockerCompose -ParameterName Service -ScriptBlock $getDockerComposeServicesNames

Register-ArgumentCompleter -CommandName Exec-DockerCompose -ParameterName Context -ScriptBlock $getDockerComposeContextsNames
Register-ArgumentCompleter -CommandName Exec-DockerCompose -ParameterName Service -ScriptBlock $getDockerComposeServicesNames

Register-ArgumentCompleter -CommandName Stop-DockerCompose -ParameterName Context -ScriptBlock $getDockerComposeContextsNames
Register-ArgumentCompleter -CommandName Stop-DockerCompose -ParameterName Service -ScriptBlock $getDockerComposeServicesNames

Register-ArgumentCompleter -CommandName Get-DockerCompose -ParameterName DockerContext -ScriptBlock $getDockerContextsNames
Register-ArgumentCompleter -CommandName Get-DockerCompose -ParameterName Context -ScriptBlock $getDockerComposeContextsNames
Register-ArgumentCompleter -CommandName Get-DockerCompose -ParameterName Service -ScriptBlock $getDockerComposeServicesNames

Register-ArgumentCompleter -CommandName Build-DockerGitlabRunnerService -ParameterName Context -ScriptBlock $getDockerComposeContextsNames
