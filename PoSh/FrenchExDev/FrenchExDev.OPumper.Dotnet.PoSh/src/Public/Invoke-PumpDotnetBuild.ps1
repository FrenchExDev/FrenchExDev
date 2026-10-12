function Invoke-PumpDotnetBuild {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Target, [string]$WorkingDirectory='.',
          [string]$Configuration, [string]$Framework, [switch]$NoRestore,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600,[Collections.IDictionary]$Environment)
    if($Target.StartsWith('-')){return New-PumpResult -Summary 'Invalid build target.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='InvalidTarget'} -MaxOutputBytes $MaxOutputBytes}
    $arguments=@('build',$Target,'--nologo','--verbosity','minimal','--tl:off')
    if($Configuration){$arguments+=@('--configuration',$Configuration)};if($Framework){$arguments+=@('--framework',$Framework)};if($NoRestore){$arguments+='--no-restore'}
    $capture=Invoke-PumpProcess dotnet $arguments $WorkingDirectory $TimeoutSeconds -Environment $Environment
    Complete-PumpProcess $capture -Summary '.NET build: outcome and compiler diagnostics.' -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -RetryAdvice Unknown -Parse {
        param($c)
        $diagnostics=ConvertFrom-DotnetDiagnostics ($c.Stdout+"`n"+$c.Stderr)
        @{Items=$diagnostics;Errors=@($diagnostics|Where-Object Severity -eq error).Count;Warnings=@($diagnostics|Where-Object Severity -eq warning).Count}
    }
}
