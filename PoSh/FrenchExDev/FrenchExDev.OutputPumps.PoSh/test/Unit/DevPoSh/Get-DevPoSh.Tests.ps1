<#
.SYNOPSIS
Unit tests for Get-DevPoSh.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll {
    . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:DevLibrary 'DevPoSh.PoSh.psd1') -Force -DisableNameChecking
    $script:PreviousRoot=$env:FRENCHEXDEV_MYDEVPOSH_PATH
}
AfterAll { $env:FRENCHEXDEV_MYDEVPOSH_PATH=$script:PreviousRoot }
Describe 'Get-DevPoSh' {
    BeforeEach {
        $env:FRENCHEXDEV_MYDEVPOSH_PATH=$TestDrive
        Mock Get-DevPoShProjects -ModuleName 'DevPoSh.PoSh' { [pscustomobject]@{Name='Example.PoSh';Path='C:/fixture/Example.PoSh';Acme='Fixture';Manifest='C:/fixture/Example.PoSh/Example.PoSh.psd1';Data=@{}} }
        Mock New-DevPoShProjectBundle -ModuleName 'DevPoSh.PoSh' { [pscustomobject]@{Code='compiled';Assets=@{}} }
        Mock Export-DevPoShCompilation -ModuleName 'DevPoSh.PoSh' { [pscustomobject]@{Outcome='Success';Changed=$true;Path='C:/fixture/compiled.psm1'} }
        Mock Update-DevPoShStartup -ModuleName 'DevPoSh.PoSh' {}
    }
    It 'compiles all configured projects without requiring Acme and Name' {
        $r=Get-DevPoSh -Action CompileAll -CompilationRoot (Join-Path $TestDrive 'cache')
        $r.Outcome | Should -Be Success
        $r.Data.Compiled | Should -Be 1
        Should -Invoke Get-DevPoShProjects -ModuleName 'DevPoSh.PoSh' -Times 1 -Exactly -ParameterFilter {$Path[0] -eq $env:FRENCHEXDEV_MYDEVPOSH_PATH}
    }
    It 'preserves legacy project Compile and Path suffix selection' {
        $project=Join-Path $TestDrive 'PoSh/Fixture/Example/nested';New-Item $project -ItemType Directory -Force | Out-Null
        Push-Location $TestDrive
        try{(Get-DevPoSh Fixture Example Compile -Path nested).Data.Compiled | Should -Be 1}finally{Pop-Location}
        Should -Invoke Get-DevPoShProjects -ModuleName 'DevPoSh.PoSh' -ParameterFilter {$Path[0] -match 'Example[/\\]nested$'} -Times 1
    }
    It 'does not combine CompileAll with project actions' {
        {Get-DevPoSh -Action CompileAll,Compile} | Should -Throw
        {Get-DevPoSh Fixture Example CompileAll} | Should -Throw
    }
    It 'counts failures and still compiles the next project' {
        Mock Get-DevPoShProjects -ModuleName 'DevPoSh.PoSh' {foreach($name in @('Bad','Good')){[pscustomobject]@{Name=$name;Path="C:/fixture/$name";Acme='Fixture';Data=@{}}}}
        Mock New-DevPoShProjectBundle -ModuleName 'DevPoSh.PoSh' {param($Project) if($Project.Name -eq 'Bad'){throw 'bad project'};[pscustomobject]@{Code='good';Assets=@{}}}
        $r=Get-DevPoSh -Action CompileAll -CompilationRoot (Join-Path $TestDrive 'cache')
        $r.Data.Failed | Should -Be 1
        $r.Data.Compiled | Should -Be 1
    }
    It 'bounds the report by UTF8 bytes as well as project count' {
        Mock Get-DevPoShProjects -ModuleName 'DevPoSh.PoSh' {foreach($number in 1..21){[pscustomobject]@{Name="Project$number";Path="C:/fixture/Project$number";Acme='Fixture';Data=@{}}}}
        Mock Export-DevPoShCompilation -ModuleName 'DevPoSh.PoSh' {[pscustomobject]@{Outcome='Success';Changed=$true;Path=('C:/'+('été'*200)+'.psm1')}}
        $r=Get-DevPoSh -Action CompileAll -CompilationRoot (Join-Path $TestDrive 'cache')
        $r.Data.Compiled | Should -Be 21
        $r.Truncation.Applied | Should -BeTrue
        [Text.Encoding]::UTF8.GetByteCount((ConvertTo-Json $r -Depth 12 -Compress)) | Should -BeLessOrEqual 8192
    }
}
