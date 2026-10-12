function Operate-VosMachineGroup {
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseApprovedVerbs', '', Scope = 'Function')]
    [CmdletBinding()]
    param(
        [ValidateNotNull()] [parameter(Position = 0)] [string] $Group,
        [ValidateNotNull()] [parameter(Position = 1)] [object] $Config,
        [ValidateNotNull()] [parameter(Position = 2)] [scriptblock] $Operation
    )

    $MachineNameInstances = $($config.GetEnabledMachines() | Where-Object { $_.Name -eq $Group }).Value.instances.length

    for ($i = 0; $i -le $MachineNameInstances - 1; $i++) {
        $id = $Config.FormatInstance($i)
        Invoke-Command $Operation -ArgumentList $Group, $id
    }      
}
