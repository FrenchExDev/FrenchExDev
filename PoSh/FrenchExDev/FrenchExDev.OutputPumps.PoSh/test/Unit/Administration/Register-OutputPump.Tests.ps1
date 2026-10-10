BeforeAll { . (Join-Path $PSScriptRoot '../TestSupport.ps1'); Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -Force -DisableNameChecking }
Describe 'Register-OutputPump' {
    It 'updates catalogue, identity and index together without rewriting an identical registration' {
        $root=Join-Path $TestDrive 'library';$descriptor=New-TestPumpLibrary $root
        (Register-OutputPump $descriptor -LibraryRoot $root).Outcome | Should -Be Success
        $before=(Get-Item (Join-Path $root 'index.md')).LastWriteTimeUtc
        $second=Register-OutputPump $descriptor -LibraryRoot $root
        $second.Data.Items[0].ChangedFiles | Should -Be 0
        (Get-Item (Join-Path $root 'index.md')).LastWriteTimeUtc | Should -Be $before
        [IO.File]::ReadAllText((Join-Path $root 'software/fixture.md')) | Should -Match 'fixture.run'
    }
    It 'rejects an export mismatch without writing the index' {
        $root=Join-Path $TestDrive 'invalid';$descriptor=New-TestPumpLibrary $root;$descriptor.Commands[0].Function='Get-MissingPump'
        (Register-OutputPump $descriptor -LibraryRoot $root).Outcome | Should -Be Failed
        Test-Path (Join-Path $root 'index.md') | Should -BeFalse
    }
    It 'respects the registration lock' {
        $root=Join-Path $TestDrive 'locked';$descriptor=New-TestPumpLibrary $root
        $lock=[IO.File]::Open((Join-Path $root '.catalogue.lock'),'OpenOrCreate','ReadWrite','None')
        try{(Register-OutputPump $descriptor -LibraryRoot $root).Outcome | Should -Be Failed}finally{$lock.Dispose()}
    }
}
