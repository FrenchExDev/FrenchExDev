function Write-OutputPumpCaptureTiming {
    <#
    .SYNOPSIS
    Publish native lifecycle timings without publishing the capture.
    .DESCRIPTION
    Report process setup, startup, concurrent drains, completion and total time when the capture supplies them.
    #>
    param($Capture)
    foreach($phase in @('Setup','Start','StdoutDrain','StderrDrain','Run','Elapsed')) {
        $name=$phase+'Milliseconds'
        if($Capture.PSObject.Properties[$name]){Write-OutputPumpTiming -Action ('Native.'+$phase) -ElapsedMilliseconds $Capture.$name}
    }
}
