BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -DisableNameChecking -Force}
Describe 'Pump-Me' {
    BeforeEach {
        InModuleScope OutputPump.PoSh {
            $script:entry=[pscustomobject]@{Id='fixture.run';Software='fixture';Root='fixture';Module='unused';Function='Invoke-Fixture';Route=@{Executable='fixture';Tokens=@('run');Options=@{};Positional=$null;Multiple=$false};TestInput=$null;Transform=$null}
            $script:fixtureModule=New-Module -Name FixtureUnit -ScriptBlock {function Invoke-Fixture {param($WorkingDirectory,$MaxItems,$MaxOutputBytes,$TimeoutSeconds)[pscustomobject]@{Summary='fixture';Outcome='Success';Data=@{Branch='main';Items=@(1,2)};ExitCode=0;Error=$null;RetryAdvice='SafeToRetry';Truncation=[pscustomobject]@{Applied=$false;Reasons=@();TotalCount=2}}};Export-ModuleMember -Function Invoke-Fixture}
            Mock Get-OutputPumpRegistry {@($script:entry)}
            Mock Import-OutputPumpSoftware {$script:fixtureModule}
            Mock Get-Command {throw 'Unexpected native execution'} -ParameterFilter {$CommandType -eq 'Application'}
        }
    }
    It 'dispatches once and maps the typed Data object' {
        $r=Pump-Me fixture run -Map {param($c)[pscustomobject]@{Branch=$c.Branch;Password='example-secret'}}
        $r.Outcome | Should -Be Success
        $r.Data.Branch | Should -Be main
        $r.Data.Password | Should -Be '[REDACTED]'
        Should -Invoke Import-OutputPumpSoftware -ModuleName OutputPump.PoSh -Exactly -Times 1
    }
    It 'filters data while preserving the execution status' {
        $r=pump fixture run -Filter {param($c)$false}
        $r.Outcome | Should -Be Success
        @($r.Data.Items).Count | Should -Be 0
    }
    It 'returns Unsupported without importing a pump for an unknown option' {
        (Pump-Me fixture run --unknown).Outcome | Should -Be Unsupported
        Should -Invoke Import-OutputPumpSoftware -ModuleName OutputPump.PoSh -Exactly -Times 0
    }
    It 'does not prompt or invoke a function when native required arguments are missing' {
        InModuleScope OutputPump.PoSh {
            $script:fixtureModule=New-Module -Name RequiredFixture -ScriptBlock {function Invoke-Fixture {param([Parameter(Mandatory)][string]$Required) throw 'Pump should not execute'};Export-ModuleMember -Function Invoke-Fixture}
        }
        $r=Pump-Me fixture run
        $r.Outcome | Should -Be Unsupported
        $r.Error.Code | Should -Be MissingNativeArguments
    }
    It 'does not fall through to native execution on configuration failure' {
        Mock Get-OutputPumpRegistry {throw 'invalid configuration'} -ModuleName OutputPump.PoSh
        (Pump-Me fixture run).Error.Code | Should -Be RegistryNotInitialized
        Should -Invoke Get-Command -ModuleName OutputPump.PoSh -Exactly -Times 0 -ParameterFilter {$CommandType -eq 'Application'}
    }
    It 'converts projection errors into an envelope' {(Pump-Me fixture run -Map {throw 'private failure'}).Error.Code | Should -Be TransformationFailed}
    It 'passes unrecognized pipeline objects unchanged without native execution' {
        InModuleScope OutputPump.PoSh {
            $script:entry.TestInput='Test-Fixture';$script:entry.Transform='Convert-Fixture'
            $script:fixtureModule=New-Module -Name FixtureUnit -ScriptBlock {function Test-Fixture {param($x)$false};function Convert-Fixture {throw 'Must not transform'};Export-ModuleMember -Function Test-Fixture,Convert-Fixture}
        }
        $inputValue=[pscustomobject]@{Unknown=42}
        $r=$inputValue | Pump-Me -PumpId fixture.run
        [object]::ReferenceEquals($inputValue,$r) | Should -BeTrue
    }
    It 'transforms recognized pipeline input exactly once' {
        InModuleScope OutputPump.PoSh {
            $script:entry.TestInput='Test-Fixture';$script:entry.Transform='Convert-Fixture'
            $script:fixtureModule=New-Module -Name FixtureUnit -ScriptBlock {
                function Test-Fixture {param($x)$x.Kind -eq 'recognized'}
                function Convert-Fixture {param($x)[pscustomobject]@{Summary='transformed';Outcome='Success';Data=@{Items=@($x.Value*2)};ExitCode=$null;Error=$null;RetryAdvice='SafeToRetry';Truncation=[pscustomobject]@{Applied=$false;Reasons=@();TotalCount=1}}}
                Export-ModuleMember -Function Test-Fixture,Convert-Fixture
            }
        }
        $r=[pscustomobject]@{Kind='recognized';Value=21} | Pump-Me -PumpId fixture.run
        $r.Data.Items[0] | Should -Be 42
        Should -Invoke Import-OutputPumpSoftware -ModuleName OutputPump.PoSh -Exactly -Times 1
    }
}
