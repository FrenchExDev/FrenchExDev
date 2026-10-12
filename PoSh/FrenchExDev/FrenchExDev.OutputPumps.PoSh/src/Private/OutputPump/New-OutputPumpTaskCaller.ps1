<#
.SYNOPSIS
Write a temporary caller that emits one task result.
.DESCRIPTION
Define New-OutputPumpTaskCaller for the OutputPumps runtime. Encode the payload as data, reuse the session runtime when possible and delete the caller after execution.
#>


function New-OutputPumpTaskCaller {
    <#
    .SYNOPSIS
    Write a temporary caller that emits one task result.
    .DESCRIPTION
    Encode the payload as data, reuse the session runtime when possible and delete the caller after execution.
    #>
    
    param([scriptblock]$ScriptBlock,[object[]]$ArgumentList,[int]$MaxItems,[int]$MaxOutputBytes,[string]$ScriptDirectory)
    $payload=@{Body=$ScriptBlock.ToString();Arguments=$ArgumentList;Runtime=$ExecutionContext.SessionState.Module.Path;ConfigurationPath=$script:OutputPumpSession.ConfigurationPath;MaxItems=$MaxItems;MaxOutputBytes=$MaxOutputBytes}
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json $payload -Depth 20 -Compress)))
    $text=@'
<#
.SYNOPSIS
Execute a generated task and emit one bounded JSON result.
.DESCRIPTION
Decode the data payload, reuse or initialize the runtime and remove this temporary caller after execution.
#>

#requires -Version 7.4
#requires -PSEdition Core
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
try {
    $call=ConvertFrom-Json -AsHashtable -InputObject ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__CALL__')))
    $runtime=Get-Module -All | Where-Object Path -eq $call.Runtime | Select-Object -First 1
    if(!$runtime){$runtime=Import-Module $call.Runtime -PassThru -DisableNameChecking -ErrorAction Stop}
    $ready=& $runtime {$script:OutputPumpSession.Initialized -and !$script:OutputPumpSession.LastError}
    if(!$ready -and $call.ConfigurationPath) {
        $setup=& $runtime.ExportedFunctions['Setup-OutputPumps'] -ConfigurationPath $call.ConfigurationPath
        if($setup.Outcome -ne 'Success') {& $runtime.ExportedFunctions['ConvertTo-OutputPumpJson'] -InputObject $setup -MaxOutputBytes $call.MaxOutputBytes;return}
    }
    & $runtime.ExportedFunctions['Invoke-OutputPumpTask'] -ScriptBlock ([scriptblock]::Create($call.Body)) -ArgumentList @($call.Arguments) -MaxItems $call.MaxItems -MaxOutputBytes $call.MaxOutputBytes -AsJson
} catch {
    '{"Summary":"Task caller failed.","Outcome":"Failed","Data":{"Items":[]},"ExitCode":null,"Error":{"Code":"TaskCallerFailed"},"RetryAdvice":"CorrectRequest","Truncation":{"Applied":false,"Reasons":[],"ReturnedCount":0,"TotalCount":null,"NextRequest":null}}'
} finally {if($PSCommandPath -and [IO.File]::Exists($PSCommandPath)){[IO.File]::Delete($PSCommandPath)}}
'@
    [void][IO.Directory]::CreateDirectory($ScriptDirectory)
    $path=Join-Path $ScriptDirectory ('pump-task-'+[guid]::NewGuid().ToString('N')+'.ps1')
    [IO.File]::WriteAllText($path,$text.Replace('__CALL__',$encoded),[Text.UTF8Encoding]::new($false))
    New-PumpResult -Summary 'Temporary bounded task caller generated.' -Data @{Items=@(@{ScriptPath=$path;Mode='Task'})}
}
