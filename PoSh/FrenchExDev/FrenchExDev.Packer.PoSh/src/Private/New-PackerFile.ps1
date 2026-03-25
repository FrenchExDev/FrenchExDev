function New-PackerFile {
    [CmdletBinding()]
    param(
        [string] $WorkingDirectory,
        [string] $Kind,
        [scriptblock] $Files
    )

    $pathToCreate = "${WorkingDirectory}/${Kind}"

    if (Test-Path $pathToCreate) {
        Remove-Item -Recurse -Force $pathToCreate | Out-Null
    }

    New-Item $pathToCreate -ItemType Directory | Out-Null

    $invokedFiles = Invoke-Command $Files
    foreach ($file in $invokedFiles.GetEnumerator()) {
        Write-Debug "$pathToCreate/$($file.Key)"
        $extension = if (![string]::IsNullOrEmpty($file.value.extension)) {
            ".$($file.value.extension)"
        }
        $filePath = split-path "$pathToCreate/$($file.Key)" -parent
        if (!(test-path $filepath)) {
            New-Item $filePath -ItemType Directory -Force
        }
        $file.Value.Content | Out-File "$pathToCreate/$($file.Key)$($extension)" -Force -NoNewline | Out-Null

        if($IsWindows) {
            dos2unix "$pathToCreate/$($file.Key)$($extension)" 2> $null
        }
    }
}
