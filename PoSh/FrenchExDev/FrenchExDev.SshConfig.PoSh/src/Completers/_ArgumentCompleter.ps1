$getExistingHosts = {
    $(Get-Content $env:USERPROFILE\.ssh\config | select-string -pattern 'Host (.*)' | Select-CaptureGroup).1
}

Register-ArgumentCompleter -CommandName Remove-SshConfig -ParameterName HostName -ScriptBlock $getExistingHosts
Register-ArgumentCompleter -CommandName Open-Ssh -ParameterName HostName -ScriptBlock $getExistingHosts

Register-ArgumentCompleter -CommandName ssh,scp,sftp -Native -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)
    $knownHosts = Get-Content ${Env:HOMEPATH}\.ssh\config `
    | ForEach-Object { ([string]$_).Split(' ')[0] } `
    | ForEach-Object { $_.Split(',') } `
    | Sort-Object -Unique

    # For now just assume it's a hostname.
    $textToComplete = $wordToComplete
    $generateCompletionText = {
        param($x)
        $x
    }
    if ($wordToComplete -match "^(?<user>[-\w/\\]+)@(?<host>[-.\w]+)$") {
        $textToComplete = $Matches["host"]
        $generateCompletionText = {
            param($hostname)
            $Matches["user"] + "@" + $hostname
        }
    }

    $knownHosts `
    | Where-Object { $_ -like "${textToComplete}*" } `
    | ForEach-Object { [CompletionResult]::new((&$generateCompletionText($_)), $_, [CompletionResultType]::ParameterValue, $_) }
}
