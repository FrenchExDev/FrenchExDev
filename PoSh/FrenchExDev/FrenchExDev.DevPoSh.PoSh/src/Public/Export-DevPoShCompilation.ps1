function Export-DevPoShCompilation {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ScriptText,[Parameter(Mandatory)][string]$Destination,
          [Collections.IDictionary]$Assets=@{},[Collections.IDictionary]$Metadata=@{},
          [ValidatePattern('^[A-Za-z]+-[A-Za-z0-9]+$')][string]$ValidationFunction)
    $lock=$null; $stage=$null
    try {
        $target=$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Destination)
        $directory=Split-Path $target
        $fingerprint=Get-DevPoShFingerprint $ScriptText $Assets
        [void][IO.Directory]::CreateDirectory($directory)
        $lock=[IO.File]::Open("$target.lock",[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
        $manifestPath="$target.json"
        $unchanged=[IO.File]::Exists($target) -and [IO.File]::ReadAllText($target) -ceq $ScriptText
        foreach($name in $Assets.Keys) {
            $assetPath=[IO.Path]::GetFullPath((Join-Path $directory $name))
            if(!$assetPath.StartsWith($directory+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Asset outside compilation directory.'}
            if(![IO.File]::Exists($assetPath) -or [Convert]::ToBase64String([IO.File]::ReadAllBytes($assetPath)) -cne [Convert]::ToBase64String([byte[]]$Assets[$name])){$unchanged=$false}
        }
        if($unchanged -and [IO.File]::Exists($manifestPath)) {
            $old=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($manifestPath)) -AsHashtable
            if($old.Fingerprint -eq $fingerprint){return [pscustomobject]@{Outcome='Success';Path=$target;Changed=$false;Fingerprint=$fingerprint;Error=$null}}
        }
        $stage=Join-Path $directory ('.stage-'+[guid]::NewGuid().ToString('N'))
        [void][IO.Directory]::CreateDirectory($stage)
        $candidate=Join-Path $stage ([IO.Path]::GetFileName($target))
        $utf8=[Text.UTF8Encoding]::new($false)
        [IO.File]::WriteAllText($candidate,$ScriptText,$utf8)
        foreach($name in $Assets.Keys) {
            $assetPath=Join-Path $stage $name
            [void][IO.Directory]::CreateDirectory((Split-Path $assetPath))
            [IO.File]::WriteAllBytes($assetPath,[byte[]]$Assets[$name])
        }
        $validationParameters=@{Path=$candidate}
        if($ValidationFunction){$validationParameters.ValidationFunction=$ValidationFunction}
        Test-DevPoShCompilationCandidate @validationParameters
        # Content-addressed assets are immutable; publish them before the module switch.
        foreach($name in $Assets.Keys) {
            $assetPath=Join-Path $directory $name
            if(![IO.File]::Exists($assetPath)) {
                [void][IO.Directory]::CreateDirectory((Split-Path $assetPath))
                [IO.File]::Move((Join-Path $stage $name),$assetPath,$true)
            } elseif([Convert]::ToBase64String([IO.File]::ReadAllBytes($assetPath)) -cne [Convert]::ToBase64String([byte[]]$Assets[$name])){throw 'Immutable compilation asset collision.'}
        }
        $manifest=[ordered]@{Version=1;Fingerprint=$fingerprint;Module=[IO.Path]::GetFileName($target);Metadata=$Metadata}
        $manifestTemp=Join-Path $stage 'manifest.json'
        [IO.File]::WriteAllText($manifestTemp,(ConvertTo-Json -InputObject $manifest -Depth 12),$utf8)
        $previousModule=Join-Path $stage 'previous.psm1'
        $previousManifest=Join-Path $stage 'previous.json'
        $hadModule=[IO.File]::Exists($target); $hadManifest=[IO.File]::Exists($manifestPath)
        if($hadModule){[IO.File]::Copy($target,$previousModule)}
        if($hadManifest){[IO.File]::Copy($manifestPath,$previousManifest)}
        try {
            [IO.File]::Move($candidate,$target,$true)
            [IO.File]::Move($manifestTemp,$manifestPath,$true)
        } catch {
            if($hadModule){[IO.File]::Move($previousModule,$target,$true)}elseif([IO.File]::Exists($target)){[IO.File]::Delete($target)}
            if($hadManifest){[IO.File]::Move($previousManifest,$manifestPath,$true)}elseif([IO.File]::Exists($manifestPath)){[IO.File]::Delete($manifestPath)}
            throw
        }
        [pscustomobject]@{Outcome='Success';Path=$target;Changed=$true;Fingerprint=$fingerprint;Error=$null}
    } catch {
        [pscustomobject]@{Outcome='Failed';Path=$Destination;Changed=$false;Fingerprint=$null;Error=$_.Exception.GetType().Name}
    } finally {
        if($lock){$lock.Dispose()}
        if($stage -and [IO.Directory]::Exists($stage)) {
            $resolvedStage=[IO.Path]::GetFullPath($stage)
            if($resolvedStage.StartsWith([IO.Path]::GetFullPath($directory)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $resolvedStage -Recurse -Force -ErrorAction SilentlyContinue }
        }
    }
}
