param(
    [Parameter(Mandatory=$true)]
    [string]$CoverageXmlPath
)

[xml]$xml = Get-Content $CoverageXmlPath

# Summary
$lr = [math]::Round([double]$xml.coverage.'line-rate' * 100, 1)
$br = [math]::Round([double]$xml.coverage.'branch-rate' * 100, 1)
Write-Host "`n=== Coverage Summary ==="
Write-Host ("Total: lines={0:F1}%  branches={1:F1}%" -f $lr, $br)
Write-Host ""

# Per-class detail
Write-Host ("{0,-55} {1,8} {2,10}" -f "Class", "Lines", "Branches")
Write-Host ("{0,-55} {1,8} {2,10}" -f "-----", "-----", "--------")

foreach ($pkg in $xml.coverage.packages.package) {
    foreach ($cls in $pkg.classes.class) {
        $name = $cls.name
        $clr = [math]::Round([double]$cls.'line-rate' * 100, 1)
        $cbr = [math]::Round([double]$cls.'branch-rate' * 100, 1)
        Write-Host ("{0,-55} {1,7:F1}% {2,9:F1}%" -f $name, $clr, $cbr)
    }
}
