<#
.SYNOPSIS
Create a bounded streaming capture for a native process.
.DESCRIPTION
Define Start-OutputPumpStream for the OutputPumps runtime. Resolve the executable and working directory before handing record delivery to the internal C# engine.
#>


function Start-OutputPumpStream {
    <#
    .SYNOPSIS
    Create a bounded streaming capture for a native process.
    .DESCRIPTION
    Resolve the executable and working directory before handing record delivery to the internal C# engine.
    #>
    
    param([string]$Executable,[string[]]$ArgumentList,[string]$WorkingDirectory,[int]$TimeoutSeconds,[int]$MaxRecordCharacters,[char]$RecordDelimiter,[Collections.IDictionary]$Environment)
    $phase=[Diagnostics.Stopwatch]::StartNew()
    try{Initialize-PumpCapture}finally{Write-OutputPumpTiming 'Native.Initialize' $phase.Elapsed.TotalMilliseconds}
    $phase.Restart()
    try {
    $directory=(Resolve-Path -LiteralPath $WorkingDirectory -ErrorAction Stop).ProviderPath
    $command=Get-Command $Executable -CommandType Application -ErrorAction Stop | Select-Object -First 1
    $environmentValues=ConvertTo-PumpEnvironment $Environment
    }finally{Write-OutputPumpTiming 'Native.Resolve' $phase.Elapsed.TotalMilliseconds}
    [FrenchExDev.OutputPumps.StreamingProcess]::new($command.Source,$ArgumentList,$directory,$TimeoutSeconds,$MaxRecordCharacters,$RecordDelimiter,$environmentValues)
}
