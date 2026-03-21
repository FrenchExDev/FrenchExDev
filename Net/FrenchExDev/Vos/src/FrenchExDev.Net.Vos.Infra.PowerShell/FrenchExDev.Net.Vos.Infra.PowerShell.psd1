@{
    RootModule = 'FrenchExDev.Net.Vos.Infra.PowerShell.dll'
    ModuleVersion = '0.1.0'
    GUID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'
    Author = 'FrenchExDev'
    CompanyName = 'FrenchExDev'
    Description = 'PowerShell binary module for Vos VM orchestration. Wraps FrenchExDev.Net.Vos for typed VM management with Vagrant and Podman backends.'
    PowerShellVersion = '7.4'
    CmdletsToExport = @(
        'Start-VosMachine',
        'Stop-VosMachine',
        'Remove-VosMachine',
        'Restart-VosMachine',
        'Get-VosMachineStatus',
        'Enter-VosMachineSsh'
    )
    FunctionsToExport = @()
    VariablesToExport = @()
    AliasesToExport = @(
        'vup',
        'vhalt',
        'vdestroy',
        'vssh',
        'vst',
        'gvm'
    )
    PrivateData = @{
        PSData = @{
            Tags = @('Vagrant', 'Podman', 'VM', 'Vos', 'Infrastructure')
        }
    }
}
