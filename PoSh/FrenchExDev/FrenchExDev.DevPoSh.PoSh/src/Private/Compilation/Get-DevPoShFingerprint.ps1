function Get-DevPoShFingerprint {
    param([string]$Text,[Collections.IDictionary]$Assets=@{})
    $builder=[Text.StringBuilder]::new($Text)
    foreach($name in @($Assets.Keys | Sort-Object)) {
        [void]$builder.Append("`n$name`n")
        [void]$builder.Append([Convert]::ToBase64String([byte[]]$Assets[$name]))
    }
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($builder.ToString()))).ToLowerInvariant()
}
