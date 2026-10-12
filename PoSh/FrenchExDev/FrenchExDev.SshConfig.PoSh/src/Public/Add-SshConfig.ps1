function Add-SshConfig {
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $false)][object] $Config,
        [string] $ConfigFile = "$env:USERPROFILE/.ssh/config"
    )

    $hostname = $($Config | select-string -pattern 'Host (.*)' | Select-CaptureGroup).1
    $hostname
    if ([string]::IsNullOrEmpty($hostname)) {
        throw "hostname is empty"
    }

    Remove-SshConfig -hostname $hostname 
    
    $config | out-file $ConfigFile -Append -Encoding ascii
}
