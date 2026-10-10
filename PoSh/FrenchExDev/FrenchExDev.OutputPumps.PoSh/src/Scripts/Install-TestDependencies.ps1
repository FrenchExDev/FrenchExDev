#requires -Version 7.4
[CmdletBinding()]
param([string]$Path=(Join-Path (Split-Path (Split-Path $PSScriptRoot)) '.cache/Modules'))
$ErrorActionPreference='Stop'
try {
    $dependencies=Import-PowerShellDataFile (Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'resources/dependencies.psd1')
    $installed=[Collections.Generic.List[object]]::new()
    foreach($name in $dependencies.Keys) {
        $version=$dependencies[$name]
        $destination=Join-Path $Path "$name/$version"
        $manifest=Join-Path $destination "$name.psd1"
        if(!(Test-Path -LiteralPath $manifest)) {
            [void][IO.Directory]::CreateDirectory($destination)
            $archive=Join-Path $Path "$name.$version.zip"
            try {
                Invoke-WebRequest -Uri "https://www.powershellgallery.com/api/v2/package/$name/$version" -OutFile $archive -ErrorAction Stop
                Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force
            } finally {if(Test-Path $archive){Remove-Item -LiteralPath $archive -Force}}
        }
        $installed.Add(@{Module=$name;Version=$version;Path=$manifest})
    }
    [pscustomobject]@{Outcome='Success';Items=$installed.ToArray()} | ConvertTo-Json -Compress
} catch { [pscustomobject]@{Outcome='Failed';Error=$_.Exception.GetType().Name} | ConvertTo-Json -Compress; exit 1 }
