BeforeAll {
    . (Join-Path $PSScriptRoot '../TestSupport.ps1')
    Import-Module (Join-Path $script:ProviderRoot 'Git-OPumper.psd1') -Force -DisableNameChecking
    $script:case=$script:Cases['Get-PumpGitLog']
}
Describe 'Get-PumpGitLog' {
    BeforeEach { Mock Invoke-PumpProcess { throw 'Unexpected external process in unit test' } -ModuleName Git-OPumper }
    It 'projects the documented machine format and invokes the native command once' {
        InModuleScope Git-OPumper -Parameters @{Case=$script:case} {
            param($Case)
            $script:fixture=[pscustomobject]@{Stdout=$Case.Stdout;Stderr='';StartError=$null;ExitCode=0;TimedOut=$false;Truncated=$false;StdoutCharacters=$Case.Stdout.Length;StderrCharacters=0}
            Mock Invoke-PumpProcess { $script:fixture }
            
            $parameters=$Case.Arguments
            $result=Get-PumpGitLog @parameters
            $result.Outcome | Should -Be Success
            $value=$result
            foreach($part in $Case.Expected.Split('.')){if($part -match '^\d+$'){$value=@($value)[[int]$part]}else{$value=$value.$part}}
            $value | Should -Be $Case.Value
            Should -Invoke Invoke-PumpProcess -Times 1 -Exactly -ParameterFilter {$Executable -eq $Case.Native -and $Arguments -contains $Case.Option}
            ($result | ConvertTo-Json -Depth 24 -Compress) | Should -Not -Match '"Stdout"\s*:|"Stderr"\s*:'
        }
    }
    It 'distinguishes startup failure without exposing the native capture' {
        InModuleScope Git-OPumper -Parameters @{Case=$script:case} {
            param($Case)
            Mock Invoke-PumpProcess { [pscustomobject]@{Stdout='never expose';Stderr='never expose';StartError='CommandNotFoundException';ExitCode=$null;TimedOut=$false;Truncated=$false} }
            
            $parameters=$Case.Arguments
            $result=Get-PumpGitLog @parameters
            $result.Outcome | Should -Be Failed
            $result.Error.Code | Should -Be StartFailed
            ($result | ConvertTo-Json -Depth 24 -Compress) | Should -Not -Match 'never expose'
        }
    }
    It 'distinguishes expiration and does not relaunch the process' {
        InModuleScope Git-OPumper -Parameters @{Case=$script:case} {
            param($Case)
            Mock Invoke-PumpProcess { [pscustomobject]@{Stdout='';Stderr='';StartError=$null;ExitCode=$null;TimedOut=$true;Truncated=$false} }
            
            $parameters=$Case.Arguments
            $result=Get-PumpGitLog @parameters
            $result.Outcome | Should -Be Timeout
            $result.RetryAdvice | Should -Be Unknown
            Should -Invoke Invoke-PumpProcess -Times 1 -Exactly
        }
    }
}