function Invoke-PumpProcess {
    <# Internal primitive. Its capture is never the agent-facing result. Use a software pump. #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Executable, [string[]]$Arguments = @(),
          [string]$WorkingDirectory = '.', [ValidateRange(1,86400)][int]$TimeoutSeconds = 600,
          [ValidateRange(1024,4194304)][int]$CaptureLimit = 4194304)
    try {
        Initialize-PumpCapture
        $directory = (Resolve-Path -LiteralPath $WorkingDirectory -ErrorAction Stop).ProviderPath
        $command = Get-Command $Executable -CommandType Application -ErrorAction Stop | Select-Object -First 1
        [FrenchExDev.OutputPumps.BoundedProcess]::Run($command.Source, $Arguments, $directory, $TimeoutSeconds, $CaptureLimit)
    } catch {
        [pscustomobject]@{ Stdout=''; Stderr=''; StartError=$_.Exception.GetType().Name; ExitCode=$null
            TimedOut=$false; Truncated=$false; StdoutCharacters=0; StderrCharacters=0 }
    }
}
