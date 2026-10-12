Get-VosMachine main 0000 Destroy,Up,SnapshotSave -Force -NoProvision -SnapshotSave "Initial" -Verbose
Get-VosMachine main 0000 Provision,SnapshotSave -SnapshotSave "Provisioned" -Verbose
Get-VosMachine main 0000 Reload,SnapshotSave -SnapshotSave "Ready" -Verbose
