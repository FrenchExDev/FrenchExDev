---
name: quiet-cli
description: Run CLIs through local scripts written by the AI, analyze their output, and read only a useful bounded JSON result. Use for commands, builds, tests, searches, and diagnostics whose output may be large.
---

# Quiet CLI

Reduce the tokens read after calling a program by computing the answer locally. **The AI writes the contents of the scriptblock**: it chooses the commands, arguments, conditions, parsers, counters, and final projection. The supplied runtime handles processes, streams, and the result envelope; it does not infer which information is useful.

A question about the current branch needs a branch name. A question about repository cleanliness needs counters and a boolean. Reviewing a change then requires targeted content. Define this need before running the program.

This `SKILL.md` is the reference document for users; the guidance and examples below do not require reading `PROMPT.md`.

## Result contract

Every companion returns the same envelope. `.Data` can be any serializable value: a `PSCustomObject`, array, dictionary, scalar, or `null`. Choose its shape according to the question rather than forcing every answer into a collection.

```text
SchemaVersion : 1
Summary       : brief description
Outcome       : Success | Failed | Timeout | Unsupported
Data          : domain-specific value computed by the script
Execution     : ExitCode, AnalysisComplete, and optional duration/step counters
Error         : selected and sanitized diagnostic, or null
Truncation    : Applied, Reasons
```

`Execution.AnalysisComplete` means that analysis of the native output is complete. It does not certify that every detail was transmitted or that the result answers every possible question. Add a domain qualifier such as `AnswerComplete`, `CountExact`, or `Scope` when needed. Unknown results are `null`, never invented zeros.

A preview can be omitted while a count remains exact. Conversely, an incomplete search does not establish that no matches exist. Interpret the envelope and the domain qualifiers together before using `.Data`.

## Prepare the call

1. Define `.Data`: expected properties, units, scope, and conditions that establish a complete answer. Unknown data is `null`.
2. Verify arguments and formats in the official documentation, the installed version's manpage, or the source code. Read only the relevant sections. Keep the verification source in the reusable script or its reference.
3. Reduce output at the source when this preserves the request's meaning: `git branch --show-current`, `git diff --numstat -z`, a path filter, and so on. A narrower scope must appear in `.Data.Scope`.
4. Write or adapt a companion script in PowerShell **Core 7.4+**, or Bash for a Bash environment. Pass arguments as an array; use `WorkingDirectory` in QuietCli. Preserve the requested command's effects and authorization boundaries.
5. Consume the output inside the script. Read only its compact JSON. Check `Outcome`, `Execution.AnalysisComplete`, `Error`, and `Truncation` before using `.Data`.

The default budget is **4096 UTF-8 bytes for the entire task**, or 2048 if the answer fits. This limit measures bytes, not an exact token count. Do not display captures, a full patch, stderr, or logs on failure. A command already known to be short and sufficient may be called directly.

## Capture and analyze locally

- Run the intended command exactly once with its arguments and working directory. Machine-readable format options may replace terminal formatting after their equivalence has been established.
- Do not construct commands by concatenation or use `eval` or `Invoke-Expression`. When shell syntax is necessary, explicitly invoke that shell with an inspectable script.
- Capture stdout and stderr simultaneously and keep both streams inside the companion. Do not emit progress, a full transcript, or raw captures, including on failure.
- Prefer documented JSON, NDJSON, XML, or delimited records. For verbose or long-running processes, reduce records as they arrive into counters, bounded state, domain calculations, and a small selection of diagnostics.
- Bound internal capture for complete JSON/XML documents. If a document exceeds the limit, return an incomplete or failed result; never parse its prefix as a complete document.
- Set a timeout and distinguish `Timeout` from omitted output. A preview limit or final JSON budget must not terminate the process before the necessary analysis finishes.
- Select relevant diagnostics and sanitize known secrets before transmission. Redaction rules are an additional safety net, not permission to return raw captures.

## Bundled runtime and AI-authored scriptblock

Determine `$skillRoot` from this `SKILL.md` file's path. The examples using `scripts/` and `references/` require those companion resources alongside the file; downloading `SKILL.md` alone does not supply them. Check their availability before calling them.

When the packaged runtime is present, import it:

```powershell
Import-Module (Join-Path $skillRoot 'scripts/runtime/QuietCli.psd1') -ErrorAction Stop
```

The runtime needs no OutputPumps registry, DevPoSh, or third-party PowerShell module. Git must be installed for the Git examples. [Exact API and limits, when packaged](references/runtime.md).

In this source tree, the runtime and demonstration companions are at the `FrenchExDev.QuietCli.PoSh` module root, two directories above this file. Set `$moduleRoot` to that directory, or to the extracted module directory when using a downloaded copy, then import:

```powershell
Import-Module (Join-Path $moduleRoot 'QuietCli.PoSh.psd1') -ErrorAction Stop
```

The module's [README](../../README.md) documents its runtime contract and supplied measurements. The demonstrations below use `$moduleRoot` explicitly so they do not depend on the current working directory or a developer-specific repository path.

Choose the mode according to the verified format:

- **Bounded document**: `-Parse { param($capture) ... }`, with local access to `$capture.Stdout` and `$capture.ExitCode`. Emit a single domain value; for an array, use `return ,$items`.
- **Record stream**: `-State`, `-OnRecord { param($record,$state) ... }`, then `-Complete { param($state,$capture) ... }`. `OnRecord` updates bounded state and emits nothing. The default delimiter is LF; choose `([char]0)` for Git `-z`.
- **Composition**: `Invoke-QuietTask -AsJson { ... }`; wrap **every locally consumed call** in `Invoke-QuietStep`, including a result assigned to a variable. Intermediate steps do not return to the model.

The task body emits only the final domain projection. After a failed step or missing `.Data`, stop operations that depend on it. A value computed by a parser before a native failure is classified does not establish success.

Tasks can use preceding steps' `.Data`, compute aggregates, and select subsequent commands through conditions or loops. Keep those decisions and intermediate results local. When a new model decision is necessary, end the current phase with a compact state that supports choosing the next step.

## "Which branch am I on?"

Exact native command: **`git branch --show-current`**. Empty output with exit code 0 means detached HEAD; the name of a branch without its first commit is still available. [Manpage and semantics](references/git.md#branch).

The AI can produce this complete scriptblock; `$repo` is the repository directory:

```powershell
Invoke-QuietTask -AsJson -MaxOutputBytes 2048 -ArgumentList @($repo) {
    param($repository)
    $branch = Invoke-QuietStep branch {
        Invoke-QuietCli -FilePath git -WorkingDirectory $repository `
            -ArgumentList @('branch','--show-current') -Parse {
                param($capture)
                $name = $capture.Stdout.TrimEnd("`r","`n")
                if ($name.Contains("`r") -or $name.Contains("`n")) {
                    throw 'Unexpected branch response.'
                }
                [pscustomobject]@{
                    Branch = $(if ($name.Length) { $name } else { $null })
                    Detached = !$name.Length
                }
            }
    }
    if ($branch.Outcome -ne 'Success' -or $null -eq $branch.Data) { return }
    [pscustomobject]@{
        Branch=$branch.Data.Branch
        Detached=$branch.Data.Detached
        AnswerComplete=$true
    }
}
```

Saved and tested version:

```powershell
& "$skillRoot/scripts/Get-QuietGitBranch.ps1" -Repository $repo
```

`Pump-Me` belongs to a different API, OutputPumps. Do not invent `Pump-Me git branch --show-current`: verify its route before using it. This skill's examples use the QuietCli API actually bundled here.

## "Is the repository clean? How many files are affected?"

Exact native command:

```text
git --no-optional-locks status --porcelain=v1 -z --untracked-files=all --ignore-submodules=none -- .
```

The v1 `-z` format provides NUL-delimited records. A rename contains two paths; counting its two records as two files would be incorrect. `--untracked-files=all` counts untracked files, whereas `normal` may group a directory. Ignored files are excluded. [Grammar, conflicts, and scope](references/git.md#status).

The tested parsers are reusable; the AI still writes the selection and answer:

```powershell
Import-Module "$skillRoot/scripts/QuietGit.psm1"
Invoke-QuietTask -AsJson -MaxOutputBytes 2048 -ArgumentList @($repo) {
    param($repository)
    $state = New-QuietGitStatusState
    $status = Invoke-QuietStep status {
        Invoke-QuietCli -FilePath git -WorkingDirectory $repository `
            -ArgumentList @('--no-optional-locks','status','--porcelain=v1','-z',
                            '--untracked-files=all','--ignore-submodules=none','--','.') `
            -RecordDelimiter ([char]0) -State $state -OnRecord {
                param($record,$state)
                Add-QuietGitStatusRecord $record $state
            } -Complete {
                param($state,$capture)
                Complete-QuietGitStatus $state $capture
            }
    }
    if ($status.Outcome -ne 'Success' -or $null -eq $status.Data) { return }
    # The AI selects only the properties needed to answer its question.
    [pscustomobject]@{
        Scope='Current directory and descendants; ignored files excluded'
        Clean=$status.Data.Clean
        ChangedTracked=$status.Data.ChangedTracked
        Untracked=$status.Data.Untracked
        Conflicts=$status.Data.Conflicts
        CountExact=$status.Data.CountExact
    }
}
```

Complete version:

```powershell
& "$skillRoot/scripts/Get-QuietGitStatus.ps1" -Repository $repo
& "$skillRoot/scripts/Get-QuietGitStatus.ps1" -Repository $repo -Pathspec @('src/','tests/')
```

`Staged` and `Unstaged` may count the same file; their sum is not the number of distinct files. Conflicts are counted separately. `Clean` does not certify synchronization with a remote, the absence of ignored files, or an atomic snapshot during concurrent changes.

For a combined branch and cleanliness overview, the module also supplies [Generated-GitOverview.ps1](../../examples/Generated-GitOverview.ps1):

```powershell
& (Join-Path $moduleRoot 'examples/Generated-GitOverview.ps1') `
    -Repository $repo -MaxOutputBytes 2048
```

It reads the branch and reduces NUL-delimited status records locally. An illustrative `.Data` value is:

```json
{"Branch":"main","Detached":false,"Changed":2,"Untracked":1,"Clean":false,"AnswerComplete":true}
```

These are illustrative values, not measurements of the current repository. This companion counts Git entries with `--untracked-files=normal`, so an untracked directory may count as one entry. Use the earlier `--untracked-files=all` example when the question requires untracked file counts. Transmit file names only when the question requires them.

## Prepare and validate a noisy job

Question: "Prepare and then validate 2,000 items."

Use two steps that run the intended CLI, reduce its events, and verify its completion marker. Validation depends on successful preparation. Return the prepared and validated counts in one envelope that preserves failures.

The module's [Generated-NoisyJob.ps1](../../examples/Generated-NoisyJob.ps1) runs the local [demonstration CLI](../../fixtures/noisy-job.py) and requires Python 3:

```powershell
& (Join-Path $moduleRoot 'examples/Generated-NoisyJob.ps1') `
    -Python python3 -Records 2000 -MaxOutputBytes 2048
```

Use `-Python python` when that is the installed Python 3 executable's name. The companion uses `-State`, `-OnRecord`, and `-Complete`, checking each stage's completion marker before continuing. On success, `.Data` contains:

```json
{
  "Operation": "prepare-and-validate",
  "AnswerComplete": true,
  "Prepared": 2000,
  "Validated": 2000,
  "Stages": [
    { "Stage": "prepare", "Processed": 2000, "DiagnosticRecords": 2000 },
    { "Stage": "validate", "Processed": 2000, "DiagnosticRecords": 2000 }
  ]
}
```

Read the single final envelope. Failed or incomplete preparation prevents validation; the task preserves the failure rather than presenting successful zero counts.

## Compose a companion with local decisions

Question: "Which repository am I in, and how many commits are reachable from its current HEAD?"

This complete example requires Git and the imported QuietCli runtime. It locates the repository, uses that step's `.Data` as the next command's working directory, and emits one compact JSON document:

```powershell
Invoke-QuietTask -MaxOutputBytes 2048 -AsJson `
    -ArgumentList @((Get-Location).Path) -ScriptBlock {
    param($directory)

    $root = Invoke-QuietStep -Name repository -ScriptBlock {
        Invoke-QuietCli -FilePath git `
            -ArgumentList @('rev-parse', '--show-toplevel') `
            -WorkingDirectory $directory -TimeoutSeconds 10 `
            -Summary 'Repository root located.' -Parse {
                param($capture)
                $capture.Stdout.Trim()
            }
    }
    if ($root.Outcome -ne 'Success' -or !$root.Execution.AnalysisComplete) {
        return
    }

    $commits = Invoke-QuietStep -Name commits -ScriptBlock {
        Invoke-QuietCli -FilePath git `
            -ArgumentList @('rev-list', '--count', 'HEAD') `
            -WorkingDirectory $root.Data -TimeoutSeconds 30 `
            -Summary 'Reachable commits counted.' -Parse {
                param($capture)
                [long]::Parse(
                    $capture.Stdout.Trim(),
                    [Globalization.CultureInfo]::InvariantCulture
                )
            }
    }
    if ($commits.Outcome -ne 'Success' -or !$commits.Execution.AnalysisComplete) {
        return
    }

    [pscustomobject]@{
        Repository = $root.Data
        CommitCount = $commits.Data
        Scope = 'Commits reachable from HEAD'
        AnswerComplete = $true
    }
}
```

Both calls go through `Invoke-QuietStep`, including the results assigned to variables. Captures remain local, and `Invoke-QuietTask -AsJson` applies the 2048-byte budget to the final envelope. A directory outside a repository or a HEAD without a commit produces a failure, not an inferred zero count.

## "What changed in this version?" — a complex diff on Packer

Define the comparison first:

| Question | Diff arguments | Saved example |
|---|---|---|
| Working directory versus index | `git diff` | `-Mode Unstaged` |
| Index versus HEAD, or the empty tree before the first commit | `git diff --cached` | `-Mode Staged` |
| Working directory versus a commit | `git diff A` | `-Mode WorkingTree -Before A` |
| Two commit states | `git diff A B` | `-Mode Between -Before A -After B` |
| Changes in B since its common ancestor with A | `git diff A...B` | `-Mode Topic -Before A -After B` |

The examples add `--no-pager`, `--no-color`, `--no-ext-diff`, `--no-textconv`, `--numstat`, `-z`, and `--find-renames=50%`. They resolve references to full IDs before running the diff. `Topic` explicitly finds the common ancestor and requires it to be unique. [Exact commands and numstat grammar](references/git.md#diff).

Real example, verified on **hashicorp/packer**, tags **v1.11.0 → v1.11.1**:

```powershell
& "$skillRoot/scripts/Get-QuietGitDiff.ps1" -Repository $packerRepo `
    -Mode Between -Before v1.11.0 -After v1.11.1 -Top 3
```

The verified answer counts **93 files, 5548 added lines, and 672 deleted lines**. The total remains exact; only three paths are transmitted. The other 90 omitted paths are declared in `.Data.Changes.PathsOmitted`, and the envelope signals the preview limit. [Reproducible setup, SHAs, results, and limits](references/packer.md).

The AI can write a variant that reuses only the numstat grammar:

```powershell
# $beforeSha and $afterSha are the verified commits in references/packer.md.
Import-Module "$skillRoot/scripts/QuietGit.psm1"
Invoke-QuietTask -AsJson -MaxOutputBytes 2048 `
    -ArgumentList @($packerRepo,$beforeSha,$afterSha) {
    param($repository,$before,$after)
    $state = New-QuietGitDiffState -Top 0
    $diff = Invoke-QuietStep diff {
        Invoke-QuietCli -FilePath git -WorkingDirectory $repository `
            -ArgumentList @('--no-pager','diff','--no-color','--no-ext-diff',
                            '--no-textconv','--numstat','-z','--find-renames=50%',
                            $before,$after,'--','packer/') `
            -RecordDelimiter ([char]0) -State $state -OnRecord {
                param($record,$state)
                Add-QuietGitDiffRecord $record $state
            } -Complete {
                param($state,$capture)
                Complete-QuietGitDiff $state $capture
            }
    }
    if ($diff.Outcome -ne 'Success' -or $null -eq $diff.Data) { return }
    [pscustomobject]@{
        Scope=[pscustomobject]@{Before=$before;After=$after;Pathspec='packer/'}
        Files=$diff.Data.Files
        TextAdded=$diff.Data.TextAdded
        TextDeleted=$diff.Data.TextDeleted
        BinaryFiles=$diff.Data.BinaryFiles
        CountExact=$diff.Data.CountExact
    }
}
```

This variant answers only for `packer/`: its figures are not the repository totals cited above. Binary files have unknown line counts, never invented zeros. A statistical summary is not a review of the patch's content. To inspect the code, then choose relevant paths and hunks. [Other precise questions, including the presence of changes and targeted patch reading](references/more-examples.md).

## Adapt to another program

The program does not need a pre-existing provider. Read its documentation, define the expected result, then write the appropriate scriptblock. For JSON/XML, set a bounded capture and reject incomplete documents. For NDJSON, lines, or delimited records, reduce progressively without accumulating every record.

An exact count requires normal process completion, complete analysis, and the completion markers documented by the program. Stopping after the first twenty results does not establish an exact total. Preview limits must not interrupt the necessary analysis.

If the JSON is insufficient, prepare a more targeted question or correct the contract. Do not automatically rerun an operation, especially one with side effects. Do not increase the amount read by retrieving the raw transcript. [Contract, errors, timeouts, and output budget](references/runtime.md).

## Use a Bash companion

In a Bash environment, return the same JSON contract. Build JSON with `jq` or an explicit serializer, check the native process and reducer exit codes separately, drain stderr as well as stdout, and declare dependencies. The PowerShell runtime is not required for this mode.

The module's [Bash Git companion](../../examples/generated-git-status.sh) requires Bash 4+, Git, awk, jq, and GNU `timeout`. From the extracted module directory:

```bash
bash ./examples/generated-git-status.sh /path/to/repository
```

Replace the path with the repository to inspect. Read only the resulting envelope and apply the same checks for outcome, analysis completeness, diagnostics, and omissions.

## Verify an adaptation

Run `scripts/Test-QuietCli.ps1` for the supplied examples. For a new parser, verify the meaningful cases of its format: success, no results, native failure, invalid format, omitted records, and timeout. The bundled runtime and examples were tested with PowerShell 7.6.6 and Git 2.55.0.windows.3 on October 12, 2026; [verification results](references/validation.json).

Keep reusable parsers and calculations in tested functions or modules; the ad hoc script describes the current task. Do not claim measured token savings without comparable measurements.

When distributing this reference, preserve any supplied `scripts/` and `references/` paths. The module demonstrations additionally need the module root, `examples/`, and `fixtures/`. If a downloaded copy contains only `SKILL.md`, obtain the required runtime and companions separately or write a companion from the contract above; do not assume the referenced files are installed.
