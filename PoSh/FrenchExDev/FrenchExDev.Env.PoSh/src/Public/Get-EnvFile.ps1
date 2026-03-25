function Get-EnvFile {
    [CmdletBinding()]
    param(
        [string] $EnvFile = ".env"
    )

    Write-Verbose "Get-EnvFile> EnvFile: $EnvFile"

    $envContent = Get-Content $EnvFile

    $builtLines = @{}

    foreach ($line in $envContent) {
        Write-Verbose  "Get-EnvFile> Line $line"
        if([string]::IsNullOrEmpty($line)) {
            continue;
        }
        $lineMatches = $line | Select-String -pattern '^(.*)=(.*)$' | Select-CaptureGroup
        $key = $lineMatches.1
        $value = $lineMatches.2
        Write-Verbose "Get-EnvFile> Line $line : $key = $value"
        $builtLines[$key] = $value
    }

    $builtLines
}
