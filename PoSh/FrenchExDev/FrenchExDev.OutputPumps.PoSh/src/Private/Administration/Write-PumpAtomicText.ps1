<#
.SYNOPSIS
Replace a text artifact without exposing a partial write.
.DESCRIPTION
Define Write-PumpAtomicText for the OutputPumps runtime. Write through a temporary file and avoid replacing an artifact whose content is unchanged.
#>


function Write-PumpAtomicText {
    <#
    .SYNOPSIS
    Replace a text artifact without exposing a partial write.
    .DESCRIPTION
    Write through a temporary file and avoid replacing an artifact whose content is unchanged.
    #>
    
    param([string]$Path,[string]$Text)
    $parent=Split-Path $Path;[void][IO.Directory]::CreateDirectory($parent)
    $temporary=Join-Path $parent ('.pump-write-'+[guid]::NewGuid().ToString('N'))
    try{[IO.File]::WriteAllText($temporary,$Text,[Text.UTF8Encoding]::new($false));[IO.File]::Move($temporary,$Path,$true)}
    finally{if([IO.File]::Exists($temporary)){[IO.File]::Delete($temporary)}}
}
