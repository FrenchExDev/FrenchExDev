function New-DockerCompose {
    [CmdletBinding()]
    param(
        [scriptblock] $Services,
        [scriptblock] $Volumes,
        [scriptblock] $Networks,
        [string] $File,
        [pscustomobject[]] $Files
    )

    $dockerCompose = [pscustomobject] @{}

    if ($null -ne $services) {
        $dockerCompose | Add-Member -MemberType NoteProperty -Name "services" -Value $(Invoke-Command $Services)
    }

    if ($null -ne $Volumes) {
        $dockerCompose | Add-Member -MemberType NoteProperty -Name "volumes" -Value $(Invoke-Command $Volumes)
    }

    if ($null -ne $Networks) {
        $dockerCompose | Add-Member -MemberType NoteProperty -Name "networks" -Value $(Invoke-Command $Networks)
    }

    if ($null -ne $Files -and $Files.Length -gt 0) {
        foreach ($currentFile in $Files) {
            if (!(Test-Path $currentFile.path)) {
                New-Item -ItemType Directory $($currentFile.path) | Out-Null
            }
            $currentFile.content | Out-File "$($currentFile.path)/$($currentFile.name)" -Encoding ascii -NoNewline
        }
    }

    if (![string]::IsNullOrEmpty($File)) {
        Write-DockerCompose -DockerCompose $dockerCompose -File $File
        return
    }

    $dockerCompose
}
