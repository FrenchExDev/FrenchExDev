function New-PumpResult {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Summary,
          [ValidateSet('Success','Failed','Timeout','Unsupported')][string]$Outcome='Success',
          [System.Collections.IDictionary]$Data=@{}, $ExitCode=$null, $ErrorInfo=$null,
          [ValidateSet('SafeToRetry','NarrowRequest','CorrectRequest','Unknown')][string]$RetryAdvice='SafeToRetry',
          [ValidateRange(1,1000)][int]$MaxItems=20,
          [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [switch]$Truncated, [string[]]$Reasons=@(), $TotalCount=$null, [switch]$UnknownTotal,
          [string]$NextRequest='Specify a narrower path/filter or a lower MaxItems.')
    $state = @{ Omitted=$false; Redacted=$false; Secrets=@(Get-ChildItem Env: | Where-Object {
        $_.Name -match '(?i)password|passwd|secret|token|api_?key|connectionstring' -and $_.Value.Length -ge 4
    } | ForEach-Object Value) }
    $safe = Protect-PumpValue $Data $state
    $reasonList = [Collections.Generic.List[string]]::new()
    foreach ($reason in $Reasons) { if ($reason -and !$reasonList.Contains($reason)) { $reasonList.Add($reason) } }
    if (-not $safe.Contains('Items')) { $safe['Items']=@() }
    $items = @($safe['Items'])
    if ($null -eq $TotalCount -and -not $Truncated -and -not $UnknownTotal) { $TotalCount=$items.Count }
    if ($items.Count -gt $MaxItems) { $safe['Items']=@($items | Select-Object -First $MaxItems); $reasonList.Add('ItemLimit'); $Truncated=$true }
    $safeSummary = (Protect-PumpValue $Summary $state) -replace '[\r\n]+',' '
    $safeError = Protect-PumpValue $ErrorInfo $state
    $safeNext = Protect-PumpValue $NextRequest $state
    if ($state.Omitted) { $Truncated=$true; $reasonList.Add('FieldLimit') }
    if ($state.Redacted) { $Truncated=$true; $reasonList.Add('SensitiveData') }
    $result = [pscustomobject][ordered]@{
        Summary=$safeSummary; Outcome=$Outcome; Data=$safe; ExitCode=$ExitCode; Error=$safeError
        RetryAdvice=$RetryAdvice
        Truncation=[pscustomobject]@{ Applied=[bool]$Truncated; Reasons=$reasonList.ToArray()
            ReturnedCount=@($safe['Items']).Count; TotalCount=$TotalCount
            NextRequest=$(if($Truncated){$safeNext}else{$null}) }
    }
    while ([Text.Encoding]::UTF8.GetByteCount((ConvertTo-Json -InputObject $result -Depth 24 -Compress)) -gt $MaxOutputBytes) {
        if (!$reasonList.Contains('OutputBudget')) { $reasonList.Add('OutputBudget') }
        $result.Truncation.Applied=$true; $result.Truncation.Reasons=$reasonList.ToArray()
        $result.Truncation.NextRequest=$safeNext
        $remaining = @($result.Data['Items'])
        if ($remaining.Count -gt 0) { $result.Data['Items']=@($remaining | Select-Object -First ($remaining.Count-1)) }
        else {
            $result.Data=[ordered]@{ Items=@() }
            $result.Summary=$result.Summary.Substring(0,[Math]::Min(120,$result.Summary.Length))
            if ($null -ne $result.Error) { $result.Error=[ordered]@{Code='DetailsOmitted';Message='Error details omitted to fit output budget.'} }
            $result.Truncation.NextRequest='Narrow the path/filter and lower MaxItems.'
        }
        $result.Truncation.ReturnedCount=@($result.Data['Items']).Count
    }
    if ($result.Truncation.Applied -and $result.Outcome -eq 'Success') { $result.RetryAdvice='NarrowRequest' }
    return $result
}
