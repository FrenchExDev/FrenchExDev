function Update-EnvFile {
    [CmdletBinding()]
    param(
        [string] $Path = ".",
        [string] $EnvFile = ".env",
        [parameter(Mandatory = $true)] [hashtable] $NewData
    )

    $envFilePath = "$Path/$EnvFile"

    $lines = if (Test-Path $envFilePath) { Get-EnvFile -EnvFile $envFilePath } else { @{} }
    
    foreach ($kv in $NewData.GetEnumerator()) {
        $key = $kv.Name
        $value = $kv.Value
        $lines.$key = $value
    }

    Write-EnvFile -EnvFile $envFilePath -Data $lines
}
