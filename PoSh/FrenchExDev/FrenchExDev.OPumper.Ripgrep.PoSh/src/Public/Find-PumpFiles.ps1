function Find-PumpFiles {
    [CmdletBinding()]
    param([string[]]$Path=@('.'), [string[]]$Glob=@(), [string]$WorkingDirectory='.',
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600,[Collections.IDictionary]$Environment)
    $arguments=@('--files','--null');foreach($g in $Glob){$arguments+=@('--glob',$g)};$arguments+=@('--')+$Path
    $state=@{Items=[Collections.Generic.List[object]]::new();Count=0L;MaxItems=$MaxItems}
    Invoke-OutputPumpNative -Executable rg -ArgumentList $arguments -WorkingDirectory $WorkingDirectory -TimeoutSeconds $TimeoutSeconds -Environment $Environment -Summary 'File discovery: selected paths.' -SuccessCodes @(0,1) -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -RecordDelimiter ([char]0) -State $state -OnRecord {
        param($record,$state)
        if($record.Stream -ne 'stdout' -or $record.Truncated -or !$record.Text){return}
        $state.Count++
        if($state.Items.Count -lt $state.MaxItems){$state.Items.Add(@{Path=$record.Text})}
    } -Parse {
        param($state,$capture)
        $complete=$capture.ExitCode -in @(0,1) -and $capture.OmittedRecords -eq 0
        $meta=@{TotalCount=$state.Count;Truncated=($state.Count -gt $state.Items.Count);Reasons=@('ItemLimit')}
        if(!$meta.Truncated){$meta.Reasons=@()}
        if(!$complete){$meta.TotalCount=$null;$meta.UnknownTotal=$true;$meta.Truncated=$true;$meta.Reasons+=@('SearchErrors')}
        @{Items=$state.Items.ToArray();FileCount=$(if($complete){$state.Count}else{$null});ObservedFileCount=$state.Count;Complete=$complete;_PumpMeta=$meta}
    }
}
