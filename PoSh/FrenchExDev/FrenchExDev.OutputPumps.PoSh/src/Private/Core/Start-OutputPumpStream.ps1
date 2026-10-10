function Start-OutputPumpStream {
    param([string]$Executable,[string[]]$ArgumentList,[string]$WorkingDirectory,[int]$TimeoutSeconds,[int]$MaxRecordCharacters,[char]$RecordDelimiter)
    Initialize-PumpCapture
    $directory=(Resolve-Path -LiteralPath $WorkingDirectory -ErrorAction Stop).ProviderPath
    $command=Get-Command $Executable -CommandType Application -ErrorAction Stop | Select-Object -First 1
    [FrenchExDev.OutputPumps.StreamingProcess]::new($command.Source,$ArgumentList,$directory,$TimeoutSeconds,$MaxRecordCharacters,$RecordDelimiter)
}
