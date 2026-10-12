BeforeAll {
    . (Join-Path $PSScriptRoot '../TestSupport.ps1')
    Import-Module (Join-Path $script:ProviderRoot 'Git-OPumper.psd1') -Force -DisableNameChecking
    $script:case=$script:Cases['Get-PumpGitStatus']
    function Set-TestGitStatusStream {
        param([string]$Text='', [string]$StartError='', [switch]$TimedOut, [int]$GeneratedRecords=0, [int]$OmittedRecords=0)
        InModuleScope OutputPump.PoSh -Parameters @{Text=$Text;StartError=$StartError;TimedOut=[bool]$TimedOut;GeneratedRecords=$GeneratedRecords;OmittedRecords=$OmittedRecords} {
            param($Text,$StartError,$TimedOut,$GeneratedRecords,$OmittedRecords)
            $script:gitStatusStream=[pscustomobject]@{Index=0;Disposed=$false;Records=@($Text.Split([char]0) | Where-Object {$_});GeneratedRecords=$GeneratedRecords;
                Result=[pscustomobject]@{Stdout='';Stderr='';ExitCode=$(if($StartError -or $TimedOut){$null}else{0});StartError=$StartError;TimedOut=$TimedOut;Truncated=$false;OmittedRecords=$OmittedRecords;StdoutCharacters=$Text.Length;StderrCharacters=0}}
            $script:gitStatusStream | Add-Member ScriptMethod Next {
                if($this.Index -lt $this.Records.Count){$text=$this.Records[$this.Index++]}
                elseif($this.Index -lt $this.GeneratedRecords){$index=$this.Index++;$text='? '+$index+('-' * 7000);$this.Result.StdoutCharacters+=$text.Length+1}
                else{return}
                [pscustomobject]@{Text=$text;Stream='stdout';Truncated=$false;Length=$text.Length}
            }
            $script:gitStatusStream | Add-Member ScriptMethod Dispose {$this.Disposed=$true}
            Mock Start-OutputPumpStream {$script:gitStatusStream}
        }
    }
}
Describe 'Get-PumpGitStatus' {
    BeforeEach {
        Mock Invoke-PumpProcess { throw 'Git status must not buffer the entire output' } -ModuleName Git-OPumper
        Mock Start-OutputPumpStream { throw 'Unexpected external process in unit test' } -ModuleName OutputPump.PoSh
    }
    It 'projects the documented NUL format and invokes the native command once' {
        Set-TestGitStatusStream -Text $script:case.Stdout
        $parameters=$script:case.Arguments
        $result=Get-PumpGitStatus @parameters
        $result.Outcome | Should -Be Success
        $value=$result
        foreach($part in $script:case.Expected.Split('.')){if($part -match '^\d+$'){$value=@($value)[[int]$part]}else{$value=$value.$part}}
        $value | Should -Be $script:case.Value
        Should -Invoke Start-OutputPumpStream -ModuleName OutputPump.PoSh -Times 1 -Exactly -ParameterFilter {$Executable -eq 'git' -and $ArgumentList -contains '--porcelain=v2' -and [int]$RecordDelimiter -eq 0}
        Should -Invoke Invoke-PumpProcess -ModuleName Git-OPumper -Times 0 -Exactly
        ($result | ConvertTo-Json -Depth 24 -Compress) | Should -Not -Match '"Stdout"\s*:|"Stderr"\s*:'
    }
    It 'drains more than the old four MiB capture while retaining a bounded preview and exact total' {
        Set-TestGitStatusStream -Text "? first`0? second`0" -GeneratedRecords 610
        $result=Get-PumpGitStatus -MaxItems 2
        $result.Outcome | Should -Be Success
        $result.Truncation.TotalCount | Should -Be 610
        $result.Truncation.ReturnedCount | Should -Be 2
        $result.Truncation.Reasons | Should -Contain ItemLimit
        InModuleScope OutputPump.PoSh {
            $script:gitStatusStream.Result.StdoutCharacters | Should -BeGreaterThan 4194304
            $script:gitStatusStream.Index | Should -Be 610
            $script:gitStatusStream.Disposed | Should -BeTrue
        }
        Should -Invoke Start-OutputPumpStream -ModuleName OutputPump.PoSh -Times 1 -Exactly
    }
    It 'preserves rename sources, Unicode, whitespace, conflicts and branch counts across records' {
        $text="# branch.head develop`0# branch.ab +2 -3`0"+
            "2 R. N... 100644 100644 100644 a b R100 nouveau é`nnom`0? ancien nom`0"+
            "u UU N... 100644 100644 100644 100644 a b c conflit`0"+
            "1 MM N... 100644 100644 100644 a b fichier`0"
        Set-TestGitStatusStream -Text $text
        $result=Get-PumpGitStatus
        $result.Outcome | Should -Be Success
        $result.Data.Branch | Should -Be develop
        $result.Data.Ahead | Should -Be 2
        $result.Data.Behind | Should -Be 3
        $result.Data.Items[0].Path | Should -Be "nouveau é`nnom"
        $result.Data.Items[0].OriginalPath | Should -Be '? ancien nom'
        $result.Data.Items[1].Groups | Should -Contain Conflict
        $result.Data.Items[2].Groups | Should -Contain Staged
        $result.Data.Items[2].Groups | Should -Contain Unstaged
        $result.Truncation.TotalCount | Should -Be 3
    }
    It 'rejects an incomplete rename instead of publishing a successful count' {
        Set-TestGitStatusStream -Text '2 R. N... 100644 100644 100644 a b R100 destination'
        $result=Get-PumpGitStatus
        $result.Error.Code | Should -Be UnrecognizedFormat
        $result.Truncation.TotalCount | Should -BeNullOrEmpty
    }
    It 'marks the total unknown when a record exceeds the analysis limit' {
        Set-TestGitStatusStream -Text "? retained`0" -OmittedRecords 1
        $result=Get-PumpGitStatus
        $result.Error.Code | Should -Be RecordLimit
        $result.Truncation.TotalCount | Should -BeNullOrEmpty
    }
    It 'distinguishes startup failure without exposing the native capture' {
        Set-TestGitStatusStream -StartError CommandNotFoundException
        $result=Get-PumpGitStatus
        $result.Outcome | Should -Be Failed
        $result.Error.Code | Should -Be StartFailed
    }
    It 'distinguishes expiration and does not relaunch the process' {
        Set-TestGitStatusStream -TimedOut
        $result=Get-PumpGitStatus
        $result.Outcome | Should -Be Timeout
        $result.RetryAdvice | Should -Be Unknown
        Should -Invoke Start-OutputPumpStream -ModuleName OutputPump.PoSh -Times 1 -Exactly
    }
}
