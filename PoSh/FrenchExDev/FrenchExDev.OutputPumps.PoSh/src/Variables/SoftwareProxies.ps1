function Get-PumpGitStatus {
param([string]$WorkingDirectory='.', [string[]]$Path=@(),
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'git' -Function 'Get-PumpGitStatus' -Parameters $PSBoundParameters
}

function Get-PumpGitDiff {
param([string]$WorkingDirectory='.', [string[]]$Path=@(), [switch]$Staged, [string]$Revision,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'git' -Function 'Get-PumpGitDiff' -Parameters $PSBoundParameters
}

function Get-PumpGitChanges {
param([Parameter(Mandatory)][string]$Path, [string]$WorkingDirectory='.', [switch]$Staged,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'git' -Function 'Get-PumpGitChanges' -Parameters $PSBoundParameters
}

function Get-PumpGitLog {
param([string]$WorkingDirectory='.', [string[]]$Path=@(), [string]$Revision='HEAD',
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'git' -Function 'Get-PumpGitLog' -Parameters $PSBoundParameters
}

function Find-PumpText {
param([Parameter(Mandatory)][string]$Pattern, [string[]]$Path=@('.'), [string[]]$Glob=@(),
          [string]$WorkingDirectory='.', [switch]$Literal, [switch]$IgnoreCase,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'ripgrep' -Function 'Find-PumpText' -Parameters $PSBoundParameters
}

function Find-PumpFiles {
param([string[]]$Path=@('.'), [string[]]$Glob=@(), [string]$WorkingDirectory='.',
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'ripgrep' -Function 'Find-PumpFiles' -Parameters $PSBoundParameters
}

function Get-PumpDirectory {
param([string]$Path='.', [string]$Filter='*', [switch]$Recurse, [switch]$Force,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'powershell' -Function 'Get-PumpDirectory' -Parameters $PSBoundParameters
}

function Get-PumpFile {
param([Parameter(Mandatory)][string]$Path, [ValidateRange(1,2147483647)][int]$StartLine=1,
          [ValidateRange(1,1000)][int]$LineCount=20, [string]$Filter, [switch]$Tail,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'powershell' -Function 'Get-PumpFile' -Parameters $PSBoundParameters
}

function Get-PumpLog {
param([Parameter(Mandatory)][string]$Path, [string]$Filter,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'powershell' -Function 'Get-PumpLog' -Parameters $PSBoundParameters
}

function Invoke-PumpDotnetBuild {
param([Parameter(Mandatory)][string]$Target, [string]$WorkingDirectory='.',
          [string]$Configuration, [string]$Framework, [switch]$NoRestore,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'dotnet' -Function 'Invoke-PumpDotnetBuild' -Parameters $PSBoundParameters
}

function Invoke-PumpDotnetTest {
param([Parameter(Mandatory)][string]$Target, [string]$WorkingDirectory='.',
          [string]$Configuration, [string]$Framework, [string]$Filter, [string]$MtpFilterOption, [switch]$NoRestore, [switch]$NoBuild,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'dotnet' -Function 'Invoke-PumpDotnetTest' -Parameters $PSBoundParameters
}

function Get-PumpPodmanContainers {
param([string]$WorkingDirectory='.', [string[]]$Filter=@(), [switch]$All,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'podman' -Function 'Get-PumpPodmanContainers' -Parameters $PSBoundParameters
}

function Get-PumpPodmanImages {
param([string]$WorkingDirectory='.', [string[]]$Filter=@(),
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'podman' -Function 'Get-PumpPodmanImages' -Parameters $PSBoundParameters
}

function Get-PumpPodmanLog {
param([Parameter(Mandatory)][string]$Container, [string]$Since, [string]$Filter, [string]$WorkingDirectory='.',
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'podman' -Function 'Get-PumpPodmanLog' -Parameters $PSBoundParameters
}

function Get-PumpPnpmDependencies {
param([string]$WorkingDirectory='.', [string[]]$Package=@(), [switch]$Recursive,
          [ValidateRange(0,5)][int]$Depth=0,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'pnpm' -Function 'Get-PumpPnpmDependencies' -Parameters $PSBoundParameters
}

$script:PumpProxyNames = @('Get-PumpGitStatus','Get-PumpGitDiff','Get-PumpGitChanges','Get-PumpGitLog','Find-PumpText','Find-PumpFiles','Get-PumpDirectory','Get-PumpFile','Get-PumpLog','Invoke-PumpDotnetBuild','Invoke-PumpDotnetTest','Get-PumpPodmanContainers','Get-PumpPodmanImages','Get-PumpPodmanLog','Get-PumpPnpmDependencies')
