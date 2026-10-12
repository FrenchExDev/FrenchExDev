
function Write-EnvFile {
    [CmdletBinding()]
    param(
        [string] $EnvFile = ".env",
        [hashtable] $Data
    )

    Write-Verbose "Write-EnvFile> EnvFile: $EnvFile"

    if (test-path $EnvFile) {
        Remove-Item $EnvFile -Force
    }

    Out-File $EnvFile -Encoding ascii

    foreach ($kv in $data.GetEnumerator()) {
        $key = $kv.Name
        $value = $kv.Value

        Write-Verbose "Write-EnvFile> Key: $key, Value: $value"

        "${key}=${value}" | Out-File $EnvFile -Append -Encoding ascii
    }
}
