<#
.SYNOPSIS
Persist a validated reusable pump in the catalogue.
.DESCRIPTION
Define Register-OutputPumpDocumentation for the OutputPumps runtime. Coordinate descriptor validation and documentation updates under the library registration lock.
#>


function Register-OutputPumpDocumentation {
    <#
    .SYNOPSIS
    Persist a validated reusable pump in the catalogue.
    .DESCRIPTION
    Coordinate descriptor validation and documentation updates under the library registration lock.
    #>
    
    param([Collections.IDictionary]$Descriptor,[string]$LibraryRoot)
    $lock=$null;$written=[Collections.Generic.List[string]]::new();$previous=@{}
    try {
        $root=$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($LibraryRoot)
        Assert-PumpDescriptor $Descriptor $root
        $lock=[IO.File]::Open((Join-Path $root '.catalogue.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
        $cataloguePath=Join-Path $root 'catalogue.json'
        $catalogue=if([IO.File]::Exists($cataloguePath)){@([IO.File]::ReadAllText($cataloguePath)|ConvertFrom-Json -AsHashtable)}else{@()}
        $matches=@($catalogue | Where-Object Id -eq $Descriptor.Id)
        if($matches.Count -gt 1){throw 'Duplicate catalogue identity.'}
        $catalogue=@($catalogue | Where-Object Id -ne $Descriptor.Id)+@($Descriptor)
        $catalogue=@($catalogue | Sort-Object Id)
        $files=[ordered]@{}
        $files[$cataloguePath]=ConvertTo-Json -InputObject $catalogue -Depth 24
        $files[(Join-Path $root "software/$($Descriptor.Id).md")]=ConvertTo-PumpIdentity $Descriptor
        $files[(Join-Path $root 'index.md')]=ConvertTo-PumpIndex $catalogue
        foreach($path in $files.Keys){
            $previous[$path]=if([IO.File]::Exists($path)){[IO.File]::ReadAllText($path)}else{$null}
            if($previous[$path] -cne $files[$path]){
                [void][IO.Directory]::CreateDirectory((Split-Path $path))
                Write-PumpAtomicText $path $files[$path]
                $written.Add($path)
            }
        }
        New-PumpResult -Summary 'OutputPump documentation registered.' -Data @{Items=@(@{Id=$Descriptor.Id;ChangedFiles=$written.Count})}
    }catch{
        foreach($path in $written){if($null -eq $previous[$path]){[IO.File]::Delete($path)}else{Write-PumpAtomicText $path $previous[$path]}}
        New-PumpResult -Summary 'OutputPump documentation registration failed.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='RegistrationFailed';Message=$_.Exception.Message}
    }finally{if($lock){$lock.Dispose()}}
}
