function Get-OutputPumpRegistry {
    param([string]$ConfigurationPath)
    if(!$script:OutputPumpSession.Initialized -or $script:OutputPumpSession.LastError){throw 'OutputPumps registry is not initialized. Run Setup-OutputPumps.'}
    if($ConfigurationPath -and [IO.Path]::GetFullPath($ConfigurationPath) -ne $script:OutputPumpSession.ConfigurationPath){throw 'Run Setup-OutputPumps to change configuration.'}
    foreach($stack in $script:OutputPumpSession.Registry.Values){if($stack.Count){$entry=$stack[$stack.Count-1];if(!$script:OutputPumpSession.Configuration.Pumps.ContainsKey($entry.Id) -or $script:OutputPumpSession.Configuration.Pumps[$entry.Id]){$entry}}}
}
