<#
.SYNOPSIS
Specialized compatibility commands for independent software providers.
.DESCRIPTION
Forward parameters to the selected provider through the runtime dispatcher instead of duplicating parsing implementations.
#>


function Get-PumpGitStatus {
    <#
    .SYNOPSIS
    Read bounded Git working-tree and branch state.
    .DESCRIPTION
    Forward the selected paths and limits to the independent Git provider through the runtime dispatcher.
    #>
    
param([string]$WorkingDirectory='.', [string[]]$Path=@(),
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'git' -Function 'Get-PumpGitStatus' -Parameters $PSBoundParameters
}

function Get-PumpGitDiff {
    <#
    .SYNOPSIS
    Read bounded Git diff statistics.
    .DESCRIPTION
    Forward revision, staging and path selections to the independent Git provider.
    #>
    
param([string]$WorkingDirectory='.', [string[]]$Path=@(), [switch]$Staged, [string]$Revision,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'git' -Function 'Get-PumpGitDiff' -Parameters $PSBoundParameters
}

function Get-PumpGitChanges {
    <#
    .SYNOPSIS
    Read bounded changes for a selected Git path.
    .DESCRIPTION
    Delegate parsing and capture to the Git provider while retaining the specialized compatibility command.
    #>
    
param([Parameter(Mandatory)][string]$Path, [string]$WorkingDirectory='.', [switch]$Staged,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'git' -Function 'Get-PumpGitChanges' -Parameters $PSBoundParameters
}

function Get-PumpGitLog {
    <#
    .SYNOPSIS
    Read a limited Git commit history.
    .DESCRIPTION
    Forward the revision, paths and output limits to the independent Git provider.
    #>
    
param([string]$WorkingDirectory='.', [string[]]$Path=@(), [string]$Revision='HEAD',
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'git' -Function 'Get-PumpGitLog' -Parameters $PSBoundParameters
}

function Find-PumpText {
    <#
    .SYNOPSIS
    Search text through the structured ripgrep provider.
    .DESCRIPTION
    Forward documented search filters and retain bounded matches rather than displaying native search output.
    #>
    
param([Parameter(Mandatory)][string]$Pattern, [string[]]$Path=@('.'), [string[]]$Glob=@(),
          [string]$WorkingDirectory='.', [switch]$Literal, [switch]$IgnoreCase,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'ripgrep' -Function 'Find-PumpText' -Parameters $PSBoundParameters
}

function Find-PumpFiles {
    <#
    .SYNOPSIS
    Discover files through the structured ripgrep provider.
    .DESCRIPTION
    Forward path and glob selections to the provider and return its bounded file inventory.
    #>
    
param([string[]]$Path=@('.'), [string[]]$Glob=@(), [string]$WorkingDirectory='.',
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'ripgrep' -Function 'Find-PumpFiles' -Parameters $PSBoundParameters
}

function Get-PumpDirectory {
    <#
    .SYNOPSIS
    Read a bounded filesystem inventory.
    .DESCRIPTION
    Delegate filtering and recursion to the PowerShell provider rather than printing a directory listing.
    #>
    
param([string]$Path='.', [string]$Filter='*', [switch]$Recurse, [switch]$Force,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'powershell' -Function 'Get-PumpDirectory' -Parameters $PSBoundParameters
}

function Get-PumpFile {
    <#
    .SYNOPSIS
    Read a selected bounded window of a text file.
    .DESCRIPTION
    Forward line and tail selections to the PowerShell provider, which sanitizes the retained content.
    #>
    
param([Parameter(Mandatory)][string]$Path, [ValidateRange(1,2147483647)][int]$StartLine=1,
          [ValidateRange(1,1000)][int]$LineCount=20, [string]$Filter, [switch]$Tail,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'powershell' -Function 'Get-PumpFile' -Parameters $PSBoundParameters
}

function Get-PumpLog {
    <#
    .SYNOPSIS
    Extract a bounded window of relevant log records.
    .DESCRIPTION
    Delegate log filtering and retained-item limits to the independent PowerShell provider.
    #>
    
param([Parameter(Mandatory)][string]$Path, [string]$Filter,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'powershell' -Function 'Get-PumpLog' -Parameters $PSBoundParameters
}

function Invoke-PumpDotnetBuild {
    <#
    .SYNOPSIS
    Build a project and retain useful structured diagnostics.
    .DESCRIPTION
    Forward project selections and limits to the .NET provider instead of displaying the build log.
    #>
    
param([Parameter(Mandatory)][string]$Target, [string]$WorkingDirectory='.',
          [string]$Configuration, [string]$Framework, [switch]$NoRestore,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'dotnet' -Function 'Invoke-PumpDotnetBuild' -Parameters $PSBoundParameters
}

function Invoke-PumpDotnetTest {
    <#
    .SYNOPSIS
    Run tests and retain useful counters and failures.
    .DESCRIPTION
    Delegate test-engine selection and report parsing to the independent .NET provider.
    #>
    
param([Parameter(Mandatory)][string]$Target, [string]$WorkingDirectory='.',
          [string]$Configuration, [string]$Framework, [string]$Filter, [string]$MtpFilterOption, [switch]$NoRestore, [switch]$NoBuild,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'dotnet' -Function 'Invoke-PumpDotnetTest' -Parameters $PSBoundParameters
}

function Get-PumpPodmanContainers {
    <#
    .SYNOPSIS
    Read a bounded container inventory.
    .DESCRIPTION
    Forward container selections to the Podman provider and return its parsed result envelope.
    #>
    
param([string]$WorkingDirectory='.', [string[]]$Filter=@(), [switch]$All,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'podman' -Function 'Get-PumpPodmanContainers' -Parameters $PSBoundParameters
}

function Get-PumpPodmanImages {
    <#
    .SYNOPSIS
    Read a bounded image inventory.
    .DESCRIPTION
    Forward image selections to the Podman provider instead of exposing native table output.
    #>
    
param([string]$WorkingDirectory='.', [string[]]$Filter=@(),
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'podman' -Function 'Get-PumpPodmanImages' -Parameters $PSBoundParameters
}

function Get-PumpPodmanLog {
    <#
    .SYNOPSIS
    Extract a bounded selection of container logs.
    .DESCRIPTION
    Delegate the requested log window and filters to the Podman provider.
    #>
    
param([Parameter(Mandatory)][string]$Container, [string]$Since, [string]$Filter, [string]$WorkingDirectory='.',
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'podman' -Function 'Get-PumpPodmanLog' -Parameters $PSBoundParameters
}

function Get-PumpPnpmDependencies {
    <#
    .SYNOPSIS
    Read a bounded dependency inventory.
    .DESCRIPTION
    Forward the package selection and limits to the pnpm provider for machine-format parsing.
    #>
    
param([string]$WorkingDirectory='.', [string[]]$Package=@(), [switch]$Recursive,
          [ValidateRange(0,5)][int]$Depth=0,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600)
Invoke-OutputPumpFunction -SoftwareId 'pnpm' -Function 'Get-PumpPnpmDependencies' -Parameters $PSBoundParameters
}

$script:PumpProxyNames = @('Get-PumpGitStatus','Get-PumpGitDiff','Get-PumpGitChanges','Get-PumpGitLog','Find-PumpText','Find-PumpFiles','Get-PumpDirectory','Get-PumpFile','Get-PumpLog','Invoke-PumpDotnetBuild','Invoke-PumpDotnetTest','Get-PumpPodmanContainers','Get-PumpPodmanImages','Get-PumpPodmanLog','Get-PumpPnpmDependencies')
