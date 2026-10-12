function ConvertFrom-DotnetDiagnostics {
    param([string]$Text)
    $items=[Collections.Generic.List[object]]::new();$seen=[Collections.Generic.HashSet[string]]::new()
    foreach($line in ($Text -split '\r?\n')) {
        if($line -match '^(?<file>.*?)\((?<line>\d+)(?:,\d+)?\):\s*(?<severity>error|warning)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<message>.*?)(?:\s+\[[^\]]+\])?$' -or
           $line -match '^(?<file>.*?)\s*:\s*(?<severity>error|warning)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<message>.*?)(?:\s+\[[^\]]+\])?$') {
            if($seen.Add($line)){$items.Add(@{Path=$Matches.file.Trim();Line=$(if($Matches.ContainsKey('line')){[int]$Matches.line}else{$null});Severity=$Matches.severity;Code=$Matches.code;Message=$Matches.message})}
        }
    }
    return ,$items.ToArray()
}
