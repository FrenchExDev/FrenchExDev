function Invoke-OutputPumpNative {
    [CmdletBinding(DefaultParameterSetName='Buffered')]
    param([Parameter(Mandatory,Position=0)][string]$Executable,[string[]]$ArgumentList=@(),[string]$WorkingDirectory='.',
          [Parameter(Mandatory)][scriptblock]$Parse,[string]$Summary='Native command processed.',[int[]]$SuccessCodes=@(0),
          [Parameter(Mandatory,ParameterSetName='Streaming')][scriptblock]$OnRecord,
          [Parameter(Mandatory,ParameterSetName='Streaming')][object]$State,
          [Parameter(ParameterSetName='Streaming')][ValidateRange(1024,1048576)][int]$MaxRecordCharacters=65536,
          [Parameter(ParameterSetName='Streaming')][char]$RecordDelimiter="`n",
          [ValidateRange(1,1000)][int]$MaxItems=20,[ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,[ValidateRange(1,86400)][int]$TimeoutSeconds=600)
    try {
        if($PSCmdlet.ParameterSetName -eq 'Streaming') {
            try {$stream=Start-OutputPumpStream -Executable $Executable -ArgumentList $ArgumentList -WorkingDirectory $WorkingDirectory -TimeoutSeconds $TimeoutSeconds -MaxRecordCharacters $MaxRecordCharacters -RecordDelimiter $RecordDelimiter}
            catch {return New-PumpResult -Summary $Summary -Outcome Failed -RetryAdvice CorrectRequest -MaxOutputBytes $MaxOutputBytes -ErrorInfo @{Code='StartFailed';ExceptionType=$_.Exception.GetType().Name}}
            $parserFailed=$false
            try {
                while($null -ne ($record=$stream.Next())) {
                    if(!$parserFailed) {
                        try {
                            $emitted=0
                            & $OnRecord $record $State *>&1 | ForEach-Object {$emitted++}
                            if($emitted){throw 'OnRecord must update State without emitting output.'}
                        }catch{$parserFailed=$true}
                    }
                }
                $capture=$stream.Result
            }finally{$stream.Dispose()}
            if($parserFailed -and !$capture.TimedOut -and !$capture.StartError) {
                return New-PumpResult -Summary $Summary -Outcome Failed -ExitCode $capture.ExitCode -RetryAdvice CorrectRequest -Truncated -UnknownTotal -Reasons @('UnrecognizedFormat') -MaxOutputBytes $MaxOutputBytes -ErrorInfo @{Code='UnrecognizedFormat';Message='Streaming parser failed; the command was drained without relaunch.'}
            }
            $finalizer={param($completedCapture) & $Parse $State $completedCapture}.GetNewClosure()
            $result=Complete-PumpProcess -Capture $capture -Parse $finalizer -Summary $Summary -SuccessCodes $SuccessCodes -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes
            if($capture.OmittedRecords -gt 0 -and !$capture.TimedOut -and !$capture.StartError) {
                $data=@{};foreach($key in $result.Data.Keys){$data[$key]=$result.Data[$key]}
                $data.AnalysisComplete=$false
                return New-PumpResult -Summary $Summary -Outcome Failed -Data $data -ExitCode $capture.ExitCode -RetryAdvice NarrowRequest -Truncated -UnknownTotal -Reasons @($result.Truncation.Reasons + @('RecordLimit')) -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -ErrorInfo @{Code='RecordLimit';OmittedRecords=$capture.OmittedRecords}
            }
            return $result
        }
        $capture=Invoke-PumpProcess -Executable $Executable -Arguments $ArgumentList -WorkingDirectory $WorkingDirectory -TimeoutSeconds $TimeoutSeconds
        Complete-PumpProcess -Capture $capture -Parse $Parse -Summary $Summary -SuccessCodes $SuccessCodes -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes
    }catch{New-PumpResult -Summary 'Native pump failed.' -Outcome Failed -RetryAdvice CorrectRequest -MaxOutputBytes $MaxOutputBytes -ErrorInfo @{Code='NativePumpFailed';ExceptionType=$_.Exception.GetType().Name}}
}
