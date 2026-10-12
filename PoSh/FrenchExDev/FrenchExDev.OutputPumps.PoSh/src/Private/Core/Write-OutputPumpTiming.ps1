function Write-OutputPumpTiming {
    <#
    .SYNOPSIS
    Report a monotonic action duration on the debug stream.
    .DESCRIPTION
    Emit stable millisecond fields and aggregate counts without business data or process arguments.
    #>
    param([string]$Action,[double]$ElapsedMilliseconds,[long]$Count=1)
    $elapsed=$ElapsedMilliseconds.ToString('F3',[Globalization.CultureInfo]::InvariantCulture)
    $average=$(if($Count -gt 0){$ElapsedMilliseconds/$Count}else{0}).ToString('F3',[Globalization.CultureInfo]::InvariantCulture)
    Write-Debug "[OutputPumps] Timing: action=$Action; elapsedMs=$elapsed; count=$Count; averageMs=$average."
}
