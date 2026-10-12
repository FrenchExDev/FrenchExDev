function Get-PumpPodmanLog {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Container, [string]$Since, [string]$Filter, [string]$WorkingDirectory='.',
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600,[Collections.IDictionary]$Environment)
    if($Container.StartsWith('-')){return New-PumpResult -Summary 'Invalid container.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='InvalidContainer'} -MaxOutputBytes $MaxOutputBytes}
    $arguments=@('logs','--timestamps','--tail',($MaxItems+1).ToString());if($Since){$arguments+=@('--since',$Since)};$arguments+=@('--',$Container)
    $capture=Invoke-PumpProcess podman $arguments $WorkingDirectory $TimeoutSeconds -Environment $Environment
    Complete-PumpProcess $capture -Summary 'Podman logs: selected timestamped events in a bounded tail.' -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -Parse {
        param($c)
        $items=[Collections.Generic.List[object]]::new()
        foreach($stream in @('Stdout','Stderr')){$privateKey=$false;foreach($line in ($c.$stream -split '\r?\n')){
            if($line -match '^(\d{4}-\d{2}-\d{2}T\S+)\s+(.*)$'){
                $timestamp=$Matches[1];$message=$Matches[2]
                if($message -match '-----BEGIN .*PRIVATE KEY-----'){$privateKey=$true}
                $keyEnd=$message -match '-----END .*PRIVATE KEY-----'
                if($privateKey){$message='[REDACTED: private key]'}
                if($keyEnd){$privateKey=$false}
                if(!$Filter -or $message -match $Filter){$items.Add(@{Timestamp=$timestamp;Stream=$stream;Message=$message})}
            }
        }}
        @{Items=@($items.ToArray()|Sort-Object Timestamp|Select-Object -Last $MaxItems);Window='Tail';_PumpMeta=@{UnknownTotal=$true;Truncated=$true;Reasons=@('TailWindow');NextRequest='Use Since and a narrower Filter; only the bounded tail was examined.'}}
    }
}
