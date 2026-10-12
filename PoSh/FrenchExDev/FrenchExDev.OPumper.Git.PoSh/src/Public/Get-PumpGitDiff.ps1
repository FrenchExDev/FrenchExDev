function Get-PumpGitDiff {
    [CmdletBinding()]
    param([string]$WorkingDirectory='.', [string[]]$Path=@(), [switch]$Staged, [string]$Revision,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600,[Collections.IDictionary]$Environment)
    $arguments=@('--no-pager','diff','--no-ext-diff','--no-textconv','--numstat','-z')
    if($Staged){$arguments+='--cached'}
    if($Revision){if($Revision.StartsWith('-')){return New-PumpResult -Summary 'Invalid Git revision.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='InvalidRevision'} -MaxOutputBytes $MaxOutputBytes};$arguments+=$Revision}
    $arguments+=@('--')+$Path
    $capture=Invoke-PumpProcess git $arguments $WorkingDirectory $TimeoutSeconds -Environment $Environment
    Complete-PumpProcess $capture -Summary 'Git diff: per-path additions and deletions.' -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -Parse {
        param($c)
        $records=$c.Stdout.Split([char]0);$items=[Collections.Generic.List[object]]::new()
        for($i=0;$i -lt $records.Count;$i++) {
            if(!$records[$i]){continue}
            $p=$records[$i] -split "`t",3;if($p.Count -ne 3){throw 'numstat'}
            $name=$p[2];$old=$null
            if(!$name){if($i+2 -ge $records.Count){throw 'rename'};$i++;$old=$records[$i];$i++;$name=$records[$i]}
            $binary=$p[0] -eq '-';$items.Add(@{Path=$name;OriginalPath=$old;Binary=$binary;Added=$(if($binary){$null}else{[int]$p[0]});Deleted=$(if($binary){$null}else{[int]$p[1]})})
        }
        @{Items=$items.ToArray()}
    }
}
