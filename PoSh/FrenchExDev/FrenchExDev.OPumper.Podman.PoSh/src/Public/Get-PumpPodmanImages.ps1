function Get-PumpPodmanImages {
    [CmdletBinding()]
    param([string]$WorkingDirectory='.', [string[]]$Filter=@(),
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600,[Collections.IDictionary]$Environment)
    $arguments=@('images','--format','json');foreach($f in $Filter){$arguments+=@('--filter',$f)}
    $capture=Invoke-PumpProcess podman $arguments $WorkingDirectory $TimeoutSeconds -Environment $Environment
    Complete-PumpProcess $capture -Summary 'Podman images: identity, names and size.' -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -Parse {
        param($c)
        if($c.ExitCode -ne 0 -and !$c.Stdout.Trim()){return @{Items=@()}}
        $rows=ConvertFrom-Json -InputObject $c.Stdout -AsHashtable -ErrorAction Stop
        if($c.Stdout.TrimStart()[0] -ne '['){throw 'Expected array'}
        @{Items=@(foreach($raw in $rows){$r=@{};foreach($key in $raw.Keys){$r[$key]=$raw[$key]};if(!$r.ContainsKey('Id')){throw 'Unknown image format'};@{Id=$r['Id'];Names=$r['Names'];Size=$r['Size'];Created=$r['Created']}})}
    }
}
