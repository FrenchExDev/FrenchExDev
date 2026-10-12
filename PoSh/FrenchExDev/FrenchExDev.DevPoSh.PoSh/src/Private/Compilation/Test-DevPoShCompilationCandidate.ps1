function Test-DevPoShCompilationCandidate {
    param([Parameter(Mandatory)][string]$Path,[ValidatePattern('^[A-Za-z]+-[A-Za-z0-9]+$')][string]$ValidationFunction)
    $syntaxTokens=$null; $syntaxErrors=$null
    $null=[Management.Automation.Language.Parser]::ParseFile($Path,[ref]$syntaxTokens,[ref]$syntaxErrors)
    if($syntaxErrors.Count){throw 'Generated module has syntax errors.'}
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Path))
    $validation=if($ValidationFunction){"& `$module { & '$ValidationFunction' }; "}else{''}
    $probe="try { `$module=Import-Module ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('$encoded'))) -PassThru -DisableNameChecking -ErrorAction Stop; $validation exit 0 } catch { exit 1 }"
    $psi=[Diagnostics.ProcessStartInfo]::new((Join-Path $PSHOME $(if($IsWindows){'pwsh.exe'}else{'pwsh'})))
    $psi.UseShellExecute=$false; $psi.CreateNoWindow=$true; $psi.RedirectStandardOutput=$true; $psi.RedirectStandardError=$true
    foreach($arg in @('-NoLogo','-NoProfile','-NonInteractive','-Command',$probe)){[void]$psi.ArgumentList.Add($arg)}
    $process=[Diagnostics.Process]::new(); $process.StartInfo=$psi
    try {
        $null=$process.Start()
        $stdout=$process.StandardOutput.BaseStream.CopyToAsync([IO.Stream]::Null)
        $stderr=$process.StandardError.BaseStream.CopyToAsync([IO.Stream]::Null)
        if(!$process.WaitForExit(30000)){$process.Kill($true);throw 'Compilation validation expired.'}
        [Threading.Tasks.Task]::WaitAll(@($stdout,$stderr),5000) | Out-Null
        if($process.ExitCode -ne 0){throw 'Generated module failed isolated import validation.'}
    } finally {$process.Dispose()}
}
