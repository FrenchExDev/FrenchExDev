$getExistingProjectsAcme = {
    param($commandName, $parameterName, $stringMatch, $commandAst, $fakeBoundParameters)
    Get-ChildItem -Directory -Depth 0 ./PoSh/ | ForEach-Object { $_.Name }
}

$getExistingProjectsOfAcme = {
    param($commandName, $parameterName, $stringMatch, $commandAst, $fakeBoundParameters)
    $acme = $fakeBoundParameters['Acme']
    Get-ChildItem -Directory -Depth 0 "./PoSh/$acme" | ForEach-Object { $_.Name }
}


Register-ArgumentCompleter -CommandName Build-PoShModule -ParameterName Acme -ScriptBlock $getExistingProjectsAcme
Register-ArgumentCompleter -CommandName Get-PoSh -ParameterName Acme -ScriptBlock $getExistingProjectsAcme
Register-ArgumentCompleter -CommandName Get-PoSh -ParameterName Name -ScriptBlock $getExistingProjectsOfAcme