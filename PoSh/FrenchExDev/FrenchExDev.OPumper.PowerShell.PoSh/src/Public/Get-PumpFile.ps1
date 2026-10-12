function Get-PumpFile {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path, [ValidateRange(1,2147483647)][int]$StartLine=1,
          [ValidateRange(1,1000)][int]$LineCount=20, [string]$Filter, [switch]$Tail,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
    $queue=[Collections.Generic.Queue[object]]::new();$total=0;$fieldLimit=$false;$redacted=$false
    $timer=[Diagnostics.Stopwatch]::StartNew();$expired=$false
    try {
        Initialize-PumpCapture
        $file=(Resolve-Path -LiteralPath $Path -ErrorAction Stop).ProviderPath
        $regex=if($Filter){[regex]::new($Filter,[Text.RegularExpressions.RegexOptions]::None,[TimeSpan]::FromSeconds(1))}else{$null}
        $enumerator=[FrenchExDev.OutputPumps.BoundedProcess]::OpenLines($file)
        try { while($enumerator.MoveNext()) {
            $line=$enumerator.Current
            if($timer.Elapsed.TotalSeconds -gt $TimeoutSeconds){$expired=$true;throw 'deadline'}
            if($line.Number -lt $StartLine){continue}
            if(!$Tail -and $line.Number -ge ([long]$StartLine+$LineCount)){break}
            if($line.Length -gt 512){$fieldLimit=$true}
            if($line.Text -eq '[REDACTED: private key]'){$redacted=$true}
            if($regex -and !$regex.IsMatch($line.Text)){continue}
            $total++
            if($Tail -or $queue.Count -lt $MaxItems+1){$queue.Enqueue(@{Line=$line.Number;Text=$line.Text})}
            if($Tail -and $queue.Count -gt $MaxItems){[void]$queue.Dequeue()}
        }} finally {$enumerator.Dispose()}
        $reasons=@();if($fieldLimit){$reasons+='FieldLimit'};if($redacted){$reasons+='SensitiveData'}
        if($Tail -and $total -gt $queue.Count){$reasons+='TailWindow'}
        New-PumpResult -Summary 'File content: selected numbered lines.' -Data @{Path=$file;Items=$queue.ToArray();Window=@{StartLine=$StartLine;LineCount=$(if($Tail){$null}else{$LineCount});Tail=[bool]$Tail}} -TotalCount $total -Truncated:($reasons.Count -gt 0) -Reasons $reasons -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -NextRequest 'Narrow the line range/filter, or lower MaxItems.'
    } catch {
        New-PumpResult -Summary 'File extraction failed.' -Outcome $(if($expired){'Timeout'}else{'Failed'}) -RetryAdvice CorrectRequest -ErrorInfo @{Code=$(if($expired){'Timeout'}else{'ReadOrFilterFailed'});ExceptionType=$_.Exception.GetType().Name;ParserLine=$_.InvocationInfo.ScriptLineNumber;Message=$_.Exception.Message} -MaxOutputBytes $MaxOutputBytes
    }
}
