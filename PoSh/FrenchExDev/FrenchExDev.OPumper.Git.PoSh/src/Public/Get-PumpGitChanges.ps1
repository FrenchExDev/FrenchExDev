function Get-PumpGitChanges {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path, [string]$WorkingDirectory='.', [switch]$Staged,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600,[Collections.IDictionary]$Environment)
    $arguments=@('--no-pager','diff','--no-ext-diff','--no-textconv','--no-color','--unified=0')
    if(Test-Path -LiteralPath (Join-Path $WorkingDirectory $Path) -PathType Container){return New-PumpResult -Summary 'Changed-line extraction needs one file.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='SingleFileRequired';Message='Use Get-PumpGitDiff first and select one file.'} -MaxOutputBytes $MaxOutputBytes}
    if($Staged){$arguments+='--cached'};$arguments+=@('--',$Path)
    $capture=Invoke-PumpProcess git $arguments $WorkingDirectory $TimeoutSeconds -Environment $Environment
    Complete-PumpProcess $capture -Summary 'Git changes: selected changed lines, without patch headers.' -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -Parse {
        param($c)
        $items=[Collections.Generic.List[object]]::new();$oldLine=0;$newLine=0;$fileCount=0;$privateKey=$false;$inHunk=$false
        foreach($line in ($c.Stdout -split '\r?\n')) {
            if($line.StartsWith('diff --git ')){$inHunk=$false;$fileCount++;if($fileCount -gt 1){throw 'Single file required'};continue}
            if($line -match '^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@'){$inHunk=$true;$oldLine=[int]$Matches[1];$newLine=[int]$Matches[2];continue}
            if(!$inHunk){continue}
            if($line.StartsWith('+') -or $line.StartsWith('-')) {
                $text=$line.Substring(1)
                if($text -match '-----BEGIN .*PRIVATE KEY-----'){$privateKey=$true}
                $safeText=if($privateKey){'[REDACTED: private key]'}else{$text}
                if($text -match '-----END .*PRIVATE KEY-----'){$privateKey=$false}
                if($line.StartsWith('+')){$items.Add(@{Change='Added';Line=$newLine;Text=$safeText});$newLine++}
                else{$items.Add(@{Change='Deleted';Line=$oldLine;Text=$safeText});$oldLine++}
            }
        }
        @{Items=$items.ToArray();Path=$Path}
    }
}
