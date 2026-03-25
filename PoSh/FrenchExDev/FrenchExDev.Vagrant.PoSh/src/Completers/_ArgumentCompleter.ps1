$getExistingMachines = {
    $(vagrant status --machine-readable | select-string -pattern '(.*),(.*),metadata,provider,virtualbox' | Select-CaptureGroup).2
}

Register-ArgumentCompleter -CommandName Stop-Vagrant -ParameterName MachineName -ScriptBlock $getExistingMachines
Register-ArgumentCompleter -CommandName Start-Vagrant -ParameterName MachineName -ScriptBlock $getExistingMachines