$getDockerContextsNames = {
    param($commandName, $parameterName, $stringMatch, $commandAst, $fakeBoundParameters)

    docker context ls --format '{{.Name}}'
}

Register-ArgumentCompleter -CommandName Switch-DockerContext -ParameterName Name -ScriptBlock $getDockerContextsNames
Register-ArgumentCompleter -CommandName Remove-DockerContext -ParameterName RemoteName -ScriptBlock $getDockerContexts