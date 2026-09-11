# Clone all FrenchExDev .PoSh repos from GitHub into PoSh/FrenchExDev/<Project>/
$ErrorActionPreference = 'Stop'

$targetDir = Join-Path $PSScriptRoot 'PoSh' 'FrenchExDev'
New-Item -Path $targetDir -ItemType Directory -Force | Out-Null

$repos = gh repo list frenchexdev --limit 200 --json name --jq '.[] | select(.name | test("\\.PoSh$")) | .name'

$cloned = 0
$skipped = 0
$failed = 0

foreach ($repo in $repos) {
    $dest = Join-Path $targetDir $repo
    if (Test-Path (Join-Path $dest '.git')) {
        Write-Host "SKIP  $repo (already cloned)"
        $skipped++
    }
    else {
        Write-Host "CLONE $repo ..."
        git clone "https://github.com/frenchexdev/$repo.git" $dest
        if ($LASTEXITCODE -eq 0) {
            $cloned++
        }
        else {
            Write-Host "FAIL  $repo" -ForegroundColor Red
            $failed++
        }
    }
}

Write-Host "`nDone: $cloned cloned, $skipped skipped, $failed failed"
