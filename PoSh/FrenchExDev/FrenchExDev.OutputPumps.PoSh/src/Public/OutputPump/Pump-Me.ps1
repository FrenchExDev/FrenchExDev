function Pump-Me {
    [CmdletBinding(DefaultParameterSetName='Command',PositionalBinding=$false)]
    param(
        [Parameter(Mandatory,Position=0,ParameterSetName='Command')][string]$Command,
        [Parameter(Position=1,ValueFromRemainingArguments,ParameterSetName='Command')][string[]]$NativeArguments=@(),
        [Parameter(Mandatory,ParameterSetName='Pipeline')][string]$PumpId,
        [Parameter(Mandatory,ValueFromPipeline,ParameterSetName='Pipeline')]$InputObject,
        [string]$ConfigurationPath,[string]$WorkingDirectory='.',[scriptblock]$Filter,[scriptblock]$Map,
        [ValidateRange(1,1000)][int]$MaxItems=20,[ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,[ValidateRange(1,86400)][int]$TimeoutSeconds=600
    )
    process {
        try{$registry=@(Get-OutputPumpRegistry $ConfigurationPath)}
        catch{return New-PumpResult -Summary 'OutputPumps registry unavailable; run Setup-OutputPumps.' -Outcome Failed -RetryAdvice CorrectRequest -MaxOutputBytes $MaxOutputBytes -ErrorInfo @{Code='RegistryNotInitialized'}}
        try {
            if($PSCmdlet.ParameterSetName -eq 'Pipeline') {
                $selected=@($registry | Where-Object Id -eq $PumpId)
                if($selected.Count -ne 1 -or !$selected[0].TestInput -or !$selected[0].Transform){throw 'Unknown object transformer.'}
                $entry=$selected[0]
                if($entry.TestInput -is [scriptblock]){$recognition=@(& $entry.TestInput $InputObject *>&1)}else{$module=Import-OutputPumpSoftware $entry;$recognition=@(& $module.ExportedFunctions[$entry.TestInput] $InputObject *>&1)}
                if($recognition.Count -ne 1 -or $recognition[0] -isnot [bool]){throw 'Recognition must return one boolean.'}
                if(!$recognition[0]){return $InputObject}
                $result=if($entry.Transform -is [scriptblock]){& $entry.Transform $InputObject *>&1}else{& $module.ExportedFunctions[$entry.Transform] $InputObject *>&1}
            } else {
                $executable=[IO.Path]::GetFileNameWithoutExtension($Command)
                $matches=@($registry | Where-Object {
                    $r=$_.Route
                    if(!$r -or $r.Executable -ne $executable -or @($r.Tokens).Count -gt $NativeArguments.Count){return $false}
                    for($i=0;$i -lt @($r.Tokens).Count;$i++){if($r.Tokens[$i] -cne $NativeArguments[$i]){return $false}}
                    $true
                } | Sort-Object {@($_.Route.Tokens).Count} -Descending)
                if(!$matches.Count) {
                    $location=$null
                    try{$native=Get-Command $Command -CommandType Application -ErrorAction Stop | Select-Object -First 1;$location=Get-Location;Set-Location -LiteralPath $WorkingDirectory -ErrorAction Stop;& $native.Source @NativeArguments;$global:LASTEXITCODE=$LASTEXITCODE}
                    finally{if($location){Set-Location -LiteralPath $location.Path}}
                    return
                }
                $entry=$matches[0]
                try{$parameters=ConvertFrom-OutputPumpArguments $entry.Route @($NativeArguments | Select-Object -Skip @($entry.Route.Tokens).Count)}
                catch{return New-PumpResult -Summary 'Native options are unsupported by this pump.' -Outcome Unsupported -RetryAdvice CorrectRequest -MaxOutputBytes $MaxOutputBytes -ErrorInfo @{Code='UnsupportedNativeArguments'}}
                $function=if($entry.PSObject.Properties['Command']){$entry.Command}else{$null}
                $handler=if($entry.PSObject.Properties['Handler']){$entry.Handler}else{$null}
                if(!$handler){$module=Import-OutputPumpSoftware $entry;$function=$module.ExportedFunctions[$entry.Function]}
                $common=@{WorkingDirectory=$WorkingDirectory;MaxItems=$MaxItems;MaxOutputBytes=$MaxOutputBytes;TimeoutSeconds=$TimeoutSeconds}
                foreach($key in $common.Keys){if((!$function -or $function.Parameters.ContainsKey($key)) -and !$parameters.ContainsKey($key)){$parameters[$key]=$common[$key]}}
                if($function -and !(Test-PumpRequiredParameters $function $parameters)){return New-PumpResult -Summary 'Required native arguments are missing.' -Outcome Unsupported -RetryAdvice CorrectRequest -MaxOutputBytes $MaxOutputBytes -ErrorInfo @{Code='MissingNativeArguments'}}
                $call=[pscustomobject]@{PSTypeName='FrenchExDev.OutputPump.InvocationContext';PumpId=$entry.Id;Parameters=$parameters;WorkingDirectory=$WorkingDirectory;MaxItems=$MaxItems;MaxOutputBytes=$MaxOutputBytes;TimeoutSeconds=$TimeoutSeconds}
                $result=if($handler){& $handler $call *>&1}else{& $function @parameters *>&1}
            }
            $all=@($result)
            if($all.Count -ne 1){throw 'Pump must return exactly one envelope.'}
            foreach($field in @('Summary','Outcome','Data','ExitCode','Error','RetryAdvice','Truncation')){if(!$result.PSObject.Properties[$field]){throw 'Invalid pump envelope.'}}
            Complete-OutputPumpResult $result $entry.Id $Filter $Map $MaxItems $MaxOutputBytes
        }catch{New-PumpResult -Summary 'Pump execution failed.' -Outcome Failed -RetryAdvice CorrectRequest -MaxOutputBytes $MaxOutputBytes -ErrorInfo @{Code='PumpInvocationFailed';ExceptionType=$_.Exception.GetType().Name}}
    }
}
