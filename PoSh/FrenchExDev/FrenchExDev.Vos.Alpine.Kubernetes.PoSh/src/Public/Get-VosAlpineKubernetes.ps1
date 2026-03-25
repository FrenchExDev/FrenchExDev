enum VosAlpineKubernetesGroup {
    JumpBox
    ControlPlan
    Worker
}

enum VosAlpineKubernetesAction {
    Up
    Halt
    Destroy
}

function Get-VosAlpineKubernetes {
    [CmdletBinding()]
    param(
        [VosAlpineKubernetesAction[]] $Action,
        [VosAlpineKubernetesGroup[]] $Group,
        [ValidateNotNullOrWhiteSpace()] [string] $JumpBoxGroupName = "jb",
        [ValidateNotNullOrWhiteSpace()] [string] $ControlPlanGroupName = "cp",
        [ValidateNotNullOrWhiteSpace()] [string] $WorkerGroupName = "worker",
        [switch] $All,
        [switch] $Force
    )

    $InternalVerboseDebugConfig = @{
        Verbose = $VerbosePreference
        Debug   = $DebugPreference
    }

    $InternalGroups = if ($All) { $Group = @([VosAlpineKubernetesGroup]::JumpBox, [VosAlpineKubernetesGroup]::ControlPlan, [VosAlpineKubernetesGroup]::Worker) } 

    foreach ($currentGroup in $Group) {
        switch ([VosAlpineKubernetesGroup] $currentGroup) {
            ([VosAlpineKubernetesGroup]::JumpBox) { $InternalGroups += @($JumpBoxGroupName) }
            ([VosAlpineKubernetesGroup]::ControlPlan) { $InternalGroups += @($ControlPlanGroupName) }
            ([VosAlpineKubernetesGroup]::Worker) { $InternalGroups += @($WorkerGroupName) }
            default { throw "Group '$currentGroup' has not been implemented." }
        }
    }

    foreach ($currentAction in $Action ) {
        switch ([VosAlpineKubernetesAction] $currentAction) {
            ([VosAlpineKubernetesAction]::Up) {
                Get-VosMachineGroup Up, UpdateSshConfig $InternalGroups @InternalVerboseDebugConfig
            }
            ([VosAlpineKubernetesAction]::Halt) {
                $HaltGroups = $InternalGroups
                if ($All) { $([array]::Reverse($HaltGroups)) } else { $HaltGroups }
                Get-VosMachineGroup Halt $HaltGroups @InternalVerboseDebugConfig
            }
            ([VosAlpineKubernetesAction]::Destroy) {
                Get-VosMachineGroup Destroy $InternalGroups -Force:$Force @InternalVerboseDebugConfig
            }
            default {
                throw "Get-VosAlpineKubernetes > Action '$currentAction' is not yet implemented."
            }
        }
    }
}
