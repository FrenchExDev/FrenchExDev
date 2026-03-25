function New-DockerComposeService {
    [CmdletBinding()]
    param(
        [string] $Image,
        [string] $ContainerName,
        [string] $Restart,
        [string] $HostName,
        [string] $ShmSize,
        [scriptblock] $Build,
        [scriptblock] $Networks,
        [scriptblock] $Ports,
        [scriptblock] $Command,
        [scriptblock] $Labels,
        [scriptblock] $Volumes,
        [scriptblock] $Environment,
        [scriptblock] $Logging,
        [scriptblock] $Healthcheck,
        [scriptblock] $Links,
        [scriptblock] $DependsOn,
        [scriptblock] $ExtraHosts,
        [scriptblock] $Expose,
        [scriptblock] $CapAdd
    )

    $BuildInvoked = if ($null -ne $Build) { Invoke-Command $Build }
    $NetworksInvoked = if ($null -ne $Networks) { Invoke-Command $Networks }
    $PortsInvoked = if ($null -ne $Ports) { Invoke-Command $Ports }
    $CommandInvoked = if ($null -ne $Command) { Invoke-Command $Command }
    $LabelsInvoked = if ($null -ne $Labels) { Invoke-Command $Labels }
    $VolumesInvoked = if ($null -ne $Volumes) { Invoke-Command $Volumes }
    $EnvironmentInvoked = if ($null -ne $Environment) { Invoke-Command $Environment }
    $LoggingInvoked = if ($null -ne $Logging) { Invoke-Command $Logging }
    $HealthcheckInvoked = if ($null -ne $Healthcheck) { Invoke-Command $Healthcheck }
    $LinksInvoked = if ($null -ne $Links) { Invoke-Command $Links }
    $DependsOnInvoked = if ($null -ne $DependsOn) { Invoke-Command $DependsOn }
    $ExtraHostsInvoked = if ($null -ne $ExtraHosts) { Invoke-Command $ExtraHosts }
    $ExposeInvoked = if ($null -ne $Expose) { Invoke-Command $Expose }
    $CapAddInvoked = if ($null -ne $CapAdd) { Invoke-Command $CapAdd }

    $dcService = [pscustomobject] @{}

    if (![string]::IsNullOrEmpty($Image)) {
        $dcService | add-member -MemberType NoteProperty -Name "image" -Value $Image
    }

    if ($null -ne $BuildInvoked) {
        $dcService | Add-Member -MemberType NoteProperty -Name "build" -Value $BuildInvoked
    }

    if (![string]::IsNullOrEmpty($HostName)) {
        $dcService | Add-Member -MemberType NoteProperty -Name "hostname" -Value $HostName
    }

    if (![string]::IsNullOrEmpty($ContainerName)) {
        $dcService | Add-Member -MemberType NoteProperty -Name "container_name" -Value $ContainerName
    }

    if (![string]::IsNullOrEmpty($Restart)) {
        $dcService | Add-Member -MemberType NoteProperty -Name "restart" -Value $Restart
    }

    if ($null -ne $NetworksInvoked) {
        $dcService | Add-Member -MemberType NoteProperty -Name "networks" -Value $NetworksInvoked
    }

    if ($null -ne $PortsInvoked) {
        $dcService | Add-Member -MemberType NoteProperty -Name "ports" -Value $PortsInvoked
    }

    if ($null -ne $CommandInvoked) {
        $dcService | Add-Member -MemberType NoteProperty -Name "command" -Value $CommandInvoked
    }

    if ($null -ne $LabelsInvoked) {
        $dcService | Add-Member -MemberType NoteProperty -Name "labels" -Value $LabelsInvoked
    }

    if ($null -ne $VolumesInvoked) {
        $dcService | Add-Member -MemberType NoteProperty -Name "volumes" -Value $VolumesInvoked
    }

    if ($null -ne $EnvironmentInvoked) {
        $dcService | Add-Member -MemberType NoteProperty -Name "environment" -Value $EnvironmentInvoked
    }

    if ($null -ne $LoggingInvoked) {
        $dcService | Add-Member -MemberType NoteProperty -Name "logging" -Value $LoggingInvoked
    }

    if ($null -ne $HealthcheckInvoked) {
        $dcService | Add-Member -MemberType NoteProperty -Name "healthcheck" -Value $HealthcheckInvoked
    }

    if ($null -ne $LinksInvoked) {
        $dcService | Add-Member -MemberType NoteProperty -Name "links" -Value $LinksInvoked
    }

    if ($null -ne $DependsOnInvoked) {
        $dcService | Add-Member -MemberType NoteProperty -Name "depends_on" -Value $DependsOnInvoked
    }

    if ($null -ne $ExtraHostsInvoked) {
        $dcService | Add-Member -MemberType NoteProperty -Name "extra_hosts" -Value $ExtraHostsInvoked
    }

    if ($null -ne $ExposeInvoked) {
        $dcService | Add-Member -MemberType NoteProperty -Name "expose" -Value $ExposeInvoked
    }

    if (![string]::IsNullOrEmpty($ShmSize)) {
        $dcService | Add-Member -MemberType NoteProperty -Name "shm_size" -Value $ShmSize
    }

    if ($null -ne $CapAddInvoked) {
        $dcService | Add-Member -MemberType NoteProperty -Name "cap_add" -Value $CapAddInvoked
    }

    $dcService
}