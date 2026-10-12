function Convert-BoolToJson {
    param(
        [bool] $value
    )
    if ($value) { "true" }else { "false" }
}
