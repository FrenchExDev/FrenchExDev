function Get-PumpPnpmDependencies {
    [CmdletBinding()]
    param([string]$WorkingDirectory='.', [string[]]$Package=@(), [switch]$Recursive,
          [ValidateRange(0,5)][int]$Depth=0,
          [ValidateRange(1,1000)][int]$MaxItems=20, [ValidateRange(1024,8192)][int]$MaxOutputBytes=8192,
          [ValidateRange(1,86400)][int]$TimeoutSeconds=600,[Collections.IDictionary]$Environment)
    $binary=Get-Command pnpm -CommandType Application -ErrorAction SilentlyContinue | Where-Object Extension -eq '.exe' | Select-Object -First 1
    $executable=if($binary){$binary.Source}else{'pnpm'}
    if($IsWindows -and !$binary) {
        $launcher=Get-Command pnpm -ErrorAction SilentlyContinue | Select-Object -First 1
        if($launcher){$candidate=Join-Path (Split-Path $launcher.Source) 'node_modules/pnpm/pnpm.exe';if(Test-Path -LiteralPath $candidate){$executable=$candidate}}
        if($executable -eq 'pnpm'){return New-PumpResult -Summary 'A direct pnpm executable is required.' -Outcome Unsupported -RetryAdvice CorrectRequest -ErrorInfo @{Code='DirectExecutableMissing';Message='Resolve pnpm.exe without a cmd/PowerShell launcher.'} -MaxOutputBytes $MaxOutputBytes}
    }
    foreach($p in $Package){if($p.StartsWith('-')){return New-PumpResult -Summary 'Invalid package filter.' -Outcome Failed -RetryAdvice CorrectRequest -ErrorInfo @{Code='InvalidPackage'} -MaxOutputBytes $MaxOutputBytes}}
    $arguments=@('list','--json','--depth',$Depth.ToString());if($Recursive){$arguments+='--recursive'};$arguments+=$Package
    $capture=Invoke-PumpProcess $executable $arguments $WorkingDirectory $TimeoutSeconds -Environment $Environment
    Complete-PumpProcess $capture -Summary 'pnpm dependencies: selected packages and versions.' -MaxItems $MaxItems -MaxOutputBytes $MaxOutputBytes -Parse {
        param($c)
        if($c.ExitCode -ne 0 -and !$c.Stdout.Trim()){return @{Items=@()}}
        if(!$c.Stdout.TrimStart().StartsWith('[')){throw 'Expected pnpm array'}
        $projects=ConvertFrom-Json -InputObject $c.Stdout -AsHashtable -ErrorAction Stop
        $items=[Collections.Generic.List[object]]::new()
        function Add-Dependencies($dependencies,$project,$kind,$level) {
            if($null -eq $dependencies){return}
            foreach($name in ($dependencies.Keys | Sort-Object)) {
                $entry=$dependencies[$name];$items.Add(@{Project=$project;Name=$name;Version=$entry.version;Kind=$kind;Depth=$level})
                if($entry.ContainsKey('dependencies')){Add-Dependencies $entry.dependencies $project 'Transitive' ($level+1)}
            }
        }
        foreach($project in $projects){$projectName=if($project.ContainsKey('name')){$project['name']}elseif($project.ContainsKey('path')){Split-Path $project['path'] -Leaf}else{'(unnamed)'};foreach($kind in @('dependencies','devDependencies','optionalDependencies')){if($project.ContainsKey($kind)){Add-Dependencies $project[$kind] $projectName $kind 0}}}
        @{Items=$items.ToArray();Projects=@($projects).Count;Depth=$Depth}
    }
}
