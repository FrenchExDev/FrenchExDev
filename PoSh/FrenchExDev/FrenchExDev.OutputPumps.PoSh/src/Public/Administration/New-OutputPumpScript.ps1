<#
.SYNOPSIS
Generate a temporary bounded pump or task caller.
.DESCRIPTION
Define New-OutputPumpScript for the OutputPumps runtime. Validate specialized calls and encode arguments as data; task callers aggregate intermediate results under one budget.
#>


function New-OutputPumpScript {
    <#
    .SYNOPSIS
    Generate a temporary bounded pump or task caller.
    .DESCRIPTION
    Validate specialized calls and encode arguments as data; task callers aggregate intermediate results under one budget.
    #>
    
    [CmdletBinding(DefaultParameterSetName='Pump')]
    param([Parameter(Mandatory,ParameterSetName='Pump')][string]$SoftwareId,[Parameter(Mandatory,ParameterSetName='Pump')][string]$Function,[Parameter(ParameterSetName='Pump')][hashtable]$Parameters=@{},
          [Parameter(Mandatory,ParameterSetName='Task')][scriptblock]$TaskScriptBlock,[Parameter(ParameterSetName='Task')][object[]]$ArgumentList=@(),
          [Parameter(ParameterSetName='Task')][ValidateRange(1,1000)][int]$MaxItems=20,[Parameter(ParameterSetName='Task')][ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [string]$LibraryRoot=$script:PumpLibraryRoot,[string]$ScriptDirectory=([IO.Path]::GetTempPath()))
    try {
        if($PSCmdlet.ParameterSetName -eq 'Task') {
            return New-OutputPumpTaskCaller -ScriptBlock $TaskScriptBlock -ArgumentList $ArgumentList -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -ScriptDirectory $ScriptDirectory
        }
        $entry=$null
        if($script:OutputPumpSession.Initialized -and !$script:OutputPumpSession.LastError){$entry=@(Get-OutputPumpRegistry | Where-Object {$_.Software -eq $SoftwareId -and $_.Function -eq $Function}) | Select-Object -First 1}
        if($entry){$command=$entry.Command}
        else {
            $catalogue=@(ConvertFrom-Json -InputObject ([IO.File]::ReadAllText((Join-Path $LibraryRoot 'catalogue.json'))) -AsHashtable)
            $software=@($catalogue | Where-Object Id -eq $SoftwareId)
            if($software.Count -ne 1 -or $Function -notin $software[0].Commands.Function){throw 'Unregistered specialized pump.'}
            $command=(Import-OutputPumpSoftware ([pscustomobject]@{Module=(Join-Path $LibraryRoot $software[0].Module)})).ExportedFunctions[$Function]
        }
        if($command){foreach($key in $Parameters.Keys){if(!$command.Parameters.ContainsKey($key)){throw 'Unknown call parameter.'}};if(!(Test-PumpRequiredParameters $command $Parameters)){throw 'Required call parameter missing.'}}
        $providerImport=$null
        $providerModule=if($entry -and $entry.Command){$entry.Command.Module}else{$null}
        if(!$providerModule -and $entry -and $script:OutputPumpSession.Providers.ContainsKey($entry.Software)){
            $providerModule=$script:OutputPumpSession.Providers[$entry.Software].Module
        }
        if($providerModule){
            $manifest=Join-Path $providerModule.ModuleBase ($providerModule.Name+'.psd1')
            $providerImport=if([IO.File]::Exists($manifest)){$manifest}else{$providerModule.Path}
        }
        $payload=@{SoftwareId=$SoftwareId;Function=$Function;Parameters=$Parameters;Runtime=$ExecutionContext.SessionState.Module.Path;ProviderImport=$providerImport;ConfigurationPath=$script:OutputPumpSession.ConfigurationPath;HadRegistry=[bool]$entry}
        $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json $payload -Depth 20 -Compress)))
        $text=@'
<#
.SYNOPSIS
Execute a generated specialized pump call.
.DESCRIPTION
Restore the encoded arguments as data, initialize only the selected provider when needed and emit compact JSON.
Delete this temporary caller after execution, including when invocation fails.
#>

#requires -Version 7.4
#requires -PSEdition Core
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
try {
    $call=ConvertFrom-Json -AsHashtable -InputObject ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__CALL__')))
    $runtime=Get-Module -All | Where-Object Path -eq $call.Runtime | Select-Object -First 1
    if(!$runtime){$runtime=Import-Module $call.Runtime -PassThru -DisableNameChecking -ErrorAction Stop}
    if($call.HadRegistry) {
        $ready=& $runtime {$script:OutputPumpSession.Initialized -and !$script:OutputPumpSession.LastError}
        if(!$ready){
            if($call.ProviderImport){Import-Module $call.ProviderImport -Global -DisableNameChecking -ErrorAction Stop}
            $setup=& $runtime.ExportedFunctions['Setup-OutputPumps'] -ConfigurationPath $call.ConfigurationPath -ProviderId $call.SoftwareId
            if($setup.Outcome -ne 'Success'){throw 'Setup failed.'}
        }
    }
    $result=& $runtime {param($call) Invoke-OutputPumpFunction -SoftwareId $call.SoftwareId -Function $call.Function -Parameters $call.Parameters} $call
    $bytes=if($call.Parameters.ContainsKey('MaxOutputBytes')){$call.Parameters.MaxOutputBytes}else{8192}
    $items=if($call.Parameters.ContainsKey('MaxItems')){$call.Parameters.MaxItems}else{20}
    & $runtime.ExportedFunctions['ConvertTo-OutputPumpJson'] -InputObject $result -MaxItems $items -MaxOutputBytes $bytes
} catch {
    '{"Summary":"Pump call failed; correct its parameters or implementation.","Outcome":"Failed","Data":{"Items":[]},"ExitCode":null,"Error":{"Code":"InvalidCallOrPumpFailure"},"RetryAdvice":"CorrectRequest","Truncation":{"Applied":false,"Reasons":[],"ReturnedCount":0,"TotalCount":null,"NextRequest":null}}'
} finally {if($PSCommandPath -and [IO.File]::Exists($PSCommandPath)){[IO.File]::Delete($PSCommandPath)}}
'@
        [void][IO.Directory]::CreateDirectory($ScriptDirectory)
        $path=Join-Path $ScriptDirectory ('pump-'+[guid]::NewGuid().ToString('N')+'.ps1')
        [IO.File]::WriteAllText($path,$text.Replace('__CALL__',$encoded),[Text.UTF8Encoding]::new($false))
        New-PumpResult -Summary 'Temporary bounded pump caller generated.' -Data @{Items=@(@{ScriptPath=$path;SoftwareId=$SoftwareId;Function=$Function})}
    }catch{New-PumpResult -Summary 'Pump caller could not be generated.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='ScriptGenerationFailed';ExceptionType=$_.Exception.GetType().Name;Message=$_.Exception.Message;Line=$_.InvocationInfo.ScriptLineNumber}}
}
