<#
.SYNOPSIS
Scaffold an independent OutputPumps contributor module.
.DESCRIPTION
Define New-OPumperModule for the OutputPumps runtime. Create the manifest, contribution closure, bounded pump, documentation and isolated tests without overwriting a nonempty directory.
#>


function New-OPumperModule {
    <#
    .SYNOPSIS
    Scaffold an independent OutputPumps contributor module.
    .DESCRIPTION
    Create the manifest, contribution closure, bounded pump, documentation and isolated tests without overwriting a nonempty directory.
    #>
    
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')][string]$Name,
          [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_.-]+$')][string]$Executable,[Parameter(Mandatory)][string]$Path)
    try {
        $destination=$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
        if(Test-Path -LiteralPath $destination){if(!(Test-Path -LiteralPath $destination -PathType Container) -or @(Get-ChildItem -LiteralPath $destination -Force).Count){throw 'Destination must be absent or an empty directory.'}}
        if(!$PSCmdlet.ShouldProcess($destination,'Create an independent OutputPumps provider')){return [pscustomobject]@{Path=$destination;Changed=$false}}
        $id=$Name.ToLowerInvariant();$moduleName="$Name-OPumper";$projectName="FrenchExDev.OPumper.$Name.PoSh";$function="Invoke-Pump$Name"
        foreach($folder in @('src/Public','src/Private','src/Classes','src/Completers','src/Variables','test/Unit','test/Integration','test/Fixtures','resources','software')){[void][IO.Directory]::CreateDirectory((Join-Path $destination $folder))}
        $manifest=@"
@{
 RootModule='$moduleName.psm1';ModuleVersion='1.0.0';GUID='$([guid]::NewGuid())';Author='Your organization'
 PowerShellVersion='7.4';CompatiblePSEditions=@('Core');RequiredModules=@('OutputPump.PoSh')
 FunctionsToExport=@('$function');CmdletsToExport=@();VariablesToExport=@();AliasesToExport=@()
 PrivateData=@{OutputPumps=@{Version=1;ProviderId='$id';RegistrationScript='src/Setup-OutputPumps.ps1'}}
}
"@
        $loader=@'
<#
.SYNOPSIS
Load the independent __NAME__ OutputPumps provider.
.DESCRIPTION
Define provider commands without registering routes; Setup-OutputPumps activates contributions separately.
#>

#requires -Version 7.4
#requires -PSEdition Core
$public=@()
foreach($category in @('Classes','Private','Public','Completers','Variables')) {
    $folder=Join-Path $PSScriptRoot "src/$category"
    if(!(Test-Path -LiteralPath $folder -PathType Container)){continue}
    foreach($file in Get-ChildItem -LiteralPath $folder -Filter '*.ps1' -File -Recurse | Sort-Object FullName){. $file.FullName;if($category -eq 'Public'){$public+=$file.BaseName}}
}
Export-ModuleMember -Function $public
'@
        $pump=@'
<#
.SYNOPSIS
Define the structured __NAME__ CLI pump.
.DESCRIPTION
Keep streaming analysis local and expose useful business counters in one bounded result envelope.
#>


function __FUNCTION__ {
    <#
    .SYNOPSIS
    Summarize __NAME__ CLI events as structured data.
    .DESCRIPTION
    Analyze records until both streams complete, then publish counters through the shared capture engine.
    Replace the example counters with fields from the CLI's documented output format.
    #>
    [CmdletBinding()]
    param([string[]]$ArgumentList=@(),[string]$WorkingDirectory='.',[int]$MaxItems=20,[int]$MaxOutputBytes=8192,[int]$TimeoutSeconds=600,[Collections.IDictionary]$Environment)
    $state=@{EventCount=0;Warnings=0;Errors=0}
    Invoke-OutputPumpNative -Executable '__EXE__' -ArgumentList $ArgumentList -WorkingDirectory $WorkingDirectory -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -TimeoutSeconds $TimeoutSeconds -Environment $Environment -Summary '__NAME__ events summarized.' -State $state -OnRecord {
        param($record,$state)
        # Replace these counters with useful fields of your CLI's documented format.
        if($record.Stream -ne 'stdout' -or $record.Truncated -or !$record.Text){return}
        $state.EventCount++
        if($record.Text -match '\bWARN(?:ING)?\b'){$state.Warnings++}
        if($record.Text -match '\bERROR\b'){$state.Errors++}
    } -Parse {
        param($state,$capture)
        @{EventCount=$state.EventCount;Warnings=$state.Warnings;Errors=$state.Errors;Items=@()}
    }
}
'@
        $setup=@'
<#
.SYNOPSIS
Register the __NAME__ provider in the shared session registry.
.DESCRIPTION
Reuse the supplied module instance or import this provider, then capture its command reference in a closure.
Registration defines behavior without executing the CLI.
#>

#requires -Version 7.4
param([Parameter(Mandatory)][pscustomobject]$Context)
$module=$Context.Module
if(!$module){$module=Import-Module $Context.ManifestPath -PassThru -Global -DisableNameChecking -ErrorAction Stop}
$Context.Module=$module
& $module {
    param($context)
    $command=Get-Command '__FUNCTION__' -CommandType Function -ErrorAction Stop
    $handler={param($call) $parameters=$call.Parameters; & $command @parameters}.GetNewClosure()
    $route=@{Executable='__EXE__';Tokens=@();Options=@{};Positional='ArgumentList';Multiple=$true}
    [void](& $context.Register -Id '__ID__.run' -Route $route -Handler $handler -Command $command -ProviderId $context.ProviderId)
} $Context
'@
        $test=@'
<#
.SYNOPSIS
Unit tests for the generated __NAME__ pump.
.DESCRIPTION
Mock native capture and feed controlled records to the parser without requiring the CLI executable.
#>

BeforeAll {
    $engine=Get-Module OutputPump.PoSh | Select-Object -First 1
    if($engine){Import-Module -ModuleInfo $engine -Global}else{Import-Module OutputPump.PoSh -ErrorAction Stop}
    Import-Module (Join-Path $PSScriptRoot '../../__MODULE__.psd1') -DisableNameChecking
}
Describe '__FUNCTION__' {
    It 'returns useful data rather than native log lines' {
        Mock Invoke-OutputPumpNative -ModuleName '__MODULE__' {
            param($Parse,$OnRecord,$State)
            foreach($text in @('INFO ready','WARN slow')){& $OnRecord ([pscustomobject]@{Text=$text;Stream='stdout';Truncated=$false}) $State}
            $data=& $Parse $State ([pscustomobject]@{ExitCode=0;OmittedRecords=0})
            New-OutputPumpResult -Summary 'Fixture' -Data $data
        }
        $result=__FUNCTION__
        $result.Data.EventCount | Should -Be 2
        $result.Data.Warnings | Should -Be 1
        $result.Outcome | Should -Be Success
    }
}
'@
        $descriptor=@{Id=$id;Name=$Name;Purpose='Structured CLI events';Module="$moduleName.psm1";Documentation=@('https://example.org/replace-with-official-cli-docs');VerifiedVersion='replace-me';IdentityMarkdown='Replace with verified CLI documentation and supported formats.';Commands=@(@{Id="$id.run";Function=$function;When='Summarize CLI events';Example="$function -WorkingDirectory .";Parameters=@('ArgumentList','WorkingDirectory','Environment');Route=@{Executable=$Executable;Tokens=@();Options=@{};Positional='ArgumentList';Multiple=$true}})}
        $writes=@{"$moduleName.psd1"=$manifest;"$moduleName.psm1"=$loader;"src/Public/$function.ps1"=$pump;"src/Setup-OutputPumps.ps1"=$setup;"test/Unit/$function.Tests.ps1"=$test;'catalogue.json'=(ConvertTo-Json -InputObject @($descriptor) -Depth 12);'resources/dependencies.psd1'="@{ Pester='6.2.0' }";'.gitignore'=".cache/`ntest/Artifacts/`n";'README.md'="# $moduleName`n`nPowerShell 7.4+ Core provider for OutputPumps. DevPoSh is optional.`n`nAdd the parent directory to roots in ~/.pumps-config.yaml, import OutputPump.PoSh and call Setup-OutputPumps. Run Pump-Me $Executable.`n`nReplace the example parser and documentation URL with your CLI's verified machine format. Native arguments are arrays. The parser produces useful Data fields, never a raw log.`n`nTests use Pester 6.2.0: Invoke-Pester ./test/Unit. Install test dependencies explicitly. Live closures belong to the current session; persist reusable behavior in src/Setup-OutputPumps.ps1.`n"}
        $writes['index.md']=ConvertTo-PumpIndex @($descriptor)
        $writes["software/$id.md"]=ConvertTo-PumpIdentity $descriptor
        $writes['README.md']=@'
# __PROJECT__

Independent PowerShell 7.4+ Core provider for OutputPumps. DevPoSh is optional.

Use `__PROJECT__` as the project directory name. The module entry point is `__MODULE__.psd1`; its module identity and provider ID are independent of the directory name.

## Activate

Import the OutputPumps engine first. Add this provider's parent directory to `~/.pumps-config.yaml`:

```yaml
version: 2
roots:
  - path: C:/path/to/providers
providers: {}
pumps: {}
```

Create your profile if necessary, then edit it with `code $PROFILE`. Paste:

```powershell
Import-Module 'C:/path/to/FrenchExDev.OutputPumps.PoSh/OutputPump.PoSh.psd1' -DisableNameChecking
Setup-OutputPumps | Out-Null
```

Open a new shell and run `Pump-Me __EXE__`. The result is a bounded envelope whose `.Data` contains CLI-specific fields. A normal module import defines its commands without registering routes. Setup reuses modules already loaded by DevPoSh.

## Implement your parser

Replace `src/Public/__FUNCTION__.ps1` with a useful parser for your CLI's documented machine format. The starter reduces all records into counters using `-State` and `-OnRecord`; `-Parse` finalizes after process exit and EOF. Keep state bounded with counters and a limited preview. Reaching a preview limit never stops the process. Oversized records signal incomplete analysis; no partial record is parsed as complete. Replace placeholder documentation and version evidence in `catalogue.json` before publishing.

`src/Setup-OutputPumps.ps1` registers a `.GetNewClosure()` callback in the provider's module scope. Capture command references explicitly so private functions stay accessible. Registration must not run native commands. Provider options are available through `$Context.Options`.

Use `Register-OutputPump -Descriptor ... -LibraryRoot ...` to validate and update your software sheet and index. No runtime dependency on DevPoSh is required.

## Test

Use Pester **6.2.0** with experimental parallelism disabled. Import the engine, then run `Invoke-Pester ./test`. Unit tests mock process capture. The registration integration test uses an isolated YAML file and inventory, without launching the CLI.
'@
        $writes['test/Fixtures/events.log']="INFO ready`nWARN slow`n"
        $writes['test/Integration/Setup-OutputPumps.Tests.ps1']=@'
<#
.SYNOPSIS
Verify activation of the generated __NAME__ provider.
.DESCRIPTION
Use an isolated configuration and inventory to check route registration without launching the CLI.
#>

BeforeAll {
    $engine=Get-Module OutputPump.PoSh | Select-Object -First 1
    if($engine){Import-Module -ModuleInfo $engine -Global}else{Import-Module OutputPump.PoSh -ErrorAction Stop}
}
Describe '__MODULE__ contribution' {
    It 'registers its route without invoking the CLI' {
        $root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
        $configuration=Join-Path $TestDrive pumps.yaml
        [IO.File]::WriteAllText($configuration,"version: 2`nroots:`n - path: '$($root.Replace("'","''"))'`nproviders: {}`npumps: {}`n")
        $result=Setup-OutputPumps -ConfigurationPath $configuration -InventoryPath (Join-Path $TestDrive registry.json)
        $result.Outcome | Should -Be Success
        (Get-OutputPump -Id '__ID__.run').Data.Items.Count | Should -Be 1
    }
}
'@
        foreach($folder in @('src/Private','src/Classes','src/Completers','src/Variables')){$writes["$folder/.gitkeep"]=''}
        foreach($relative in $writes.Keys){$content=$writes[$relative].Replace('__FUNCTION__',$function).Replace('__EXE__',$Executable).Replace('__NAME__',$Name).Replace('__ID__',$id).Replace('__MODULE__',$moduleName).Replace('__PROJECT__',$projectName);[IO.File]::WriteAllText((Join-Path $destination $relative),$content,[Text.UTF8Encoding]::new($false))}
        New-PumpResult -Summary 'Independent OPumper module created.' -Data @{Items=@(@{Path=$destination;ProviderId=$id;Module=$moduleName})}
    }catch{New-PumpResult -Summary 'OPumper module could not be created.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='ScaffoldFailed';Message=$_.Exception.Message}}
}
