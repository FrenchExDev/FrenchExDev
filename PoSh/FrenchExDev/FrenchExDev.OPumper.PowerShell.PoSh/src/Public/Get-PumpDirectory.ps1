function Get-PumpDirectory {
    [CmdletBinding()]
    param([string]$Path='.', [string]$Filter='*', [switch]$Recurse, [switch]$Force,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
    $items=[Collections.Generic.List[object]]::new();$total=0;$timer=[Diagnostics.Stopwatch]::StartNew();$expired=$false
    try {
        Get-ChildItem -LiteralPath $Path -Filter $Filter -Recurse:$Recurse -Force:$Force -ErrorAction Stop | ForEach-Object {
            if($timer.Elapsed.TotalSeconds -gt $TimeoutSeconds){$expired=$true;throw 'deadline'}
            $total++;if($items.Count -lt $MaxItems+1){$items.Add(@{Path=$_.FullName;Directory=$_.PSIsContainer;Bytes=$(if($_.PSIsContainer){$null}else{$_.Length})})}
        }
        New-PumpResult -Summary 'Directory inventory: selected entries.' -Data @{Items=$items.ToArray()} -TotalCount $total -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes
    } catch {
        New-PumpResult -Summary 'Directory inventory incomplete.' -Outcome $(if($expired){'Timeout'}else{'Failed'}) -Data @{Items=$items.ToArray()} -RetryAdvice CorrectRequest -Truncated -Reasons @('CollectionIncomplete') -ErrorInfo @{Code=$(if($expired){'Timeout'}else{'ReadFailed'})} -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes
    }
}
