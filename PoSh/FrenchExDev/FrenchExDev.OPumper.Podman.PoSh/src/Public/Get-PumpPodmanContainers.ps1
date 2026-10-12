function Get-PumpPodmanContainers {
    [CmdletBinding()]
    param([string]$WorkingDirectory='.', [string[]]$Filter=@(), [switch]$All,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600,[Collections.IDictionary]$Environment)
    $arguments=@('ps','--format','json');if($All){$arguments+='--all'};foreach($f in $Filter){$arguments+=@('--filter',$f)}
    $capture=Invoke-PumpProcess podman $arguments $WorkingDirectory $TimeoutSeconds -Environment $Environment
    Complete-PumpProcess $capture -Summary 'Podman containers: identity, image and state.' -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -Parse {
        param($c)
        if($c.ExitCode -ne 0 -and !$c.Stdout.Trim()){return @{Items=@()}}
        $rows=ConvertFrom-Json -InputObject $c.Stdout -AsHashtable -ErrorAction Stop
        if($c.Stdout.TrimStart()[0] -ne '['){throw 'Expected array'}
        @{Items=@(foreach($raw in $rows){$r=@{};foreach($key in $raw.Keys){$r[$key]=$raw[$key]};if(!$r.ContainsKey('Id') -or !$r.ContainsKey('Names')){throw 'Unknown container format'};@{Id=$r['Id'];Names=$r['Names'];Image=$r['Image'];State=$r['State'];Status=$r['Status'];ExitCode=$r['ExitCode']}})}
    }
}
