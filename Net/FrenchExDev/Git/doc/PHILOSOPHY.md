# Philosophy

## Why a BinaryWrapper for Git?

Git is the most widely used version control system. A typed C# wrapper enables:

- Compile-time safety for git command construction
- IDE autocompletion for all flags and options
- Version-aware API surface tracking across git releases
- Consistent builder pattern shared with Podman, Vagrant, and other wrappers

## All Commands, Not Just Porcelain

We scrape all commands (porcelain + plumbing) via `git help -a`. Low-level plumbing commands like `cat-file`, `rev-parse`, and `hash-object` are essential for tooling that interacts with git internals.

## Build From Source

Git does not distribute pre-built Linux binaries. Unlike Podman (static binaries on GitHub releases) or Vagrant (.deb packages on HashiCorp), git must be compiled from source for version-specific scraping. This is slower but ensures exact version fidelity.

## `-h` Not `--help`

Git's `--help` opens man pages (via a pager), which is unsuitable for scraping. The `-h` flag prints short help to stderr and exits with code 129. The Design pipeline handles this via a middleware that catches the non-zero exit code and extracts the stderr content.

## Custom Parser

Git's help format is unique among the CLIs we wrap:

- Root `git help -a` uses Title Case section headers
- Subcommands use `usage:`/`or:` patterns for groups
- Options follow GNU conventions but output to stderr

No existing parser (Cobra, Standard, Argparse) handles all three modes correctly, so `GitHelpParser` implements mode detection and delegates option parsing to `StandardHelpParser.ParseOptionLine()`.

## Minimum Version 2.30.0

Git 2.30.0 (January 2021) provides a stable `-h` format and includes most modern flags. Starting here gives broad coverage without dealing with older format inconsistencies.
