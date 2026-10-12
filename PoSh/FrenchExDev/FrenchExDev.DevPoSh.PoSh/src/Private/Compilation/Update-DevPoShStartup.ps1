function Update-DevPoShStartup {
    param([string]$CompilationRoot,[object[]]$Entries,[string]$SourceRoot,[switch]$Replace)
    [void][IO.Directory]::CreateDirectory($CompilationRoot)
    $path=Join-Path $CompilationRoot 'startup.json'
    $lock=[IO.File]::Open("$path.lock",[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    try {
        $old=if(Test-Path -LiteralPath $path){@( (ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($path))).Modules )}else{@()}
        $keys=@($Entries | ForEach-Object {"$($_.SourceRoot)|$($_.Project)"})
        $remaining=@($old | Where-Object { if($Replace -and $_.SourceRoot -eq $SourceRoot){return $false}; "$($_.SourceRoot)|$($_.Project)" -notin $keys })
        $content=ConvertTo-Json -InputObject ([ordered]@{Version=1;Modules=@($remaining)+@($Entries)}) -Depth 6
        if(!(Test-Path -LiteralPath $path) -or [IO.File]::ReadAllText($path) -cne $content){$temp="$path.$([guid]::NewGuid().ToString('N')).tmp";[IO.File]::WriteAllText($temp,$content,[Text.UTF8Encoding]::new($false));[IO.File]::Move($temp,$path,$true)}
    } finally {$lock.Dispose()}
}
