$getVosMachinesNames = {
    $config = Get-VosConfigObject
    $($config.GetEnabledMachines() | ForEach-Object { $_.Name })
}

$getVosMachineInstances = {
    param($commandName, $parameterName, $stringMatch, $commandAst, $fakeBoundParameters)
    
    $config = Get-VosConfigObject
    $machineNameInstances = $($config.GetEnabledMachines() | Where-Object { $_.Name -eq $fakeBoundParameters["MachineName"] }).Value.instances.length
    for ($i = 0; $i -le $machineNameInstances - 1; $i++) {
        $config.FormatInstance($i)
    }
}

$getProvisionWith = {
    param($commandName, $parameterName, $stringMatch, $commandAst, $fakeBoundParameters)
    
    $machineName = $fakeBoundParameters["MachineName"]
    $machineNumber = $fakeBoundParameters["MachineNumber"]
    $machine = Get-VosMachine  $machineName $machineNumber -Machine -NoDebug
    $machine.machineType.base.provisioning.psobject.properties | ForEach-Object { $_.Name }
}

Register-ArgumentCompleter -CommandName Get-VosMachine -ParameterName MachineName -ScriptBlock $getVosMachinesNames
Register-ArgumentCompleter -CommandName Get-VosMachine -ParameterName MachineNumber -ScriptBlock $getVosMachineInstances
Register-ArgumentCompleter -CommandName Get-VosMachine -ParameterName ProvisionWith -ScriptBlock $getProvisionWith

Register-ArgumentCompleter -CommandName Get-VosMachineGroup -ParameterName Group -ScriptBlock $getVosMachinesNames