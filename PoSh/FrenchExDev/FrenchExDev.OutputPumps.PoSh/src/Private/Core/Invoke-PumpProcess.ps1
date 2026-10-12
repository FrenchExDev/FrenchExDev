<#
.SYNOPSIS
Capture both native output streams within fixed buffers.
.DESCRIPTION
Define Invoke-PumpProcess for the OutputPumps runtime. Resolve the executable and working directory, wait for completion and represent start failures as capture data.
#>


function Invoke-PumpProcess {
    <#
    .SYNOPSIS
    Capture both native output streams within fixed buffers.
    .DESCRIPTION
    Resolve the executable and working directory, wait for completion and represent start failures as capture data.
    #>
    
    <# Internal primitive. Its capture is never the agent-facing result. Use a software pump. #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Executable, [string[]]$Arguments = @(),
          [string]$WorkingDirectory = '.', [ValidateRange(1,86400)][int]$TimeoutSeconds = 600,
          [ValidateRange(1024,4194304)][int]$CaptureLimit = 4194304,[Collections.IDictionary]$Environment)
    $total=[Diagnostics.Stopwatch]::StartNew()
    try {
        $phase=[Diagnostics.Stopwatch]::StartNew()
        try{Initialize-PumpCapture}finally{Write-OutputPumpTiming 'Native.Initialize' $phase.Elapsed.TotalMilliseconds}
        $phase.Restart()
        try {
        $directory = (Resolve-Path -LiteralPath $WorkingDirectory -ErrorAction Stop).ProviderPath
        $command = Get-Command $Executable -CommandType Application -ErrorAction Stop | Select-Object -First 1
        $environmentValues=ConvertTo-PumpEnvironment $Environment
        }finally{Write-OutputPumpTiming 'Native.Resolve' $phase.Elapsed.TotalMilliseconds}
        Write-Debug "[OutputPumps] Buffered capture: executable=$Executable; stdoutLimitCharacters=$CaptureLimit; stderrLimitCharacters=$([Math]::Min($CaptureLimit,262144)); timeoutSeconds=$TimeoutSeconds."
        $capture=[FrenchExDev.OutputPumps.BoundedProcess]::Run($command.Source, $Arguments, $directory, $TimeoutSeconds, $CaptureLimit, $environmentValues)
        Write-Debug "[OutputPumps] Buffered completion: exitCode=$($capture.ExitCode); timedOut=$($capture.TimedOut); truncated=$($capture.Truncated); stdoutCharacters=$($capture.StdoutCharacters); stdoutRetained=$($capture.Stdout.Length); stderrCharacters=$($capture.StderrCharacters); stderrRetained=$($capture.Stderr.Length)."
        Write-OutputPumpCaptureTiming $capture
        $capture
    } catch {
        [pscustomobject]@{ Stdout=''; Stderr=''; StartError=$_.Exception.GetType().Name; ExitCode=$null
            TimedOut=$false; Truncated=$false; StdoutCharacters=0; StderrCharacters=0 }
    }finally{Write-OutputPumpTiming 'Native.BufferedTotal' $total.Elapsed.TotalMilliseconds}
}
