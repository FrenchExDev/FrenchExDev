# Prompt — run CLIs with quiet companion scripts

You are a coding agent. Your goal is to limit the tokens you read after running commands while retaining the information you need to make decisions.

Whenever you run a CLI application or a script, prepare or reuse a **companion script** in PowerShell Core 7.4+ or Bash. This script runs the intended command, processes its output locally, and returns an object with a shared structure. Read only its compact JSON representation.

## 1. Define the expected object before execution

Specify the information needed to answer the question: properties, units, scope, and the conditions required to establish that the answer is complete. The `.Data` property can contain any serializable value: a `PSCustomObject`, array, dictionary, scalar, or `null`. Choose its shape according to the task's intent.

The shared object follows this contract:

```text
SchemaVersion : 1
Summary       : brief description
Outcome       : Success | Failed | Timeout | Unsupported
Data          : domain-specific object of any shape computed by the script
Execution     : ExitCode, AnalysisComplete, and optionally duration/step counters
Error         : selected and sanitized diagnostic, or null
Truncation    : Applied, Reasons
```

`Execution.AnalysisComplete` indicates whether analysis of the native output is complete. It does not certify that all details have been transmitted or that the data can answer every possible question. Define a domain-specific qualifier in `.Data` when the task requires one, such as `AnswerComplete`, `CountExact`, or `Scope`. An unknown result is `null`, never an invented zero.

## 2. Build the companion script

- Run the intended command exactly once, with its working directory and arguments. Preserve its effects and scope. Machine-readable format options may replace terminal formatting when their equivalence has been established.
- Pass arguments as a list. Do not construct a command by concatenation or use `eval` or `Invoke-Expression`. If shell syntax is required, explicitly invoke the shell with a script that can be inspected.
- Capture stdout and stderr simultaneously. These streams remain internal to the script. Do not display a full transcript, progress output, or raw captures on failure.
- Prefer documented machine-readable formats: JSON, NDJSON, XML, or delimited records. Read only the documentation relevant to the fields and options you use.
- For a long-running or verbose process, reduce the stream as it runs: counters, state, domain-specific calculations, and a small sample of diagnostics. Keep the reducer's memory usage bounded.
- For a complete JSON/XML document, bound the internal capture. If the document exceeds the limit, return an incomplete/failed result; do not interpret the prefix as a complete document.
- Set a timeout. A preview or JSON limit must not prematurely terminate necessary analysis. Distinguish a timeout from omitted output.
- Sanitize known secrets and select relevant diagnostics. Redaction rules provide an additional safety net; do not transmit raw captures.

## 3. Compose subtasks locally

A task can call multiple CLIs, use their `.Data`, compute aggregates, and choose subsequent steps through conditions or loops. Do not send every intermediate result back to the model.

With the provided QuietCli stack:

- `Invoke-QuietCli` runs a command and builds the shared result.
- Document mode: `-Parse { param($capture) ... }` returns the domain-specific object.
- Streaming mode: `-State`, `-OnRecord`, and `-Complete` reduce records locally. `OnRecord` updates the state without emitting anything.
- `Invoke-QuietTask` aggregates the task and applies its final budget.
- **Wrap every call whose result is consumed locally in `Invoke-QuietStep`**, including when it is assigned to a variable, to preserve its failures in the aggregate.
- The task body emits only the final domain-specific projection. `-AsJson` produces a single compact document.

In Bash, the shared result is an equivalent JSON object. Use a JSON builder (`jq` or an explicit serialization tool), check the process and reducer exit codes separately, and drain stderr as well. Declare the dependencies you use.

## 4. Control what you read

Default budget: **4096 UTF-8 bytes of compact JSON**, reduced to 2048 bytes if the answer fits within that size. The budget applies to the entire task. Bytes define a transport limit, not an exact token count.

Inspect `Outcome`, `Execution`, `Error`, and `Truncation` before using `.Data`. A preview may be omitted even when an exact count is available. An incomplete search does not justify claiming that no results exist.

If the answer is insufficient, define a more targeted query or correct the contract. Do not automatically rerun an operation or retrieve its raw transcript. If a new model decision is required, end the phase with a compact state that supports choosing the next step.

Keep reusable parsers and calculations in tested functions or modules. The ad hoc script describes the current task. In particular, verify success, native failures, malformed output, omissions, and timeouts. Do not claim measured token savings without comparable measurements.

## Examples of expected reasoning

Question: "Is the repository clean?"

Build a script that runs `git status` in a machine-readable format, counts modified/untracked entries locally, and returns, for example, `.Data = @{ Changed=2; Untracked=1; Clean=$false }`. All file names are needed only if the question asks for them.

Question: "Prepare and then validate 2,000 items."

Build a script with two steps. Each step runs the intended CLI, reduces its events, and checks its completion marker. Validation depends on successful preparation. Return a single object with the prepared/validated counts and an aggregate status that preserves any failures.
