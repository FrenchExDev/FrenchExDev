BeforeAll {
    . (Join-Path $PSScriptRoot '../TestSupport.ps1')
    Import-Module (Join-Path $script:ProviderRoot 'Ripgrep-OPumper.psd1') -Force -DisableNameChecking
    $script:case=$script:Cases['Find-PumpFiles']
}
Describe 'Find-PumpFiles' {
    BeforeEach { Mock Invoke-OutputPumpNative { throw 'Unexpected external process in unit test' } -ModuleName Ripgrep-OPumper }
    It 'projects the documented machine format and invokes the native command once' {
        InModuleScope Ripgrep-OPumper -Parameters @{Case=$script:case} {
            param($Case)
            $script:fixture=[pscustomobject]@{Stdout=$Case.Stdout;Stderr='';StartError=$null;ExitCode=0;TimedOut=$false;Truncated=$false;StdoutCharacters=$Case.Stdout.Length;StderrCharacters=0}
            Mock Invoke-OutputPumpNative {
                param($Executable,$ArgumentList,$State,$OnRecord,$Parse,$MaxItems,$MaxOutputBytes,$SuccessCodes,$RecordDelimiter)
                $capture=$script:fixture
                $capture | Add-Member NoteProperty OmittedRecords 0 -Force
                $delimiter=if($null -ne $RecordDelimiter){$RecordDelimiter}else{[char]10}
                foreach($text in $capture.Stdout.Split([char]$delimiter)){
                    if($text){& $OnRecord ([pscustomobject]@{Text=$text;Stream='stdout';Truncated=$false;Length=$text.Length}) $State}
                }
                $finalizer={param($c) & $Parse $State $c}.GetNewClosure()
                Complete-PumpProcess -Capture $capture -Parse $finalizer -Summary fixture -SuccessCodes $SuccessCodes -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes
            }
            
            $parameters=$Case.Arguments
            $result=Find-PumpFiles @parameters
            $result.Outcome | Should -Be Success
            $value=$result
            foreach($part in $Case.Expected.Split('.')){if($part -match '^\d+$'){$value=@($value)[[int]$part]}else{$value=$value.$part}}
            $value | Should -Be $Case.Value
            Should -Invoke Invoke-OutputPumpNative -Times 1 -Exactly -ParameterFilter {$Executable -eq $Case.Native -and $ArgumentList -contains $Case.Option}
            ($result | ConvertTo-Json -Depth 24 -Compress) | Should -Not -Match '"Stdout"\s*:|"Stderr"\s*:'
        }
    }
    It 'distinguishes startup failure without exposing the native capture' {
        InModuleScope Ripgrep-OPumper -Parameters @{Case=$script:case} {
            param($Case)
            Mock Invoke-OutputPumpNative { New-OutputPumpResult -Summary failed -Outcome Failed -ErrorInfo @{Code='StartFailed'} }
            
            $parameters=$Case.Arguments
            $result=Find-PumpFiles @parameters
            $result.Outcome | Should -Be Failed
            $result.Error.Code | Should -Be StartFailed
            ($result | ConvertTo-Json -Depth 24 -Compress) | Should -Not -Match 'never expose'
        }
    }
    It 'distinguishes expiration and does not relaunch the process' {
        InModuleScope Ripgrep-OPumper -Parameters @{Case=$script:case} {
            param($Case)
            Mock Invoke-OutputPumpNative { New-OutputPumpResult -Summary timeout -Outcome Timeout -RetryAdvice Unknown }
            
            $parameters=$Case.Arguments
            $result=Find-PumpFiles @parameters
            $result.Outcome | Should -Be Timeout
            $result.RetryAdvice | Should -Be Unknown
            Should -Invoke Invoke-OutputPumpNative -Times 1 -Exactly
        }
    }
}
