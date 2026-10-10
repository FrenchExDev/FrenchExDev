function Get-PumpGitStatus {
    [CmdletBinding()]
    param([string]$WorkingDirectory='.', [string[]]$Path=@(),
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
    $arguments=@('--no-optional-locks','status','--porcelain=v2','--branch','-z','--untracked-files=all','--') + $Path
    $capture=Invoke-PumpProcess git $arguments $WorkingDirectory $TimeoutSeconds
    Complete-PumpProcess $capture -Summary 'Git status: branch and grouped changed paths.' -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -Parse {
        param($c)
        $data=@{Branch=$null;Upstream=$null;Ahead=$null;Behind=$null;Head=$null;Items=@()}
        $records=$c.Stdout.Split([char]0); $items=[Collections.Generic.List[object]]::new()
        for($i=0;$i -lt $records.Count;$i++) {
            $r=$records[$i]; if(!$r){continue}
            if($r -match '^# branch\.(\w+) (.*)$') {
                switch($Matches[1]) {
                    'head' {$data.Branch=$Matches[2]}
                    'oid' {$data.Head=$Matches[2]}
                    'upstream' {$data.Upstream=$Matches[2]}
                    'ab' {if($Matches[2] -match '^\+(\d+) -(\d+)$'){$data.Ahead=[int]$Matches[1];$data.Behind=[int]$Matches[2]}}
                }; continue
            }
            $original=$null; $groups=@(); $xy=$null
            switch($r.Substring(0,1)) {
                '?' {$pathName=$r.Substring(2);$groups=@('Untracked')}
                '!' {$pathName=$r.Substring(2);$groups=@('Ignored')}
                '1' {$p=$r -split ' ',9;if($p.Count -ne 9){throw 'status record'};$xy=$p[1];$pathName=$p[8]}
                '2' {$p=$r -split ' ',10;if($p.Count -ne 10 -or $i+1 -ge $records.Count){throw 'rename record'};$xy=$p[1];$pathName=$p[9];$i++;$original=$records[$i]}
                'u' {$p=$r -split ' ',11;if($p.Count -ne 11){throw 'conflict record'};$xy=$p[1];$pathName=$p[10];$groups=@('Conflict')}
                default {throw 'unknown status record'}
            }
            if($xy -and $groups.Count -eq 0) {
                if($xy[0] -ne '.'){$groups+='Staged'}
                if($xy[1] -ne '.'){$groups+='Unstaged'}
            }
            $items.Add(@{Path=$pathName;OriginalPath=$original;Groups=$groups;Status=$xy})
        }
        $data.Items=$items.ToArray(); $data
    }
}
