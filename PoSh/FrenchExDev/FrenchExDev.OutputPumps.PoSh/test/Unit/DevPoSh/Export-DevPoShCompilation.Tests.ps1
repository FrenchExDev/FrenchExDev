BeforeAll { . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:DevLibrary 'DevPoSh.PoSh.psd1') -Force -DisableNameChecking }
Describe 'Export-DevPoShCompilation' {
    BeforeEach { Mock Test-DevPoShCompilationCandidate -ModuleName 'DevPoSh.PoSh' {} }
    It 'publishes assets, then avoids rewriting identical artifacts' {
        $path=Join-Path $TestDrive 'module.psm1';$code='function Get-Example {42}; Export-ModuleMember Get-Example'
        $assets=@{'.assets/fixture/value.txt'=[Text.Encoding]::UTF8.GetBytes('resource')}
        (Export-DevPoShCompilation $code $path -Assets $assets).Changed | Should -BeTrue
        $before=(Get-Item $path).LastWriteTimeUtc
        (Export-DevPoShCompilation $code $path -Assets $assets).Changed | Should -BeFalse
        (Get-Item $path).LastWriteTimeUtc | Should -Be $before
        Should -Invoke Test-DevPoShCompilationCandidate -ModuleName 'DevPoSh.PoSh' -Times 1 -Exactly
    }
    It 'keeps the last good artifact when validation fails' {
        $path=Join-Path $TestDrive 'preserved.psm1';[IO.File]::WriteAllText($path,'previous')
        Mock Test-DevPoShCompilationCandidate -ModuleName 'DevPoSh.PoSh' {throw 'invalid'}
        (Export-DevPoShCompilation 'broken' $path).Outcome | Should -Be Failed
        [IO.File]::ReadAllText($path) | Should -Be previous
    }
    It 'rejects escaping resources and concurrent writes before validation' {
        $path=Join-Path $TestDrive 'locked.psm1'
        (Export-DevPoShCompilation 'valid' $path -Assets @{'../escape'=[byte[]]@(1)}).Outcome | Should -Be Failed
        $lock=[IO.File]::Open("$path.lock",'OpenOrCreate','ReadWrite','None')
        try{(Export-DevPoShCompilation 'valid' $path).Outcome | Should -Be Failed}finally{$lock.Dispose()}
        Should -Invoke Test-DevPoShCompilationCandidate -ModuleName 'DevPoSh.PoSh' -Times 0
    }
}
