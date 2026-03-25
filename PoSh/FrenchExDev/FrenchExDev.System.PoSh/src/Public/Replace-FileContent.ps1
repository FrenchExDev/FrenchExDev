function Replace-FileContent {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]
    [CmdletBinding()]
    param(
        [ValidateNotNullOrWhiteSpace()] [string] $File,
        [ValidateNotNullOrWhiteSpace()] [string] $Start,
        [ValidateNotNullOrWhiteSpace()] [string] $Stop
    )

    $content = Get-Content $File

    $newContent = @()

    $started = $false

    foreach ($line in $content) {
        if ($started) {
            $matchStop = $line -match $Stop

            if ($matchStop) {
                $started = $false
            }
            continue;
        }
        else {
            $matchStart = $line -match $Start
            $started = $matchStart
            if ($started) {
                continue;
            }
        }

        $newContent += $line
    }

    $newContent -join [System.Environment]::NewLine | Out-File $File -Encoding utf8
    dos2unix $File 2> $null
}
