function Invoke-OutputPumpFunction {
    param([string]$SoftwareId,[string]$Function,[hashtable]$Parameters)
    try {
        $entry=$null
        if($script:OutputPumpSession.Initialized -and !$script:OutputPumpSession.LastError){$entry=@(Get-OutputPumpRegistry | Where-Object {$_.Software -eq $SoftwareId -and $_.Function -eq $Function}) | Select-Object -First 1}
        if($entry -and $entry.Handler){$result=& $entry.Handler ([pscustomobject]@{PumpId=$entry.Id;Parameters=$Parameters});$id=$entry.Id}
        else {
            $software=@(ConvertFrom-Json -InputObject ([IO.File]::ReadAllText((Join-Path $script:PumpLibraryRoot 'catalogue.json'))) -AsHashtable) | Where-Object Id -eq $SoftwareId
            $definition=@($software.Commands | Where-Object Function -eq $Function)
            if($definition.Count -ne 1){throw 'Specialized pump unavailable.'}
            $module=Import-OutputPumpSoftware ([pscustomobject]@{Module=(Join-Path $script:PumpLibraryRoot $software.Module)})
            $result=& $module.ExportedFunctions[$Function] @Parameters;$id=$definition[0].Id
        }
        $settings=@{Result=$result;PumpId=$id}
        foreach($name in @('MaxItems','MaxOutputBytes')){if($Parameters.ContainsKey($name)){$settings[$name]=$Parameters[$name]}}
        Complete-OutputPumpResult @settings
    }catch{New-PumpResult -Summary 'Specialized pump could not execute.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='PumpInvocationFailed';ExceptionType=$_.Exception.GetType().Name;Message=$_.Exception.Message;Line=$_.InvocationInfo.ScriptLineNumber}}
}
