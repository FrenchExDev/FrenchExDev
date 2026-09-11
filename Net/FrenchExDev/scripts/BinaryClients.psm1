#requires -Version 7.0
Set-StrictMode -Version Latest

function Get-BinaryClient([string] $Root) {
    foreach ($directory in Get-ChildItem -LiteralPath $Root -Directory | Sort-Object Name) {
        $name = $directory.Name
        $project = Join-Path $directory.FullName "src/FrenchExDev.Net.$name.Design/FrenchExDev.Net.$name.Design.csproj"
        $script = Join-Path $directory.FullName 'scripts/Find-Missing.ps1'
        if (-not (Test-Path -LiteralPath $project) -or -not (Test-Path -LiteralPath $script)) { continue }
        $xml = [xml][IO.File]::ReadAllText($project)
        if (-not @($xml.SelectNodes('//ProjectReference') | Where-Object {
            $_.Include -match 'FrenchExDev\.Net\.BinaryWrapper\.Design\.Lib\.csproj$'
        }).Count) { continue }
        $solution = Join-Path $directory.FullName "FrenchExDev.Net.$name.slnx"
        if (-not (Test-Path -LiteralPath $solution)) { throw "Missing solution: $solution" }
        [pscustomobject]@{ Name = $name; Directory = $directory.FullName; Script = $script; Solution = $solution }
    }
}

function Select-BinaryClient($Available, [string[]] $Client, [string[]] $ExcludeClient) {
    $included = @($Client | ForEach-Object { $_.Split(',', [StringSplitOptions]::RemoveEmptyEntries) } | ForEach-Object { $_.Trim() })
    $excluded = @($ExcludeClient | ForEach-Object { $_.Split(',', [StringSplitOptions]::RemoveEmptyEntries) } | ForEach-Object { $_.Trim() })
    foreach ($name in @($included) + @($excluded)) {
        if ($name -notin @($Available.Name)) { throw "Unknown BinaryWrapper client '$name'. Available: $($Available.Name -join ', ')" }
    }
    $selected = @($Available | Where-Object {
        ($included.Count -eq 0 -or $_.Name -in $included) -and $_.Name -notin $excluded
    })
    if ($selected.Count -eq 0) { throw 'The client selection is empty.' }
    return $selected
}

function Set-BinaryStop([string] $Path) {
    if ($Path) {
        $stream = [IO.File]::Open($Path, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite)
        $stream.Dispose()
    }
}

function Start-BinaryProcess([string] $File, [string[]] $Arguments, [string] $Directory,
    [string] $Name, [string] $Stage, [string] $Log) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $File
    $info.WorkingDirectory = $Directory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    # dotnet/pwsh and the container CLIs emit UTF-8 even when the Windows
    # console uses an OEM code page. Decode before logging or displaying text.
    $info.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    $info.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
    $info.Environment['DOTNET_CLI_FORCE_UTF8_ENCODING'] = '1'
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $writer = [IO.StreamWriter]::new($Log, $false, [Text.UTF8Encoding]::new($false))
    $writer.AutoFlush = $true
    try {
        Write-Host "[$Name/$Stage] $File $($Arguments -join ' ')"
        $writer.WriteLine("$File $($Arguments -join ' ')")
        if (-not $process.Start()) { throw "Cannot start $File" }
        [pscustomobject]@{
            Process = $process; Name = $Name; Stage = $Stage; Log = $Log; Writer = $writer
            Started = [DateTime]::UtcNow; Recorded = $false; ExitCode = $null
            Readers = @(
                @{ Reader = $process.StandardOutput; Task = $process.StandardOutput.ReadLineAsync(); Done = $false },
                @{ Reader = $process.StandardError; Task = $process.StandardError.ReadLineAsync(); Done = $false }
            )
        }
    } catch {
        $writer.Dispose()
        $process.Dispose()
        throw
    }
}

function Wait-BinaryProcess([object[]] $Jobs, [string] $StopFile) {
    $heartbeat = [DateTime]::UtcNow
    try {
        do {
            $pending = $false
            foreach ($job in $Jobs) {
                if (-not $job.Recorded -and $job.Process.HasExited) {
                    $job.ExitCode = $job.Process.ExitCode
                    $job.Recorded = $true
                    if ($job.ExitCode -ne 0) { Set-BinaryStop $StopFile }
                }
                foreach ($reader in $job.Readers) {
                    # Bound draining so a chatty child cannot starve its sibling.
                    for ($i = 0; $i -lt 100 -and -not $reader.Done -and $reader.Task.IsCompleted; $i++) {
                        $line = $reader.Task.GetAwaiter().GetResult()
                        if ($null -eq $line) { $reader.Done = $true; break }
                        $job.Writer.WriteLine($line)
                        Write-Host "[$($job.Name)/$($job.Stage)] $line"
                        $reader.Task = $reader.Reader.ReadLineAsync()
                    }
                    if (-not $reader.Done) { $pending = $true }
                }
                if (-not $job.Recorded) { $pending = $true }
            }
            if ($pending) {
                if (([DateTime]::UtcNow - $heartbeat).TotalSeconds -ge 30) {
                    $active = @($Jobs | Where-Object { -not $_.Recorded } | ForEach-Object { "$($_.Name)/$($_.Stage)" })
                    $stopping = if ($StopFile -and (Test-Path -LiteralPath $StopFile)) { ' (stop requested; draining active versions)' } else { '' }
                    Write-Host "Running: $($active -join ', ')$stopping"
                    $heartbeat = [DateTime]::UtcNow
                }
                Start-Sleep -Milliseconds 100
            }
        } while ($pending)
        foreach ($job in $Jobs) {
            [pscustomobject]@{
                Client = $job.Name; Stage = $job.Stage; ExitCode = $job.ExitCode; Log = $job.Log
                StartedUtc = $job.Started.ToString('o'); FinishedUtc = [DateTime]::UtcNow.ToString('o')
            }
        }
    } finally {
        foreach ($job in $Jobs) {
            try {
                if (-not $job.Process.HasExited) {
                    Set-BinaryStop $StopFile
                    # Exceptional interruption only. Ordinary errors drain active versions.
                    $job.Process.Kill($true)
                    $job.Process.WaitForExit()
                }
            } finally {
                $job.Writer.Dispose()
                $job.Process.Dispose()
            }
        }
    }
}

function Save-BinaryReport($Report, [string] $Path) {
    $temporary = "$Path.tmp"
    [IO.File]::WriteAllText($temporary, ($Report | ConvertTo-Json -Depth 12), [Text.UTF8Encoding]::new($false))
    [IO.File]::Move($temporary, $Path, $true)
}

function Invoke-BinaryClients {
    [CmdletBinding(SupportsShouldProcess)]
    param([string] $Root, [hashtable] $Options, [string[]] $ExplicitOptions = @(), [string] $Resume, [switch] $List)
    $ErrorActionPreference = 'Stop'
    $Root = [IO.Path]::GetFullPath($Root)
    $previous = $null
    if ($Resume) {
        $previous = Get-Content -LiteralPath $Resume -Raw | ConvertFrom-Json -AsHashtable
        if ($previous.SchemaVersion -ne 1 -or $previous.Root -ne $Root) { throw 'Resume report belongs to another checkout or schema.' }
        foreach ($key in @($Options.Keys)) {
            if ($key -notin $ExplicitOptions -and $previous.Parameters.ContainsKey($key)) { $Options[$key] = $previous.Parameters[$key] }
        }
        # A successful old run is not evidence for a different selection mode or output.
        foreach ($key in 'OutputRoot','Runtime','Framework','Configuration','MinVersion','AllVersions','RetryKnownMissing','SkipTests','KeepImages') {
            if (($Options[$key] | ConvertTo-Json -Compress) -ne ($previous.Parameters[$key] | ConvertTo-Json -Compress)) {
                throw "Resume cannot change '$key'; start a fresh run to validate that configuration."
            }
        }
    }
    $available = @(Get-BinaryClient $Root)
    $selected = @(Select-BinaryClient $available $Options.Client $Options.ExcludeClient)
    foreach ($name in $Options.MinVersion.Keys) {
        if ($name -notin @($selected.Name) -or [string]::IsNullOrWhiteSpace($Options.MinVersion[$name])) { throw "Invalid MinVersion entry: $name" }
    }
    if ($previous) {
        $selected = @($selected | Where-Object { $previous.Clients[$_.Name] -ne 'Succeeded' })
    }
    if ($List) { return $selected }
    if ($selected.Count -eq 0) { Write-Host 'All selected clients already succeeded in the resume report.'; return }
    for ($offset = 0; $offset -lt $selected.Count; $offset += $Options.ClientParallel) {
        $last = [Math]::Min($offset + $Options.ClientParallel, $selected.Count) - 1
        Write-Host "Batch $([int]($offset / $Options.ClientParallel) + 1): $($selected[$offset..$last].Name -join ', ')"
    }
    Write-Host "Concurrency: $($Options.ClientParallel) clients x $($Options.Parallel) versions x $($Options.ScrapeParallel) help commands."
    if (-not $PSCmdlet.ShouldProcess(($selected.Name -join ', '), 'Build, collect, validate and clean client batches')) { return }

    $dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $pwsh = (Get-Command pwsh -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $runtime = (Get-Command $Options.Runtime -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    foreach ($item in $selected) {
        $command = Get-Command $item.Script -ErrorAction Stop
        foreach ($parameter in 'NoBuild','FailFast','StopFile','CleanImages','Configuration','RetryKnownMissing') {
            if (-not $command.Parameters.ContainsKey($parameter)) { throw "$($item.Name) launcher lacks -$parameter." }
        }
    }
    if (-not $Options.LogRoot) { $Options.LogRoot = Join-Path $Root '../../.fake/binary-clients' }
    $Options.LogRoot = [IO.Path]::GetFullPath($Options.LogRoot)
    if ($Options.OutputRoot) { $Options.OutputRoot = [IO.Path]::GetFullPath($Options.OutputRoot) }
    $runDirectory = Join-Path $Options.LogRoot ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
    # Keep the lock outside tracked source. Never delete a lock file another process could open.
    $lockDirectory = Join-Path $Root '../../.fake/binary-clients'
    $null = New-Item -ItemType Directory -Path $lockDirectory -Force
    try { $runLock = [IO.File]::Open((Join-Path $lockDirectory 'run.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
    catch { throw "Another batch launcher holds this checkout's lock: $lockDirectory/run.lock" }
    $reportPath = Join-Path $runDirectory 'report.json'
    $report = [ordered]@{
        SchemaVersion = 1; Root = $Root; Parameters = $Options; Status = 'Running'
        StartedUtc = [DateTime]::UtcNow.ToString('o'); FinishedUtc = $null
        Clients = @{}; Steps = [Collections.Generic.List[object]]::new(); Error = $null
    }
    if ($previous) { foreach ($name in $previous.Clients.Keys) { $report.Clients[$name] = $previous.Clients[$name] } }
    foreach ($item in $selected) { $report.Clients[$item.Name] = 'Pending' }

    function Invoke-Step($Item, [string] $Stage, [string] $File, [string[]] $Arguments) {
        $scriptLog = Join-Path $runDirectory ("{0:D3}-{1}-{2}.log" -f $report.Steps.Count, $Item.Name, $Stage)
        $job = Start-BinaryProcess $File $Arguments $Item.Directory $Item.Name $Stage $scriptLog
        $result = @(Wait-BinaryProcess @($job))
        foreach ($entry in $result) { $report.Steps.Add($entry) }
        Save-BinaryReport $report $reportPath
        if ($result[0].ExitCode -ne 0) { throw "$($Item.Name)/$Stage failed (exit $($result[0].ExitCode)). Log: $scriptLog" }
    }
    function Get-ClientArguments($Item, [switch] $Cleanup, [string] $StopFile) {
        $arguments = @('-NoLogo','-NoProfile','-NonInteractive','-File',$Item.Script,
            '-NoBuild','-Framework',$Options.Framework,'-Configuration',$Options.Configuration,'-Runtime',$Options.Runtime)
        if ($Options.OutputRoot) { $arguments += @('-Output', (Join-Path $Options.OutputRoot $Item.Name)) }
        if ($Cleanup) { return $arguments + '-CleanImages' }
        $arguments += @('-FailFast','-StopFile',$StopFile,'-Parallel',"$($Options.Parallel)",'-ScrapeParallel',"$($Options.ScrapeParallel)")
        if (-not $Options.AllVersions) { $arguments += '-Missing' }
        if ($Options.RetryKnownMissing) { $arguments += '-RetryKnownMissing' }
        if ($Options.KeepImages -and (Get-Command $Item.Script).Parameters.ContainsKey('KeepImages')) { $arguments += '-KeepImages' }
        if ($Options.MinVersion.ContainsKey($Item.Name)) { $arguments += @('-MinVersion', $Options.MinVersion[$Item.Name]) }
        return $arguments
    }
    function Invoke-Validation($Item, [string] $Prefix) {
        Invoke-Step $Item "$Prefix-build" $dotnet @('build',$Item.Solution,'--configuration',$Options.Configuration,'--nologo','--verbosity','minimal')
        if (-not $Options.SkipTests) {
            Invoke-Step $Item "$Prefix-test" $dotnet @('test',$Item.Solution,'--framework',$Options.Framework,
                '--configuration',$Options.Configuration,'--no-build','--nologo','--verbosity','minimal',
                '--results-directory',(Join-Path $runDirectory "$($Item.Name)-$Prefix-tests"),'--logger','trx')
        }
    }
    try {
        $null = New-Item -ItemType Directory -Path $runDirectory
        Save-BinaryReport $report $reportPath
        Write-Host "Report: $reportPath"
        $infrastructure = [pscustomobject]@{ Name = 'BinaryWrapper'; Directory = $Root; Solution = (Join-Path $Root 'BinaryWrapper/FrenchExDev.Net.BinaryWrapper.slnx') }
        Invoke-Step $infrastructure 'runtime' $runtime @('info')
        if (-not $Options.SkipTests) { Invoke-Validation $infrastructure 'pre' }

        for ($offset = 0; $offset -lt $selected.Count; $offset += $Options.ClientParallel) {
            $last = [Math]::Min($offset + $Options.ClientParallel, $selected.Count) - 1
            $batch = @($selected[$offset..$last])
            foreach ($item in $batch) {
                $report.Clients[$item.Name] = 'Building'
                Invoke-Validation $item 'pre'
            }
            $stopFile = Join-Path $runDirectory ("batch-{0}.stop" -f $offset)
            $jobs = [Collections.Generic.List[object]]::new()
            $batchError = $null
            try {
                try {
                    foreach ($item in $batch) {
                        $report.Clients[$item.Name] = 'Collecting'
                        $log = Join-Path $runDirectory ("collect-{0}.log" -f $item.Name)
                        $jobs.Add((Start-BinaryProcess $pwsh (Get-ClientArguments $item -StopFile $stopFile) $item.Directory $item.Name 'collect' $log))
                    }
                } catch {
                    $batchError = $_
                    Set-BinaryStop $stopFile
                }
                $results = @(Wait-BinaryProcess $jobs.ToArray() $stopFile)
                foreach ($entry in $results) { $report.Steps.Add($entry) }
                Save-BinaryReport $report $reportPath
                if ($batchError) { throw $batchError }
                if (@($results | Where-Object ExitCode -ne 0).Count -gt 0 -or (Test-Path -LiteralPath $stopFile)) {
                    throw "Collection failed in batch: $($batch.Name -join ', '). See collect-*.log in $runDirectory"
                }
                foreach ($item in $batch) {
                    $report.Clients[$item.Name] = 'Validating'
                    Invoke-Validation $item 'post'
                }
            } catch { $batchError = $_ }
            finally {
                # Both workers have exited. Try every client's cleanup even if one fails.
                if (-not $Options.KeepImages) {
                    foreach ($item in $batch) {
                        try { Invoke-Step $item 'clean' $pwsh (Get-ClientArguments $item -Cleanup) }
                        catch {
                            if (-not $batchError) { $batchError = $_ } else { Write-Warning $_.Exception.Message }
                        }
                    }
                }
            }
            if ($batchError) {
                foreach ($item in $batch) { $report.Clients[$item.Name] = 'Failed' }
                throw $batchError
            }
            foreach ($item in $batch) { $report.Clients[$item.Name] = 'Succeeded' }
            Save-BinaryReport $report $reportPath
        }
        $report.Status = 'Succeeded'
    } catch {
        $report.Status = 'Failed'
        $report.Error = $_.Exception.Message
        throw
    } finally {
        if ($report.Status -eq 'Running') { $report.Status = 'Interrupted' }
        $report.FinishedUtc = [DateTime]::UtcNow.ToString('o')
        try { Save-BinaryReport $report $reportPath }
        finally { $runLock.Dispose() }
        Write-Host "Report: $reportPath"
        if ($report.Status -ne 'Succeeded') { Write-Host "After fixing the failure: ./scripts/Invoke-BinaryClients.ps1 -Resume '$reportPath'" }
    }
}
Export-ModuleMember -Function Get-BinaryClient, Select-BinaryClient, Invoke-BinaryClients
