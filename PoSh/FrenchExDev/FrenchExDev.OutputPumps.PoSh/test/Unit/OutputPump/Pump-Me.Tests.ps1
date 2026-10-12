<#
.SYNOPSIS
Unit tests for Pump-Me.
.DESCRIPTION
Exercise the named command or contract with isolated fixtures and mocks; keep process execution under test control.
#>


BeforeAll {. (Join-Path $PSScriptRoot '../TestSupport.ps1');Import-Module (Join-Path $script:Library 'OutputPump.PoSh.psd1') -DisableNameChecking -Force}
Describe 'Pump-Me' {
    BeforeEach {
        InModuleScope OutputPump.PoSh {
            $script:entry=[pscustomobject]@{Id='fixture.run';Software='fixture';Root='fixture';Module='unused';Function='Invoke-Fixture';Route=@{Executable='fixture';Tokens=@('run');Options=@{};Positional=$null;Multiple=$false};TestInput=$null;Transform=$null}
            $script:fixtureModule=New-Module -Name FixtureUnit -ScriptBlock {function Invoke-Fixture {
    <#
    .SYNOPSIS
    Return controlled data for a dispatch unit test.
    .DESCRIPTION
    Exercise argument forwarding and result projection without launching a native executable.
    #>
    param($WorkingDirectory,$MaxItems,$MaxOutputBytes,$TimeoutSeconds)[pscustomobject]@{Summary='fixture';Outcome='Success';Data=@{Branch='main';Items=@(1,2)};ExitCode=0;Error=$null;RetryAdvice='SafeToRetry';Truncation=[pscustomobject]@{Applied=$false;Reasons=@();TotalCount=2}}};Export-ModuleMember -Function Invoke-Fixture}
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
    It 'keeps debug records outside the single envelope for handler=<UseHandler>' -TestCases @(@{UseHandler=$false},@{UseHandler=$true}) {
        param($UseHandler)
        InModuleScope OutputPump.PoSh -Parameters @{UseHandler=$UseHandler} {
            param($UseHandler)
            $script:fixtureModule=New-Module -Name DebugFixture -ScriptBlock {
                function Invoke-Fixture {
                    [CmdletBinding()]
                    param($WorkingDirectory,$MaxItems,$MaxOutputBytes,$TimeoutSeconds)
                    Write-Debug '[OutputPumps] Fixture diagnostic.'
                    [pscustomobject]@{Summary='fixture';Outcome='Success';Data=@{Items=@(1)};ExitCode=0;Error=$null;RetryAdvice='SafeToRetry';Truncation=[pscustomobject]@{Applied=$false;Reasons=@();TotalCount=1}}
                }
                Export-ModuleMember -Function Invoke-Fixture
            }
            if($UseHandler) {
                $command=$script:fixtureModule.ExportedFunctions['Invoke-Fixture']
                $script:entry | Add-Member NoteProperty Command $command
                $script:entry | Add-Member NoteProperty Handler ({param($call) $parameters=$call.Parameters;& $command @parameters}.GetNewClosure())
            }
        }
        $events=@(Pump-Me fixture run -Debug 5>&1)
        $results=@($events | Where-Object {$_ -isnot [Management.Automation.DebugRecord]})
        $results.Count | Should -Be 1
        $results[0].Outcome | Should -Be Success
        @($events | Where-Object {$_ -is [Management.Automation.DebugRecord]}).Count | Should -BeGreaterOrEqual 2
        $messages=($events | Where-Object {$_ -is [Management.Automation.DebugRecord]} | ForEach-Object Message) -join ' '
        $messages | Should -Match 'action=Dispatch.Registry; elapsedMs=\d+\.\d{3};'
        $messages | Should -Match 'action=Dispatch.Invoke;'
        $messages | Should -Match 'action=Dispatch.Total;'
        $quiet=@(Pump-Me fixture run -Debug:$false 5>&1)
        $quiet.Count | Should -Be 1
        $quiet[0].Outcome | Should -Be Success
    }
    It 'forwards child environment overrides to a provider that supports them' {
        InModuleScope OutputPump.PoSh {
            $script:fixtureModule=New-Module -Name EnvironmentFixture -ScriptBlock {
                function Invoke-Fixture {
                    param($WorkingDirectory,$MaxItems,$MaxOutputBytes,$TimeoutSeconds,[Collections.IDictionary]$Environment)
                    [pscustomobject]@{Summary='fixture';Outcome='Success';Data=@{HasOverride=($Environment.ADDED -eq 'fixture-value');HasRemoval=($Environment.Contains('REMOVED') -and $null -eq $Environment.REMOVED);Items=@()};ExitCode=0;Error=$null;RetryAdvice='SafeToRetry';Truncation=[pscustomobject]@{Applied=$false;Reasons=@();TotalCount=0}}
                }
                Export-ModuleMember -Function Invoke-Fixture
            }
        }
        $result=Pump-Me fixture run -Environment @{ADDED='fixture-value';REMOVED=$null}
        $result.Outcome | Should -Be Success
        $result.Data.HasOverride | Should -BeTrue
        $result.Data.HasRemoval | Should -BeTrue
    }
    It 'rejects environment overrides without executing a provider that cannot apply them' {
        $result=Pump-Me fixture run -Environment @{ADDED='fixture-value'}
        $result.Outcome | Should -Be Unsupported
        $result.Error.Code | Should -Be EnvironmentUnsupported
    }
    It 'returns Unsupported without importing a pump for an unknown option' {
        (Pump-Me fixture run --unknown).Outcome | Should -Be Unsupported
        Should -Invoke Import-OutputPumpSoftware -ModuleName OutputPump.PoSh -Exactly -Times 0
    }
    It 'does not prompt or invoke a function when native required arguments are missing' {
        InModuleScope OutputPump.PoSh {
            $script:fixtureModule=New-Module -Name RequiredFixture -ScriptBlock {function Invoke-Fixture {
    <#
    .SYNOPSIS
    Return controlled data for a dispatch unit test.
    .DESCRIPTION
    Exercise argument forwarding and result projection without launching a native executable.
    #>
    param([Parameter(Mandatory)][string]$Required) throw 'Pump should not execute'};Export-ModuleMember -Function Invoke-Fixture}
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
            $script:fixtureModule=New-Module -Name FixtureUnit -ScriptBlock {function Test-Fixture {
    <#
    .SYNOPSIS
    Recognize the business object used by a transformation test.
    .DESCRIPTION
    Return a deterministic match decision for the explicitly selected pipeline stage.
    #>
    param($x)$false};function Convert-Fixture {
    <#
    .SYNOPSIS
    Transform the recognized object in a pipeline unit test.
    .DESCRIPTION
    Return controlled projected data without reexecuting the native command.
    #>
    throw 'Must not transform'};Export-ModuleMember -Function Test-Fixture,Convert-Fixture}
        }
        $inputValue=[pscustomobject]@{Unknown=42}
        $r=$inputValue | Pump-Me -PumpId fixture.run
        [object]::ReferenceEquals($inputValue,$r) | Should -BeTrue
    }
    It 'transforms recognized pipeline input exactly once' {
        InModuleScope OutputPump.PoSh {
            $script:entry.TestInput='Test-Fixture';$script:entry.Transform='Convert-Fixture'
            $script:fixtureModule=New-Module -Name FixtureUnit -ScriptBlock {
                function Test-Fixture {
                    <#
                    .SYNOPSIS
                    Recognize the business object used by a transformation test.
                    .DESCRIPTION
                    Return a deterministic match decision for the explicitly selected pipeline stage.
                    #>
                    param($x)$x.Kind -eq 'recognized'}
                function Convert-Fixture {
                    <#
                    .SYNOPSIS
                    Transform the recognized object in a pipeline unit test.
                    .DESCRIPTION
                    Return controlled projected data without reexecuting the native command.
                    #>
                    param($x)[pscustomobject]@{Summary='transformed';Outcome='Success';Data=@{Items=@($x.Value*2)};ExitCode=$null;Error=$null;RetryAdvice='SafeToRetry';Truncation=[pscustomobject]@{Applied=$false;Reasons=@();TotalCount=1}}}
                Export-ModuleMember -Function Test-Fixture,Convert-Fixture
            }
        }
        $r=[pscustomobject]@{Kind='recognized';Value=21} | Pump-Me -PumpId fixture.run
        $r.Data.Items[0] | Should -Be 42
        Should -Invoke Import-OutputPumpSoftware -ModuleName OutputPump.PoSh -Exactly -Times 1
    }
}
