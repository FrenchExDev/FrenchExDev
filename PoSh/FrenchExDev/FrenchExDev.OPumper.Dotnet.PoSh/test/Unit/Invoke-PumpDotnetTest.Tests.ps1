BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:ProviderRoot 'Dotnet-OPumper.psd1') -Force}
Describe 'Invoke-PumpDotnetTest' {
    BeforeEach {Mock Invoke-PumpProcess {throw 'Unexpected native execution'} -ModuleName Dotnet-OPumper}
    It 'uses VSTest reports for counters without requiring dotnet' {
        InModuleScope Dotnet-OPumper {
            Mock Get-DotnetTestRunner {'VSTest'}
            Mock Invoke-PumpProcess {
                $index=[Array]::IndexOf($Arguments,'--results-directory')
                $folder=$Arguments[$index+1];[void][IO.Directory]::CreateDirectory($folder)
                [IO.File]::WriteAllText((Join-Path $folder 'report.trx'),'<TestRun><ResultSummary><Counters total="2" passed="2" notExecuted="0" /></ResultSummary></TestRun>')
                [pscustomobject]@{Stdout='';Stderr='';StartError=$null;TimedOut=$false;Truncated=$false;ExitCode=0}
            }
            $r=Invoke-PumpDotnetTest -Target app.csproj
            $r.Outcome | Should -Be Success
            $r.Data.Total | Should -Be 2
            $r.Data.Passed | Should -Be 2
            Should -Invoke Invoke-PumpProcess -Exactly -Times 1 -ParameterFilter {$Arguments -contains 'trx'}
        }
    }
    It 'refuses to assume an MTP framework filter' {
        InModuleScope Dotnet-OPumper {
            Mock Get-DotnetTestRunner {'Microsoft.Testing.Platform'}
            $r=Invoke-PumpDotnetTest -Target app.csproj -Filter demo
            $r.Outcome | Should -Be Unsupported
            Should -Invoke Invoke-PumpProcess -Exactly -Times 0
        }
    }
    It 'does not invent counts when no report exists' {
        InModuleScope Dotnet-OPumper {
            Mock Get-DotnetTestRunner {'VSTest'}
            Mock Invoke-PumpProcess {[pscustomobject]@{Stdout='';Stderr='';StartError=$null;TimedOut=$false;Truncated=$false;ExitCode=0}}
            $r=Invoke-PumpDotnetTest -Target app.csproj
            $r.Outcome | Should -Be Failed
            $r.Error.Code | Should -Be TestReportMissing
        }
    }
}
