@{
    RootModule = 'OutputPumps.psm1'
    ModuleVersion = '2.0.0'
    GUID = '7eb1dbe4-c7cd-49c2-8fdb-8e08f09c3a67'
    Author = 'FrenchExDev'
    Description = 'Object-based output pumps and explicit compilation for PowerShell Core.'
    PowerShellVersion = '7.4'
    CompatiblePSEditions = @('Core')
    FunctionsToExport = @('Pump-Me','Get-ConfiguredOutputPumps','New-OutputPumpCompilation','ConvertTo-OutputPumpJson','Register-OutputPump','New-OutputPumpScript','Test-OutputPumpCatalogue','Get-PumpHelp','Get-PumpGitStatus','Get-PumpGitDiff','Get-PumpGitChanges','Get-PumpGitLog','Find-PumpText','Find-PumpFiles','Get-PumpDirectory','Get-PumpFile','Get-PumpLog','Invoke-PumpDotnetBuild','Invoke-PumpDotnetTest','Get-PumpPodmanContainers','Get-PumpPodmanImages','Get-PumpPodmanLog','Get-PumpPnpmDependencies')
    AliasesToExport = @('pump')
    CmdletsToExport = @()
    VariablesToExport = @()
    PrivateData = @{ PSData = @{ Tags = @('OutputPump','PowerShell7') }; DevPoSh = @{Primary=$false;Compiler='Default'} }
}
