#requires -Version 7.4
#requires -PSEdition Core
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$library=Split-Path (Split-Path $PSScriptRoot)
Import-Module (Join-Path $library 'Core.psm1') -Force
Import-Module (Join-Path $library 'OutputPumps.psd1') -Force -DisableNameChecking
foreach($name in @('Git','Ripgrep','PowerShell','Dotnet','Podman','Pnpm')){Import-Module (Join-Path $library "Modules/$name.psm1") -Force}
$workspace=Join-Path ([IO.Path]::GetTempPath()) ('output-pump-tests-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($workspace)
$failures=[Collections.Generic.List[object]]::new();$passed=0
function Assert($Condition,[string]$Message){if(!$Condition){throw $Message}}
function Assert-Envelope($Result,[int]$Budget=8192){
    Assert ($Result -is [pscustomobject]) 'Result must be a PSCustomObject'
    Assert ($Result.PSObject.Properties.Name.Count -eq 7) 'Unexpected envelope fields'
    Assert ($Result.Summary -notmatch '[\r\n]') 'Summary must have one line'
    Assert ($Result.Outcome -in @('Success','Failed','Timeout','Unsupported')) 'Unknown outcome'
    $json=ConvertTo-Json -InputObject $Result -Depth 24 -Compress
    Assert ([Text.Encoding]::UTF8.GetByteCount($json) -le $Budget) 'Output budget exceeded'
    Assert ($json -notmatch '"Stdout"\s*:|"Stderr"\s*:') 'Raw process streams leaked'
}
function Test-Case([string]$Name,[scriptblock]$Action){
    try{& $Action;$script:passed++}catch{$script:failures.Add(@{Test=$Name;Message=$_.Exception.Message;Line=$_.InvocationInfo.ScriptLineNumber})}
}
function Git-Run([string]$Directory,[string[]]$Arguments,[int[]]$Allowed=@(0)){
    $c=Invoke-PumpProcess git (@('-c','core.autocrlf=false')+$Arguments) $Directory 30
    Assert ($c.ExitCode -in $Allowed -and !$c.StartError) 'Git fixture setup failed'
}
function Write-Text([string]$Path,[string]$Text){[IO.File]::WriteAllText($Path,$Text,[Text.UTF8Encoding]::new($false))}
try {
    Test-Case 'PowerShell syntax and Core-only manifests' {
        foreach($file in (Get-ChildItem -LiteralPath $library -Recurse -File | Where-Object Extension -in '.ps1','.psm1','.psd1')){
            $tokens=$null;$errors=$null;$null=[Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
            Assert ($errors.Count -eq 0) "Syntax errors in $($file.Name)"
        }
        $manifest=Test-ModuleManifest (Join-Path $library 'OutputPumps.psd1')
        Assert ($manifest.PowerShellVersion -ge [version]'7.4') 'Core version requirement missing'
    }
    Test-Case 'Catalogue agrees with all public entry points' {
        $result=Test-OutputPumpCatalogue;Assert-Envelope $result
        Assert ($result.Outcome -eq 'Success' -and $result.Data.Items[0].SoftwareCount -ge 6) 'Catalogue invalid'
    }
    Test-Case 'Native scalar values remain typed after sanitizing' {
        $r=New-PumpResult -Summary 'Native scalars.' -Data @{Items=@(@{Present=(Test-Path -LiteralPath $library);Absent=(Test-Path -LiteralPath (Join-Path $workspace 'missing'));Number=42})}
        Assert-Envelope $r
        Assert ($r.Data.Items[0].Present -is [bool] -and $r.Data.Items[0].Present) 'True scalar lost'
        Assert ($r.Data.Items[0].Absent -is [bool] -and !$r.Data.Items[0].Absent) 'False scalar lost'
        Assert ($r.Data.Items[0].Number -eq 42) 'Numeric scalar lost'
    }
    Test-Case 'Object and byte budgets include every field' {
        $r=New-PumpResult -Summary ('é'*300) -Data @{Items=@(1..100|ForEach-Object {@{Text=('界'*700);Number=$_}})} -MaxItems 30 -MaxOutputBytes 1024
        Assert-Envelope $r 1024;Assert $r.Truncation.Applied 'Budget omission not signaled'
        Assert ($r.Truncation.Reasons -contains 'OutputBudget') 'Missing budget reason'
    }
    Test-Case 'Secrets sanitized before every output field' {
        $env:OUTPUT_PUMP_TEST_TOKEN='runtime-secret-value-1234'
        try{
            $payload='password="two word password"; token=visible-token; Bearer abc.def.ghi; https://user:pass@example.org; runtime-secret-value-1234; ghp_01234567890123456789'
            $r=New-PumpResult -Summary $payload -Data @{Items=@(@{Text=$payload;Environment='sensitive'})} -ErrorInfo @{Message=$payload}
            Assert-Envelope $r;$json=$r|ConvertTo-Json -Depth 24 -Compress
            foreach($value in @('two word password','visible-token','abc.def.ghi','user:pass','runtime-secret-value-1234','ghp_01234567890123456789')){Assert (!$json.Contains($value)) 'Secret leaked'}
            Assert ($r.Truncation.Reasons -contains 'SensitiveData') 'Redaction not signaled'
        }finally{Remove-Item Env:OUTPUT_PUMP_TEST_TOKEN}
    }
    Test-Case 'Missing executable is a structured failure' {
        $c=Invoke-PumpProcess ('missing-pump-'+[guid]::NewGuid().ToString('N')) @() $workspace 10
        $r=Complete-PumpProcess $c -Summary 'Missing executable test.';Assert-Envelope $r
        Assert ($r.Outcome -eq 'Failed' -and $r.Error.Code -eq 'StartFailed') 'Missing command not distinguished'
    }
    $flood=Join-Path $workspace 'flood.ps1'
    Write-Text $flood '$v="A"*512; for($i=0;$i -lt 18000;$i++){[Console]::Out.Write($v);[Console]::Error.Write($v)}'
    Test-Case 'Simultaneous huge streams and giant lines remain bounded' {
        $c=Invoke-PumpProcess (Join-Path $PSHOME 'pwsh.exe') @('-NoProfile','-NonInteractive','-File',$flood) $workspace 30 1024
        Assert ($c.ExitCode -eq 0 -and $c.Truncated) 'Flood not captured or not bounded'
        Assert ($c.Stdout.Length -le 1024 -and $c.Stderr.Length -le 1024) 'Internal capture exceeded bound'
        Assert ($c.StdoutCharacters -gt 8000000 -and $c.StderrCharacters -gt 8000000) 'Both streams not drained'
        $r=Complete-PumpProcess $c -Summary 'Flood test.';Assert-Envelope $r
        Assert ($r.Error.Code -eq 'CaptureLimit' -and $r.RetryAdvice -eq 'NarrowRequest') 'Capture overflow not explicit'
    }
    $sleep=Join-Path $workspace 'sleep.ps1';Write-Text $sleep 'Start-Sleep -Seconds 20'
    Test-Case 'Deadline terminates command without raw fallback' {
        $c=Invoke-PumpProcess (Join-Path $PSHOME 'pwsh.exe') @('-NoProfile','-NonInteractive','-File',$sleep) $workspace 1
        $r=Complete-PumpProcess $c -Summary 'Timeout test.';Assert-Envelope $r
        Assert ($r.Outcome -eq 'Timeout' -and $r.RetryAdvice -eq 'Unknown') 'Timeout not distinguished'
    }
    Test-Case 'Malformed machine output never becomes raw data' {
        $c=[pscustomobject]@{Stdout='invalid json password=never-display';Stderr='';StartError='';ExitCode=0;TimedOut=$false;Truncated=$false}
        $r=Complete-PumpProcess $c -Summary 'Invalid format.' -Parse {param($value) $value.Stdout|ConvertFrom-Json -ErrorAction Stop}
        Assert-Envelope $r;Assert ($r.Error.Code -eq 'UnrecognizedFormat') 'Malformed output not distinguished'
        Assert (!(($r|ConvertTo-Json -Depth 24 -Compress).Contains('never-display'))) 'Malformed source leaked'
    }
    $repoA=Join-Path $workspace 'répo one';$repoB=Join-Path $workspace 'répo two'
    foreach($repo in @($repoA,$repoB)){[void][IO.Directory]::CreateDirectory($repo);Git-Run $repo @('init','-b','main')}
    Test-Case 'Unborn branch and empty status' {
        $r=Get-PumpGitStatus -WorkingDirectory $repoA;Assert-Envelope $r
        Assert ($r.Outcome -eq 'Success' -and $r.Data.Branch -eq 'main' -and $r.Data.Head -eq '(initial)' -and $r.Data.Items.Count -eq 0) 'Unborn repository mishandled'
    }
    Write-Text (Join-Path $repoA 'é space.txt') "base`n";Git-Run $repoA @('add','--','é space.txt')
    Git-Run $repoA @('-c','user.name=OutputPumpTest','-c','user.email=test@example.invalid','commit','-m','initial')
    Test-Case 'Staged rename preserves Unicode and spaces' {
        Git-Run $repoA @('mv','--','é space.txt','renamed é.txt')
        $r=Get-PumpGitStatus -WorkingDirectory $repoA;Assert-Envelope $r
        Assert ($r.Outcome -eq 'Success' -and $r.Data.Items[0].OriginalPath -eq 'é space.txt' -and $r.Data.Items[0].Path -eq 'renamed é.txt') 'Rename paths lost'
        $diff=Get-PumpGitDiff -WorkingDirectory $repoA -Staged;Assert-Envelope $diff
        Assert ($diff.Outcome -eq 'Success' -and $diff.Data.Items[0].OriginalPath -eq 'é space.txt') 'Numstat rename lost'
        Write-Text (Join-Path $repoA 'renamed é.txt') "changed`n"
        $r=Get-PumpGitStatus -WorkingDirectory $repoA
        Assert ($r.Data.Items[0].Groups -contains 'Staged' -and $r.Data.Items[0].Groups -contains 'Unstaged') 'Combined staged/unstaged state lost'
    }
    Test-Case 'Targeted changed lines and limited history' {
        $r=Get-PumpGitChanges -WorkingDirectory $repoA -Path 'renamed é.txt';Assert-Envelope $r
        Assert ($r.Outcome -eq 'Success' -and $r.Data.Items.Count -eq 2 -and $r.Data.Items[1].Text -eq 'changed') 'Changed line extraction incorrect'
        $log=Get-PumpGitLog -WorkingDirectory $repoA -MaxItems 1;Assert-Envelope $log
        Assert ($log.Outcome -eq 'Success' -and $log.Data.Items[0].Subject -eq 'initial') 'History invalid'
        Write-Text (Join-Path $repoA 'renamed é.txt') "++counter;`n-----BEGIN PRIVATE KEY-----`nprivate-diff-material`n-----END PRIVATE KEY-----`n"
        $r=Get-PumpGitChanges -WorkingDirectory $repoA -Path 'renamed é.txt';Assert-Envelope $r
        Assert (@($r.Data.Items|Where-Object Text -eq '++counter;').Count -eq 1) 'Increment expression mistaken for a patch header'
        Assert (!(($r|ConvertTo-Json -Depth 24 -Compress).Contains('private-diff-material')) -and $r.Truncation.Reasons -contains 'SensitiveData') 'Private key in diff leaked or redaction not signaled'
    }
    Test-Case 'Truncated status followed by narrowed request' {
        1..35|ForEach-Object {Write-Text (Join-Path $repoB "file$_.txt") "needle $_`n"}
        $r=Get-PumpGitStatus -WorkingDirectory $repoB -MaxItems 2;Assert-Envelope $r
        Assert ($r.Truncation.Applied -and $r.Truncation.TotalCount -eq 35) 'Status truncation lost'
        $narrow=Get-PumpGitStatus -WorkingDirectory $repoB -Path 'file1.txt' -MaxItems 2
        Assert ($narrow.Outcome -eq 'Success' -and !$narrow.Truncation.Applied -and $narrow.Data.Items.Count -eq 1) 'Narrowed status not complete'
    }
    Test-Case 'Conflicts remain distinguishable' {
        Git-Run $repoA @('reset','--hard','HEAD');Git-Run $repoA @('checkout','-b','side')
        Write-Text (Join-Path $repoA 'é space.txt') "side`n";Git-Run $repoA @('add','.');Git-Run $repoA @('-c','user.name=OutputPumpTest','-c','user.email=test@example.invalid','commit','-m','side')
        Git-Run $repoA @('checkout','main');Write-Text (Join-Path $repoA 'é space.txt') "main`n";Git-Run $repoA @('add','.');Git-Run $repoA @('-c','user.name=OutputPumpTest','-c','user.email=test@example.invalid','commit','-m','main')
        Git-Run $repoA @('-c','user.name=OutputPumpTest','-c','user.email=test@example.invalid','merge','side') @(1)
        $r=Get-PumpGitStatus -WorkingDirectory $repoA;Assert-Envelope $r
        Assert ($r.Outcome -eq 'Success' -and $r.Data.Items[0].Groups -contains 'Conflict') 'Conflict lost'
    }
    Test-Case 'Ripgrep hits, no hits, errors and file discovery' {
        $r=Find-PumpText -WorkingDirectory $repoB -Pattern needle -MaxItems 2;Assert-Envelope $r
        Assert ($r.Outcome -eq 'Success' -and $r.Truncation.TotalCount -eq 35 -and $r.Truncation.Applied) 'Search count incorrect'
        $r=Find-PumpText -WorkingDirectory $repoB -Pattern absentvalue;Assert-Envelope $r
        Assert ($r.Outcome -eq 'Success' -and $r.ExitCode -eq 1 -and $r.Data.Items.Count -eq 0) 'No matches treated as failure'
        $r=Find-PumpText -WorkingDirectory $repoB -Pattern '[';Assert-Envelope $r
        Assert ($r.Outcome -eq 'Failed' -and $r.ExitCode -eq 2) 'Invalid pattern not failure'
        $r=Find-PumpFiles -WorkingDirectory $repoB -Glob 'file1.txt';Assert-Envelope $r
        Assert ($r.Outcome -eq 'Success' -and $r.Data.Items.Count -eq 1) 'File discovery invalid'
    }
    Test-Case 'File windows, filtered logs, giant lines and private keys' {
        $file=Join-Path $workspace 'events.log';Write-Text $file ((1..40|ForEach-Object {"error $_"}) -join "`n")
        $r=Get-PumpFile -Path $file -StartLine 10 -LineCount 3;Assert-Envelope $r
        Assert ($r.Data.Items.Count -eq 3 -and $r.Data.Items[0].Line -eq 10) 'File window incorrect'
        $r=Get-PumpLog -Path $file -Filter error -MaxItems 2;Assert-Envelope $r
        Assert ($r.Truncation.Applied -and $r.Data.Items[0].Line -eq 39 -and $r.Data.Items[1].Line -eq 40) 'Log tail incorrect'
        Write-Text $file (("x"*200000)+"`n-----BEGIN PRIVATE KEY-----`nsecret-private-material`n-----END PRIVATE KEY-----`n")
        $r=Get-PumpFile -Path $file;Assert-Envelope $r
        Assert ($r.Truncation.Applied -and !(($r|ConvertTo-Json -Depth 24 -Compress).Contains('secret-private-material'))) 'Private key or giant line leaked'
        $r=Get-PumpFile -Path $file -Filter '[';Assert ($r.Outcome -eq 'Failed') 'Invalid file filter not structured'
    }
    Test-Case 'Dotnet diagnostics, runner detection and TRX evidence' {
        $module=Get-Module -All -Name 'Dotnet-OPumper' | Select-Object -First 1
        $previousRunner=$env:DOTNET_TEST_RUNNER
        try {
            & $module {
                function script:Invoke-PumpProcess {
                    param($Executable,$Arguments,$WorkingDirectory,$TimeoutSeconds)
                    $script:LastArguments=$Arguments
                    if($Arguments[0] -eq 'test'){
                        $index=[array]::IndexOf($Arguments,'--results-directory');$path=$Arguments[$index+1];[void][IO.Directory]::CreateDirectory($path)
                        $xml='<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results><UnitTestResult testName="Example" outcome="Failed"><Output><ErrorInfo><Message>password=fixture-secret</Message></ErrorInfo></Output></UnitTestResult></Results><ResultSummary><Counters total="3" passed="1" failed="1" notExecuted="1"/></ResultSummary></TestRun>'
                        [IO.File]::WriteAllText((Join-Path $path 'fixture.trx'),$xml)
                        return [pscustomobject]@{Stdout='';Stderr='';StartError='';ExitCode=1;TimedOut=$false;Truncated=$false}
                    }
                    [pscustomobject]@{Stdout='app.cs(12,4): error CS1001: Example failure [app.csproj]';Stderr='';StartError='';ExitCode=1;TimedOut=$false;Truncated=$false}
                }
            }
            $r=Invoke-PumpDotnetBuild -Target 'app.csproj' -WorkingDirectory $repoB;Assert-Envelope $r
            Assert ($r.Outcome -eq 'Failed' -and $r.Data.Errors -eq 1 -and $r.Data.Items[0].Line -eq 12) 'Compiler diagnostics lost'
            foreach($runner in @('VSTest','Microsoft.Testing.Platform')){
                $env:DOTNET_TEST_RUNNER=$runner
                $r=Invoke-PumpDotnetTest -Target 'tests.csproj' -WorkingDirectory $repoB;Assert-Envelope $r
                Assert ($r.Outcome -eq 'Failed' -and $r.Data.Total -eq 3 -and $r.Data.Failed -eq 1 -and $r.Data.Skipped -eq 1) 'TRX evidence incorrect'
                Assert (!(($r|ConvertTo-Json -Depth 24 -Compress).Contains('fixture-secret'))) 'Test secret leaked'
                $argsUsed=& $module {$script:LastArguments}
                Assert ($(if($runner -eq 'VSTest'){$argsUsed -contains '--logger'}else{$argsUsed -contains '--report-trx' -and $argsUsed -contains '--project'})) 'Wrong runner arguments'
            }
            Remove-Item Env:DOTNET_TEST_RUNNER -ErrorAction SilentlyContinue
            Write-Text (Join-Path $repoB 'global.json') '{"test":{"runner":"Microsoft.Testing.Platform"}}'
            $r=Invoke-PumpDotnetTest -Target 'tests.csproj' -WorkingDirectory $repoB
            Assert ($r.Data.Runner -eq 'Microsoft.Testing.Platform') 'global.json runner ignored'
        } finally {
            if($null -eq $previousRunner){Remove-Item Env:DOTNET_TEST_RUNNER -ErrorAction SilentlyContinue}else{$env:DOTNET_TEST_RUNNER=$previousRunner}
            Import-Module (Join-Path (Split-Path $library) 'FrenchExDev.OPumper.Dotnet.PoSh/Dotnet-OPumper.psd1') -Force;Import-Module (Join-Path $library 'Modules/Dotnet.psm1') -Force
        }
    }
    Test-Case 'Dotnet missing reports never imply successful tests' {
        $module=Get-Module -All -Name 'Dotnet-OPumper' | Select-Object -First 1;$previousRunner=$env:DOTNET_TEST_RUNNER
        try{
            & $module {function script:Invoke-PumpProcess {param($Executable,$Arguments,$WorkingDirectory,$TimeoutSeconds);[pscustomobject]@{Stdout='';Stderr='';StartError='';ExitCode=0;TimedOut=$false;Truncated=$false}}}
            $env:DOTNET_TEST_RUNNER='VSTest';$r=Invoke-PumpDotnetTest -Target 'none.csproj' -WorkingDirectory $repoB;Assert-Envelope $r
            Assert ($r.Outcome -eq 'Failed' -and $r.Error.Code -eq 'TestReportMissing') 'Missing evidence treated as success'
            & $module {function script:Invoke-PumpProcess {param($Executable,$Arguments,$WorkingDirectory,$TimeoutSeconds);[pscustomobject]@{Stdout='unknown option --report-trx';Stderr='';StartError='';ExitCode=1;TimedOut=$false;Truncated=$false}}}
            $env:DOTNET_TEST_RUNNER='Microsoft.Testing.Platform';$r=Invoke-PumpDotnetTest -Target 'none.csproj' -WorkingDirectory $repoB;Assert-Envelope $r
            Assert ($r.Outcome -eq 'Unsupported' -and $r.Error.Code -eq 'TrxReporterMissing') 'Missing MTP reporter not distinguished'
        }finally{if($null -eq $previousRunner){Remove-Item Env:DOTNET_TEST_RUNNER -ErrorAction SilentlyContinue}else{$env:DOTNET_TEST_RUNNER=$previousRunner};Import-Module (Join-Path (Split-Path $library) 'FrenchExDev.OPumper.Dotnet.PoSh/Dotnet-OPumper.psd1') -Force;Import-Module (Join-Path $library 'Modules/Dotnet.psm1') -Force}
    }
    Test-Case 'Podman projection excludes command and environment secrets' {
        $module=Get-Module -All -Name 'Podman-OPumper' | Select-Object -First 1
        try{
            & $module {function script:Invoke-PumpProcess {
                param($Executable,$Arguments,$WorkingDirectory,$TimeoutSeconds)
                $json=if($Arguments[0] -eq 'ps'){'[{"Id":"123","Names":["app"],"Image":"image","State":"running","Status":"Up","ExitCode":0,"Command":"password=must-not-export","Env":{"SECRET":"hidden"}}]'}else{'[{"Id":"456","Names":["image:tag"],"Size":123,"Created":12}]'}
                [pscustomobject]@{Stdout=$json;Stderr='';StartError='';ExitCode=0;TimedOut=$false;Truncated=$false}
            }}
            $r=Get-PumpPodmanContainers;Assert-Envelope $r
            Assert ($r.Outcome -eq 'Success' -and $r.Data.Items[0].State -eq 'running') 'Container projection invalid'
            Assert (!(($r|ConvertTo-Json -Depth 24 -Compress).Contains('must-not-export'))) 'Container command leaked'
            $r=Get-PumpPodmanImages;Assert-Envelope $r;Assert ($r.Outcome -eq 'Success' -and $r.Data.Items[0].Size -eq 123) 'Image projection invalid'
            & $module {function script:Invoke-PumpProcess {param($Executable,$Arguments,$WorkingDirectory,$TimeoutSeconds);[pscustomobject]@{Stdout='';Stderr='cannot connect to socket';StartError='';ExitCode=125;TimedOut=$false;Truncated=$false}}}
            $r=Get-PumpPodmanContainers;Assert ($r.Outcome -eq 'Failed' -and $r.Error.Code -eq 'ConnectionFailed') 'Unavailable service treated as empty inventory'
        }finally{Import-Module (Join-Path (Split-Path $library) 'FrenchExDev.OPumper.Podman.PoSh/Podman-OPumper.psd1') -Force;Import-Module (Join-Path $library 'Modules/Podman.psm1') -Force}
    }
    Test-Case 'Podman bounded tail retains the newest events across streams' {
        $module=Get-Module -All -Name 'Podman-OPumper' | Select-Object -First 1
        try{
            & $module {function script:Invoke-PumpProcess {param($Executable,$Arguments,$WorkingDirectory,$TimeoutSeconds);[pscustomobject]@{Stdout="2026-10-10T10:00:01Z first`n2026-10-10T10:00:03Z last";Stderr='2026-10-10T10:00:02Z middle';StartError='';ExitCode=0;TimedOut=$false;Truncated=$false}}}
            $r=Get-PumpPodmanLog -Container app -MaxItems 2;Assert-Envelope $r
            Assert ($r.Outcome -eq 'Success' -and $r.Data.Items[0].Message -eq 'middle' -and $r.Data.Items[1].Message -eq 'last') 'Newest log events dropped'
            Assert ($null -eq $r.Truncation.TotalCount -and $r.Truncation.Applied) 'Tail completeness overstated'
        }finally{Import-Module (Join-Path (Split-Path $library) 'FrenchExDev.OPumper.Podman.PoSh/Podman-OPumper.psd1') -Force;Import-Module (Join-Path $library 'Modules/Podman.psm1') -Force}
    }
    Test-Case 'Pnpm projects and bounded dependency depth' {
        $module=Get-Module -All -Name 'Pnpm-OPumper' | Select-Object -First 1
        try{
            & $module {function script:Get-Command {param($Name,$CommandType);[pscustomobject]@{Source=(Join-Path $PSHOME 'pwsh.exe');Extension='.exe'}}}
            & $module {function script:Invoke-PumpProcess {param($Executable,$Arguments,$WorkingDirectory,$TimeoutSeconds);[pscustomobject]@{Stdout='[{"name":"app","dependencies":{"lib":{"version":"1.2.3","dependencies":{"child":{"version":"2.0.0"}}}}}]';Stderr='';StartError='';ExitCode=0;TimedOut=$false;Truncated=$false}}}
            $r=Get-PumpPnpmDependencies -WorkingDirectory $repoB -Depth 1;Assert-Envelope $r
            Assert ($r.Outcome -eq 'Success' -and $r.Data.Items.Count -eq 2 -and $r.Data.Items[1].Depth -eq 1) 'Dependency projection invalid'
        }finally{Import-Module (Join-Path (Split-Path $library) 'FrenchExDev.OPumper.Pnpm.PoSh/Pnpm-OPumper.psd1') -Force;Import-Module (Join-Path $library 'Modules/Pnpm.psm1') -Force}
    }
    Test-Case 'Generated calls run from two repositories and self-delete' {
        foreach($repo in @($repoA,$repoB)){
            $r=New-OutputPumpScript -SoftwareId git -Function Get-PumpGitStatus -Parameters @{WorkingDirectory=$repo;MaxItems=2}
            Assert-Envelope $r;Assert ($r.Outcome -eq 'Success') 'Script generation failed'
            $path=$r.Data.Items[0].ScriptPath
            $c=Invoke-PumpProcess (Join-Path $PSHOME 'pwsh.exe') @('-NoProfile','-NonInteractive','-File',$path) $repo 30
            Assert ($c.ExitCode -eq 0 -and !$c.Stderr) 'Caller leaked errors'
            $object=$c.Stdout|ConvertFrom-Json;Assert-Envelope $object
            Assert ($object.Outcome -eq 'Success' -and !(Test-Path -LiteralPath $path)) 'Caller failed or persisted'
        }
        $invalid=New-OutputPumpScript -SoftwareId git -Function Get-PumpGitStatus -Parameters @{MaxItems='invalid'}
        if($invalid.Outcome -eq 'Success'){$object=(& $invalid.Data.Items[0].ScriptPath)|ConvertFrom-Json;Assert-Envelope $object;Assert ($object.Outcome -eq 'Failed') 'Binding failure leaked'}
    }
    Test-Case 'Generated parameters preserve quotes and do not become code' {
        $file=Join-Path $workspace "quote'and$([char]0x2019)semi;.txt";Write-Text $file 'safe file data'
        $r=New-OutputPumpScript -SoftwareId powershell -Function Get-PumpFile -Parameters @{Path=$file;MaxItems=1}
        Assert ($r.Outcome -eq 'Success') 'Quoted call generation failed'
        $c=Invoke-PumpProcess (Join-Path $PSHOME 'pwsh.exe') @('-NoProfile','-NonInteractive','-File',$r.Data.Items[0].ScriptPath) $workspace 30
        Assert (!$c.Stderr -and $c.ExitCode -eq 0) 'Quoted parameter became executable text'
        $object=$c.Stdout|ConvertFrom-Json;Assert-Envelope $object
        Assert ($object.Outcome -eq 'Success' -and $object.Data.Items[0].Text -eq 'safe file data') 'Quoted file path was changed'
    }
    Test-Case 'MTP filters require an explicit framework option' {
        $previousRunner=$env:DOTNET_TEST_RUNNER
        try{$env:DOTNET_TEST_RUNNER='Microsoft.Testing.Platform';$r=Invoke-PumpDotnetTest -Target 'tests.csproj' -WorkingDirectory $repoB -Filter 'Example';Assert-Envelope $r;Assert ($r.Outcome -eq 'Unsupported' -and $r.Error.Code -eq 'MtpFilterOptionRequired') 'Framework filter was guessed'}
        finally{if($null -eq $previousRunner){Remove-Item Env:DOTNET_TEST_RUNNER -ErrorAction SilentlyContinue}else{$env:DOTNET_TEST_RUNNER=$previousRunner}}
    }
    Test-Case 'Real dotnet build reports success and compiler failure' {
        $project=Join-Path $workspace 'build-smoke';[void][IO.Directory]::CreateDirectory($project)
        $version=Invoke-PumpProcess dotnet @('--version') $project 30
        Assert ($version.ExitCode -eq 0 -and $version.Stdout -match '^(\d+)\.') 'SDK unavailable for build integration'
        $major=$Matches[1]
        Write-Text (Join-Path $project 'smoke.csproj') "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><TargetFramework>net$major.0</TargetFramework></PropertyGroup></Project>"
        Write-Text (Join-Path $project 'NuGet.Config') '<configuration><packageSources><clear /></packageSources></configuration>'
        Write-Text (Join-Path $project 'Example.cs') 'public class Example { public int Value => 42; }'
        $r=Invoke-PumpDotnetBuild -WorkingDirectory $project -Target 'smoke.csproj' -TimeoutSeconds 90;Assert-Envelope $r
        Assert ($r.Outcome -eq 'Success') 'Real minimal build failed'
        Write-Text (Join-Path $project 'Example.cs') 'public class Example { this is not C#; }'
        $r=Invoke-PumpDotnetBuild -WorkingDirectory $project -Target 'smoke.csproj' -NoRestore -TimeoutSeconds 90;Assert-Envelope $r
        Assert ($r.Outcome -eq 'Failed' -and $r.Data.Errors -gt 0) 'Real compiler failure not projected'
    }
    Test-Case 'Registration extension, idempotence, validation and locking' {
        $copy=Join-Path $workspace 'library';[void][IO.Directory]::CreateDirectory($copy)
        $descriptor=(Get-Content -LiteralPath (Join-Path $library 'catalogue.json') -Raw|ConvertFrom-Json -AsHashtable)[0]
        $providerName=([IO.Path]::GetFileNameWithoutExtension($descriptor.Module))+'-OPumper'
        $providerRoot=Join-Path (Split-Path $library) ('FrenchExDev.OPumper.'+[IO.Path]::GetFileNameWithoutExtension($descriptor.Module)+'.PoSh')
        foreach($item in Get-ChildItem -LiteralPath $providerRoot -Force){Copy-Item -LiteralPath $item.FullName -Destination $copy -Recurse}
        $descriptor.Module="$providerName.psm1"
        $descriptor.Id='reusable-example';$descriptor.Name='Reusable example'
        $r=Register-OutputPump -Descriptor $descriptor -LibraryRoot $copy;Assert ($r.Outcome -eq 'Success') 'New reusable module not registered'
        $paths=@('catalogue.json','index.md','software/reusable-example.md');$before=@{}
        foreach($p in $paths){$before[$p]=(Get-Item -LiteralPath (Join-Path $copy $p)).LastWriteTimeUtc.Ticks}
        $r=Register-OutputPump -Descriptor $descriptor -LibraryRoot $copy;Assert ($r.Data.Items[0].ChangedFiles -eq 0) 'Identical registration rewrote files'
        foreach($p in $paths){Assert ((Get-Item -LiteralPath (Join-Path $copy $p)).LastWriteTimeUtc.Ticks -eq $before[$p]) 'Timestamp changed'}
        $lock=[IO.File]::Open((Join-Path $copy '.catalogue.lock'),[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
        try{$r=Register-OutputPump -Descriptor $descriptor -LibraryRoot $copy;Assert ($r.Outcome -eq 'Failed') 'Concurrent lock ignored'}finally{$lock.Dispose()}
        $descriptor.Commands[0].Function='Get-PumpMissing';$r=Register-OutputPump -Descriptor $descriptor -LibraryRoot $copy;Assert ($r.Outcome -eq 'Failed') 'Invalid descriptor accepted'
    }
    Test-Case 'Installer preserves instructions and repeated installation is a no-op' {
        $homePath=Join-Path $workspace 'codex-home';[void][IO.Directory]::CreateDirectory($homePath);$agents=Join-Path $homePath 'AGENTS.md';Write-Text $agents "# Existing`nKeep this instruction.`n"
        $r=& (Join-Path $library 'Install-OutputPumps.ps1') -CodexHome $homePath;Assert-Envelope $r;Assert ($r.Outcome -eq 'Success') 'Installer failed'
        $text=[IO.File]::ReadAllText($agents);Assert ($text.StartsWith("# Existing`nKeep this instruction.`n")) 'Existing instructions changed'
        $ticks=(Get-Item -LiteralPath $agents).LastWriteTimeUtc.Ticks
        $r=& (Join-Path $library 'Install-OutputPumps.ps1') -CodexHome $homePath
        Assert (!$r.Data.Items[0].Changed -and (Get-Item -LiteralPath $agents).LastWriteTimeUtc.Ticks -eq $ticks) 'Installer rewrote unchanged instructions'
    }
} finally {
    $resolved=[IO.Path]::GetFullPath($workspace);$tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if($resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolved).StartsWith('output-pump-tests-')){Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue}
}
New-PumpResult -Summary "$passed tests passed; $($failures.Count) failed." -Outcome $(if($failures.Count){'Failed'}else{'Success'}) -Data @{Passed=$passed;Failed=$failures.Count;Items=$failures.ToArray()} -RetryAdvice $(if($failures.Count){'CorrectRequest'}else{'SafeToRetry'})
if($failures.Count){exit 1}
