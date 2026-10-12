function Get-DotnetTestRunner {
    param([string]$WorkingDirectory)
    if($env:DOTNET_TEST_RUNNER -in @('VSTest','Microsoft.Testing.Platform')){return $env:DOTNET_TEST_RUNNER}
    $folder=[IO.DirectoryInfo]::new((Resolve-Path -LiteralPath $WorkingDirectory -ErrorAction Stop).ProviderPath)
    while($null -ne $folder) {
        $global=Join-Path $folder.FullName 'global.json'
        if(Test-Path -LiteralPath $global) {
            $config=Get-Content -LiteralPath $global -Raw -ErrorAction Stop | ConvertFrom-Json -AsHashtable -ErrorAction Stop
            if($config.ContainsKey('test') -and $config.test.ContainsKey('runner')){
                if($config.test.runner -notin @('VSTest','Microsoft.Testing.Platform')){throw 'Unknown test runner'}
                return $config.test.runner
            }
            break
        }
        $folder=$folder.Parent
    }
    return 'VSTest'
}
