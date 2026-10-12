function ConvertTo-PumpEnvironment {
    <#
    .SYNOPSIS
    Copy child-only environment overrides into the native process dictionary.
    .DESCRIPTION
    Preserve null removals, reject invalid entries without revealing their contents, and never mutate the parent environment.
    #>
    param([Collections.IDictionary]$Environment)
    $comparer=if([OperatingSystem]::IsWindows()){[StringComparer]::OrdinalIgnoreCase}else{[StringComparer]::Ordinal}
    $values=[Collections.Generic.Dictionary[string,string]]::new($comparer)
    if($null -ne $Environment) {
        foreach($key in $Environment.Keys) {
            if($key -isnot [string] -or !$key -or $key.IndexOfAny([char[]]@('=',[char]0)) -ge 0){throw [ArgumentException]::new('Invalid process environment entry.')}
            # PowerShell otherwise coerces null to an empty string for Dictionary.Add.
            if($null -eq $Environment[$key]){$values.Add($key,[NullString]::Value);continue}
            $value=[string]$Environment[$key]
            if($value.IndexOf([char]0) -ge 0){throw [ArgumentException]::new('Invalid process environment entry.')}
            $values.Add($key,$value)
        }
    }
    return ,$values
}
