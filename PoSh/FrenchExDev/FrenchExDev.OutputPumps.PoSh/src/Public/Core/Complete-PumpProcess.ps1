<#
.SYNOPSIS
Turn a native capture into a parsed result envelope.
.DESCRIPTION
Define Complete-PumpProcess for the OutputPumps runtime. Distinguish start errors, deadlines, capture limits, unsupported formats and native failures before publishing useful data.
#>


function Complete-PumpProcess {
    <#
    .SYNOPSIS
    Turn a native capture into a parsed result envelope.
    .DESCRIPTION
    Distinguish start errors, deadlines, capture limits, unsupported formats and native failures before publishing useful data.
    #>
    
    param([Parameter(Mandatory)]$Capture, [scriptblock]$Parse,
          [string]$Summary, [int[]]$SuccessCodes=@(0), [int]$MaxItems=20, [int]$MaxOutputBytes=8192,
          [string]$RetryAdvice='SafeToRetry')
    $total=[Diagnostics.Stopwatch]::StartNew()
    try {
    $common=@{Summary=$Summary;ExitCode=$Capture.ExitCode;MaxItems=$MaxItems;MaxOutputBytes=$MaxOutputBytes}
    if ($Capture.StartError) {
        return New-PumpResult @common -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='StartFailed';Message=$Capture.StartError}
    }
    if ($Capture.TimedOut) {
        return New-PumpResult @common -Outcome Timeout -RetryAdvice Unknown -Truncated -Reasons @('Timeout') -ErrorInfo @{Code='Timeout';Message='Command exceeded its deadline; check state before retrying.'}
    }
    if ($Capture.Truncated) {
        $diagnostics=@{}
        foreach($name in @('StdoutCharacters','StderrCharacters')) {
            if($Capture.PSObject.Properties[$name]){$diagnostics[$name]=$Capture.$name}
        }
        foreach($name in @('Stdout','Stderr')) {
            if($Capture.PSObject.Properties[$name]){$diagnostics[$name+'RetainedCharacters']=$Capture.$name.Length}
        }
        return New-PumpResult @common -Outcome Failed -RetryAdvice NarrowRequest -Truncated -Reasons @('CaptureLimit') -NextRequest 'Narrow the source path/filter. Lowering MaxItems alone does not reduce native capture for every command.' -ErrorInfo @{Code='CaptureLimit';Message='Capture incomplete. Narrow the request; raw output is unavailable.';Diagnostics=$diagnostics}
    }
    $failed=$Capture.ExitCode -notin $SuccessCodes
    $data=@{}; $errorInfo=$null
    try {
        if ($Parse) {
            $parseTime=[Diagnostics.Stopwatch]::StartNew()
            try{$parsed=@(& $Parse $Capture *>&1)}finally{Write-OutputPumpTiming 'Native.Parse' $parseTime.Elapsed.TotalMilliseconds}
            if($parsed.Count -ne 1){throw 'Parser must return one business object.'}
            if($parsed[0] -is [Collections.IDictionary]){$data=$parsed[0]}
            elseif($parsed[0] -is [pscustomobject]){$data=@{};foreach($property in $parsed[0].PSObject.Properties){$data[$property.Name]=$property.Value}}
            else{throw 'Parser must return a dictionary or PSCustomObject.'}
        }
    }
    catch {
        return New-PumpResult @common -Outcome Failed -RetryAdvice CorrectRequest -Truncated -Reasons @('UnrecognizedFormat') -ErrorInfo @{Code='UnrecognizedFormat';Message='Output format was not recognized; update the software pump.';ExceptionType=$_.Exception.GetType().Name;ParserLine=$_.InvocationInfo.ScriptLineNumber}
    }
    if ($failed) {
        $category = switch -Regex ($Capture.Stderr) {
            'permission|denied|unauthorized' { 'AccessDenied'; break }
            'not a git repository' { 'NotRepository'; break }
            'connect|socket|daemon' { 'ConnectionFailed'; break }
            'regex|regular expression' { 'InvalidPattern'; break }
            default { 'CommandFailed' }
        }
        $errorInfo=@{Code=$category;Message='Command failed. Consult structured diagnostics or refine the request.'}
    }
    $meta=@{}
    if($data.Contains('_PumpMeta')){$meta=$data['_PumpMeta'];$data.Remove('_PumpMeta')}
    New-PumpResult @common @meta -Data $data -Outcome $(if($failed){'Failed'}else{'Success'}) -ErrorInfo $errorInfo -RetryAdvice $(if($failed){'CorrectRequest'}else{$RetryAdvice})
    }finally{Write-OutputPumpTiming 'Native.Complete' $total.Elapsed.TotalMilliseconds}
}
