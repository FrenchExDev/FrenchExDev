function Get-PumpGitLog {
    [CmdletBinding()]
    param([string]$WorkingDirectory='.', [string[]]$Path=@(), [string]$Revision='HEAD',
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
    if($Revision.StartsWith('-')){return New-PumpResult -Summary 'Invalid Git revision.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='InvalidRevision'} -MaxOutputBytes $MaxOutputBytes}
    $capture=Invoke-PumpProcess git (@('--no-pager','log',"-n$($MaxItems+1)",'--format=%H%x00%aI%x00%s%x00',$Revision,'--')+$Path) $WorkingDirectory $TimeoutSeconds
    Complete-PumpProcess $capture -Summary 'Git history: hashes, dates and subjects.' -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -Parse {
        param($c)
        $p=$c.Stdout.Split([char]0);$items=[Collections.Generic.List[object]]::new()
        for($i=0;$i+2 -lt $p.Count;$i+=3){if(!$p[$i].Trim()){continue};$items.Add(@{Hash=$p[$i].Trim();Date=$p[$i+1];Subject=$p[$i+2]})}
        @{Items=$items.ToArray();_PumpMeta=@{UnknownTotal=($items.Count -gt $MaxItems)}}
    }
}
