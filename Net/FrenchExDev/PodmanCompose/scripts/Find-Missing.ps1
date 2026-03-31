param(
    [switch] $Missing,
    [switch] $List
)

$_missing = if($missing.IsPresent) { "--missing" } else { "" }
$_list = if($list.IsPresent) { "--list" } else { "" }

$yaml = Get-Content -Raw -Path ./.binary-wrapper.yaml | ConvertFrom-Yaml
dotnet run --project $($yaml.design) -- $_missing $_list