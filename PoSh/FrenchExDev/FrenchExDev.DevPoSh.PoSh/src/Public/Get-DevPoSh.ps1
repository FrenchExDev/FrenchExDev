enum PoShAction { Pwsh; Command; Ide; Compile; CompileAll }

function Get-DevPoSh {
    [CmdletBinding(DefaultParameterSetName='All')]
    param(
        [Parameter(Mandatory,Position=0,ParameterSetName='Project',ValueFromPipelineByPropertyName)][string]$Acme,
        [Parameter(Mandatory,Position=1,ParameterSetName='Project',ValueFromPipelineByPropertyName)][string]$Name,
        [Parameter(Mandatory,Position=2,ValueFromPipelineByPropertyName)][PoShAction[]]$Action,
        [string]$Command,[string]$Ide,[string[]]$Path,
        [Parameter(ParameterSetName='All')][string]$CompilationRoot=(Join-Path $HOME '.posh.posh.compilations'),
        [string]$ConfigurationPath=(Join-Path $HOME '.pumps-config.yaml')
    )
    $all=$Action -contains [PoShAction]::CompileAll
    if($all -and ($Action.Count -ne 1 -or $Acme -or $Name)){throw 'CompileAll cannot be combined with project actions or identifiers.'}
    if(!$all -and $PSCmdlet.ParameterSetName -ne 'Project'){throw 'Specify Acme and Name for project actions.'}
    if($all) {
        $sourceRoot=$env:FRENCHEXDEV_MYDEVPOSH_PATH
        if(!$Path -and [string]::IsNullOrWhiteSpace($sourceRoot)){throw 'Configure FRENCHEXDEV_MYDEVPOSH_PATH or specify -Path.'}
        $selection=if($Path){$Path}else{@($sourceRoot)}
    } else {
        if(@($Path).Count -gt 1){throw 'Legacy project Path accepts one suffix.'}
        $selection=@(Join-Path $PWD "PoSh/$Acme/$Name/$($Path | Select-Object -First 1)")
        if(!(Test-Path -LiteralPath $selection[0])){throw 'Project directory does not exist.'}
        $sourceRoot=if($env:FRENCHEXDEV_MYDEVPOSH_PATH){$env:FRENCHEXDEV_MYDEVPOSH_PATH}else{Join-Path $PWD 'PoSh'}
    }
    foreach($current in $Action) {
        switch($current) {
            Pwsh { & (Join-Path $PSHOME $(if($IsWindows){'pwsh.exe'}else{'pwsh'})) -Login -WorkingDirectory $selection[0] }
            Command { if(!$Command){throw 'Command is required.'}; & (Join-Path $PSHOME $(if($IsWindows){'pwsh.exe'}else{'pwsh'})) -Login -WorkingDirectory $selection[0] -Command $Command }
            Ide { Get-Ide $Ide -open $selection[0] -WorkingDirectory $selection[0] }
            {$_ -in @([PoShAction]::Compile,[PoShAction]::CompileAll)} {
                $projects=@(Get-DevPoShProjects $selection)
                $dependencies=@{}; $entries=[Collections.Generic.List[object]]::new(); $reports=[Collections.Generic.List[object]]::new()
                foreach($project in $projects){$dependencies[$project.Name]=Join-Path $CompilationRoot "$($project.Acme)/$(Split-Path $project.Path -Leaf)/$($project.Name).psm1"}
                foreach($project in $projects) {
                    try {
                        $target=$dependencies[$project.Name]
                        $bundle=New-DevPoShProjectBundle $project $target $dependencies
                        $report=Export-DevPoShCompilation -ScriptText $bundle.Code -Destination $target -Assets $bundle.Assets -Metadata @{Project=$project.Name;Source=$project.Path}
                        $reports.Add([pscustomobject]@{Project=$project.Name;Outcome=$report.Outcome;Changed=$report.Changed;Path=$report.Path})
                        if($report.Outcome -eq 'Success'){$entries.Add([pscustomobject]@{Project=$project.Name;Path=$report.Path;SourceRoot=[IO.Path]::GetFullPath($(if($sourceRoot){$sourceRoot}else{Split-Path (Split-Path $project.Path)}))})}
                    }catch{$reports.Add([pscustomobject]@{Project=$project.Name;Outcome='Failed';Changed=$false;Path=$null})}
                }
                if($entries.Count){Update-DevPoShStartup $CompilationRoot $entries.ToArray() ([IO.Path]::GetFullPath($(if($sourceRoot){$sourceRoot}else{$selection[0]}))) -Replace:($all -and !$Path -and !@($reports|Where-Object Outcome -ne Success).Count)}
                $failures=@($reports | Where-Object Outcome -ne Success).Count
                $result=[pscustomobject]@{
                    Summary='DevPoSh explicit compilation completed.'
                    Outcome=$(if($failures){'Failed'}else{'Success'})
                    Data=[pscustomobject]@{
                        Compiled=@($reports|Where-Object {$_.Outcome -eq 'Success' -and $_.Changed}).Count
                        Unchanged=@($reports|Where-Object {$_.Outcome -eq 'Success' -and !$_.Changed}).Count
                        Failed=$failures
                        Items=@($reports|Select-Object -First 20)
                    }
                    Truncation=[pscustomobject]@{Applied=($reports.Count -gt 20);ReturnedCount=[Math]::Min(20,$reports.Count);TotalCount=$reports.Count;NextRequest='Select fewer projects with -Path.'}
                }
                while([Text.Encoding]::UTF8.GetByteCount((ConvertTo-Json -InputObject $result -Depth 12 -Compress)) -gt 8192 -and $result.Data.Items.Count){
                    $result.Data.Items=@($result.Data.Items | Select-Object -First ($result.Data.Items.Count-1))
                    $result.Truncation.Applied=$true
                    $result.Truncation.ReturnedCount=$result.Data.Items.Count
                }
                $result
            }
        }
    }
}
