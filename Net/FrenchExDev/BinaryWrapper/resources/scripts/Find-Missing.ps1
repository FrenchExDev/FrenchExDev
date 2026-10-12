param(
    [switch] $Missing,
    [switch] $List,
    [ValidateSet('net10.0', 'net11.0')]
    [string] $Framework = 'net10.0'
)

$_missing = if ($missing.IsPresent) { "--missing" } else { "" }
$_list = if ($list.IsPresent) { "--list" } else { "" }

$yaml = Get-Content -Raw -Path ./.binary-wrapper.yaml | ConvertFrom-Yaml
dotnet run --project $($yaml.design) --framework $Framework -- $_missing $_list
