function Set-DevPoShPowershellProfile {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidateNotNullOrWhiteSpace()][string]$Path,[switch]$Force)
    $destination=$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    $template=Join-Path $script:DevPoShProjectRoot 'resources/profile.ps1.tpl'
    $bytes=[IO.File]::ReadAllBytes($template)
    if(Test-Path -LiteralPath $destination -PathType Container){throw 'Path must designate a file.'}
    if([IO.File]::Exists($destination)) {
        if([Convert]::ToBase64String([IO.File]::ReadAllBytes($destination)) -ceq [Convert]::ToBase64String($bytes)) {
            return [pscustomobject]@{Path=$destination;Changed=$false;Backup=$null}
        }
        if(!$Force){throw 'Destination differs. Use -Force to back it up and replace it.'}
    }
    if(!$PSCmdlet.ShouldProcess($destination,'Copy the DevPoSh profile template')){return [pscustomobject]@{Path=$destination;Changed=$false;Backup=$null}}
    [void][IO.Directory]::CreateDirectory((Split-Path $destination))
    $backup=$null
    if([IO.File]::Exists($destination)) {
        $backup="$destination.$([DateTime]::UtcNow.ToString('yyyyMMddHHmmssfff')).$([guid]::NewGuid().ToString('N')).bak"
        [IO.File]::Copy($destination,$backup,$false)
    }
    $temporary="$destination.$([guid]::NewGuid().ToString('N')).tmp"
    try{[IO.File]::WriteAllBytes($temporary,$bytes);[IO.File]::Move($temporary,$destination,$true)}
    finally{if([IO.File]::Exists($temporary)){[IO.File]::Delete($temporary)}}
    [pscustomobject]@{Path=$destination;Changed=$true;Backup=$backup}
}
