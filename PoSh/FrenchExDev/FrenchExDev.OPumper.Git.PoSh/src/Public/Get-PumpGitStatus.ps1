function Get-PumpGitStatus {
    [CmdletBinding()]
    param([string]$WorkingDirectory='.', [string[]]$Path=@(),
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600,[Collections.IDictionary]$Environment)
    $arguments=@('--no-optional-locks','status','--porcelain=v2','--branch','-z','--untracked-files=all','--') + $Path
    $state=@{Branch=$null;Upstream=$null;Ahead=$null;Behind=$null;Head=$null;
        Items=[Collections.Generic.List[object]]::new();Count=0L;MaxItems=$MaxItems;PendingRename=$null}
    Write-Debug "[OutputPumps] Git status: provider=$PSCommandPath; mode=Streaming; maxItems=$MaxItems; pathFilters=$($Path.Count)."
    Invoke-OutputPumpNative -Executable git -ArgumentList $arguments -WorkingDirectory $WorkingDirectory -TimeoutSeconds $TimeoutSeconds -Environment $Environment -Summary 'Git status: branch and grouped changed paths.' -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -RecordDelimiter ([char]0) -State $state -OnRecord {
        param($record,$state)
        if($record.Stream -ne 'stdout'){return}
        if($record.Truncated){$state.PendingRename=$null;return}
        $r=$record.Text
        if($state.PendingRename) {
            if(!$r){throw 'missing rename source'}
            $item=$state.PendingRename;$item.OriginalPath=$r;$state.PendingRename=$null
        } else {
            if(!$r){return}
            if($r -match '^# branch\.(\w+) (.*)$') {
                switch($Matches[1]) {
                    'head' {$state.Branch=$Matches[2]}
                    'oid' {$state.Head=$Matches[2]}
                    'upstream' {$state.Upstream=$Matches[2]}
                    'ab' {if($Matches[2] -match '^\+(\d+) -(\d+)$'){$state.Ahead=[int]$Matches[1];$state.Behind=[int]$Matches[2]}}
                }; return
            }
            $groups=@();$xy=$null
            switch($r.Substring(0,1)) {
                '?' {$pathName=$r.Substring(2);$groups=@('Untracked')}
                '!' {$pathName=$r.Substring(2);$groups=@('Ignored')}
                '1' {$p=$r -split ' ',9;if($p.Count -ne 9){throw 'status record'};$xy=$p[1];$pathName=$p[8]}
                '2' {$p=$r -split ' ',10;if($p.Count -ne 10){throw 'rename record'};$xy=$p[1];$pathName=$p[9]}
                'u' {$p=$r -split ' ',11;if($p.Count -ne 11){throw 'conflict record'};$xy=$p[1];$pathName=$p[10];$groups=@('Conflict')}
                default {throw 'unknown status record'}
            }
            if($xy -and $groups.Count -eq 0) {
                if($xy[0] -ne '.'){$groups+='Staged'}
                if($xy[1] -ne '.'){$groups+='Unstaged'}
            }
            $item=@{Path=$pathName;OriginalPath=$null;Groups=$groups;Status=$xy}
            if($r[0] -eq '2'){$state.PendingRename=$item;return}
        }
        $state.Count++
        if($state.Items.Count -lt $state.MaxItems){$state.Items.Add($item)}
    } -Parse {
        param($state,$capture)
        if($state.PendingRename){throw 'missing rename source'}
        $meta=@{TotalCount=$state.Count}
        if($state.Count -gt $state.Items.Count){$meta.Truncated=$true;$meta.Reasons=@('ItemLimit')}
        @{Branch=$state.Branch;Upstream=$state.Upstream;Ahead=$state.Ahead;Behind=$state.Behind;Head=$state.Head;
            Items=$state.Items.ToArray();_PumpMeta=$meta}
    }
}
