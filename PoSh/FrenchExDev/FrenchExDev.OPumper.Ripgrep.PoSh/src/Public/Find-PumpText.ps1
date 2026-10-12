function Find-PumpText {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Pattern, [string[]]$Path=@('.'), [string[]]$Glob=@(),
          [string]$WorkingDirectory='.', [switch]$Literal, [switch]$IgnoreCase,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600,[Collections.IDictionary]$Environment)
    $arguments=@('--json','--color=never');if($Literal){$arguments+='--fixed-strings'};if($IgnoreCase){$arguments+='--ignore-case'}
    foreach($g in $Glob){$arguments+=@('--glob',$g)};$arguments+=@('--',$Pattern)+$Path
    $state=@{Items=[Collections.Generic.List[object]]::new();Count=0L;EncodingOmitted=$false;MaxItems=$MaxItems}
    Invoke-OutputPumpNative -Executable rg -ArgumentList $arguments -WorkingDirectory $WorkingDirectory -TimeoutSeconds $TimeoutSeconds -Environment $Environment -Summary 'Text search: paths, line numbers and selected matches.' -SuccessCodes @(0,1) -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -State $state -OnRecord {
        param($record,$state)
        if($record.Stream -ne 'stdout' -or $record.Truncated -or !$record.Text){return}
        $event=ConvertFrom-Json -InputObject $record.Text -AsHashtable -ErrorAction Stop
        if($event.type -eq 'match') {
            $state.Count++
            if(!$event.data.path.ContainsKey('text') -or !$event.data.lines.ContainsKey('text')){$state.EncodingOmitted=$true;return}
            if($state.Items.Count -lt $state.MaxItems){$state.Items.Add(@{Path=$event.data.path.text;Line=$event.data.line_number;Text=$event.data.lines.text.TrimEnd("`r","`n")})}
        }
    } -Parse {
        param($state,$capture)
        $meta=@{TotalCount=$state.Count}
        $reasons=[Collections.Generic.List[string]]::new()
        if($state.Count -gt $state.Items.Count){$meta.Truncated=$true;$reasons.Add('ItemLimit')}
        if($state.EncodingOmitted){$meta.Truncated=$true;$reasons.Add('UnsupportedEncoding')}
        if($capture.ExitCode -eq 2){$meta.Truncated=$true;$reasons.Add('SearchErrors');$meta.TotalCount=$null;$meta.UnknownTotal=$true}
        $meta.Reasons=$reasons.ToArray()
        $complete=$capture.ExitCode -in @(0,1) -and $capture.OmittedRecords -eq 0
        @{Items=$state.Items.ToArray();MatchLines=$(if($complete){$state.Count}else{$null});ObservedMatchLines=$state.Count;Complete=$complete;_PumpMeta=$meta}
    }
}
