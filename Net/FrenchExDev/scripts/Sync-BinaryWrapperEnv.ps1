#requires -Version 7.0
<#
.SYNOPSIS
Synchronise les .env des clients BinaryWrapper avec le .env commun.
.DESCRIPTION
Repère les projets immediats possedant un fichier .binary-wrapper.yaml.
Copie exactement le contenu de <Root>/.env dans le .env de chaque projet,
en le créant si nécessaire, et actualise ses copies .env imbriquées existantes.
Les fichiers déjà identiques ne sont pas réécrits. Les dossiers de compilation,
de cache et de tests, ainsi que les liens de dossiers, sont exclus.
Aucune valeur de variable n'est affichée. La source n'est jamais modifiée.
.PARAMETER Root
Répertoire contenant le .env commun et les projets. Par défaut : Net/FrenchExDev,
résolu depuis l'emplacement du script, indépendamment du répertoire courant.
.EXAMPLE
./Net/FrenchExDev/scripts/Sync-BinaryWrapperEnv.ps1 -WhatIf
Prévisualise les copies sans modifier les fichiers.
.EXAMPLE
./Net/FrenchExDev/scripts/Sync-BinaryWrapperEnv.ps1
Applique la synchronisation depuis Net/FrenchExDev/.env.
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [string] $Root = (Join-Path $PSScriptRoot '..')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$rootDirectory = Get-Item -LiteralPath $Root -Force
if (-not $rootDirectory.PSIsContainer) {
    throw "Root must be a directory: $Root"
}
$source = Join-Path $rootDirectory.FullName '.env'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "Shared .env not found: $source"
}

# Read one snapshot so every destination receives the same bytes, including BOM
# and line endings, even if the source is edited while the script is running.
$sourceBytes = [IO.File]::ReadAllBytes($source)
$sourceStream = [IO.MemoryStream]::new($sourceBytes, $false)
try {
    $sourceHash = (Get-FileHash -InputStream $sourceStream -Algorithm SHA256).Hash
}
finally {
    $sourceStream.Dispose()
}

$excludedDirectories = @(
    'bin', 'obj', 'packages', 'node_modules', 'TestResults', 'test', 'tests',
    '.git', '.vs', '.images', '.fake', '.agents', '.codex'
)
$wrappers = @(Get-ChildItem -LiteralPath $rootDirectory.FullName -Directory -Force |
    Where-Object {
        $_.Name -notin $excludedDirectories -and
        -not ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -and
        (Test-Path -LiteralPath (Join-Path $_.FullName '.binary-wrapper.yaml') -PathType Leaf)
    } | Sort-Object Name)
if ($wrappers.Count -eq 0) {
    throw "No BinaryWrapper projects (.binary-wrapper.yaml) found in $($rootDirectory.FullName)."
}

foreach ($wrapper in $wrappers) {
    $targets = [Collections.Generic.List[string]]::new()
    $targets.Add((Join-Path $wrapper.FullName '.env'))
    $pending = [Collections.Generic.Stack[IO.DirectoryInfo]]::new()
    $pending.Push($wrapper)
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        foreach ($child in Get-ChildItem -LiteralPath $directory.FullName -Directory -Force) {
            if ($child.Name -in $excludedDirectories -or
                ($child.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                continue
            }
            $pending.Push($child)
            $nestedEnv = Join-Path $child.FullName '.env'
            if (Test-Path -LiteralPath $nestedEnv -PathType Leaf) {
                $targets.Add($nestedEnv)
            }
        }
    }

    foreach ($target in $targets | Sort-Object) {
        $exists = Test-Path -LiteralPath $target
        if ($exists) {
            $item = Get-Item -LiteralPath $target -Force
            if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw "Destination must be a regular file: $target"
            }
        }
        if ($exists -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ceq $sourceHash) {
            $status = 'Unchanged'
        }
        elseif ($PSCmdlet.ShouldProcess($target, "Copy shared configuration from $source")) {
            [IO.File]::WriteAllBytes($target, $sourceBytes)
            $status = if ($exists) { 'Updated' } else { 'Created' }
        }
        else {
            $status = if ($WhatIfPreference) { 'WhatIf' } else { 'Skipped' }
        }
        [PSCustomObject]@{
            Wrapper = $wrapper.Name
            Path = [IO.Path]::GetRelativePath($rootDirectory.FullName, $target)
            Status = $status
        }
    }
}
