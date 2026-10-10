function ConvertFrom-OutputPumpArguments {
    param($Route,[string[]]$Arguments)
    $parameters = @{}
    $positionals = [Collections.Generic.List[string]]::new()
    $afterSeparator = $false
    for ($i = 0; $i -lt $Arguments.Count; $i++) {
        $argument = $Arguments[$i]
        if (!$afterSeparator -and $argument -eq '--') { $afterSeparator = $true; continue }
        if (!$afterSeparator -and $argument.StartsWith('-')) {
            $parts = $argument -split '=',2
            if (!$Route.Options.Contains($parts[0])) { throw 'Unsupported native option.' }
            $option = $Route.Options[$parts[0]]
            if ($option.Contains('Switch') -and $option.Switch) {
                if ($parts.Count -gt 1) { throw 'Switch does not accept a value.' }
                $parameters[$option.Parameter] = $true
            } else {
                $value = if ($parts.Count -gt 1) { $parts[1] } elseif (++$i -lt $Arguments.Count) { $Arguments[$i] } else { throw 'Missing native option value.' }
                if ($option.Contains('Integer') -and $option.Integer) { $value = [int]$value }
                if ($option.Contains('Multiple') -and $option.Multiple) {
                    $previous=@();if($parameters.ContainsKey($option.Parameter)){$previous=@($parameters[$option.Parameter])}
                    $parameters[$option.Parameter]=$previous+@($value)
                }
                else { $parameters[$option.Parameter] = $value }
            }
        } else { $positionals.Add($argument) }
    }
    if ($Route.Contains('FirstPositional') -and $Route.FirstPositional -and $positionals.Count) {
        $parameters[$Route.FirstPositional]=$positionals[0]
        $positionals.RemoveAt(0)
    }
    if ($positionals.Count) {
        if (!$Route.Positional) { throw 'Unexpected native positional argument.' }
        if (!$Route.Multiple -and $positionals.Count -gt 1) { throw 'Too many native positional arguments.' }
        $parameters[$Route.Positional] = if ($Route.Multiple) { $positionals.ToArray() } else { $positionals[0] }
    }
    return $parameters
}
