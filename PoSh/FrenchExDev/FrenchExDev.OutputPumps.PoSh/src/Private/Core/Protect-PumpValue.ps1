function Protect-PumpValue {
    param($Value, [hashtable]$State, [int]$Depth = 0)
    if ($null -eq $Value) { return $null }
    if ($Depth -gt 12) { $State.Omitted=$true; return '[omitted: depth]' }
    if ($Value -is [string]) {
        $s = $Value -replace '\x1b\[[0-?]*[ -/]*[@-~]', ''
        $original = $s
        foreach ($secret in $State.Secrets) { $s = $s.Replace($secret, '[REDACTED]', [StringComparison]::Ordinal) }
        $s = $s -replace '(?is)-----BEGIN [^-]*PRIVATE KEY-----.*?(?:-----END [^-]*PRIVATE KEY-----|$)', '[REDACTED: private key]'
        $s = $s -replace '(?i)\b(?:Bearer|Basic)\s+[A-Za-z0-9._~+/=-]+', '[REDACTED: authorization]'
        $s = $s -replace '(?i)\b(?:gh[pousr]_[A-Za-z0-9_]+|github_pat_[A-Za-z0-9_]+|sk-[A-Za-z0-9_-]{12,}|AKIA[A-Z0-9]{16})\b', '[REDACTED: token]'
        $s = $s -replace '(?i)\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+', '[REDACTED: jwt]'
        $s = $s -replace '(?i)(https?://)[^\s/@:]+:[^\s/@]+@', '$1[REDACTED]@'
        $s = $s -replace '(?i)((?:[\w-]*(?:password|passwd|secret|token|api[_-]?key|access[_-]?key|private[_-]?key|credential|authorization|connectionstring)[\w-]*|pwd)\s*["'']?\s*[:=]\s*)(?:"[^"]*"|''[^'']*''|[^\s,;&]+)', '$1[REDACTED]'
        if ($s -ne $original -or $s.Contains('[REDACTED')) { $State.Redacted=$true }
        if ($s.Length -gt 512) { $State.Omitted=$true; $s=$s.Substring(0,512) }
        return $s
    }
    if ($Value -is [System.Collections.IDictionary]) {
        $copy = [ordered]@{}
        foreach ($key in $Value.Keys) {
            $safeKey = Protect-PumpValue ([string]$key) $State ($Depth+1)
            if ([string]$key -match '(?i)password|passwd|secret|token|api[_-]?key|access[_-]?key|private[_-]?key|credential|authorization|connectionstring|^(pwd|environment|env)$') {
                $State.Redacted=$true; $copy[$safeKey]='[REDACTED]'
            } else { $copy[$safeKey]=Protect-PumpValue $Value[$key] $State ($Depth+1) }
        }
        return $copy
    }
    if ($Value -is [System.Collections.IEnumerable]) {
        $list = [Collections.Generic.List[object]]::new()
        foreach ($item in $Value) {
            if ($list.Count -ge 1000) { $State.Omitted=$true; break }
            $list.Add((Protect-PumpValue $item $State ($Depth+1)))
        }
        return ,$list.ToArray()
    }
    if ($Value -is [ValueType]) { return $Value }
    if ($Value -is [pscustomobject]) {
        $map = [ordered]@{}
        foreach ($property in $Value.PSObject.Properties) { $map[$property.Name]=$property.Value }
        return Protect-PumpValue $map $State ($Depth+1)
    }
    $State.Omitted=$true
    return '[omitted: unsupported type]'
}
