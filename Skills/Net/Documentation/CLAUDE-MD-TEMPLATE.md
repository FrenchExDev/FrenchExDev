# CLAUDE-MD-TEMPLATE — How to write a package `CLAUDE.md`

Every package under [Net/FrenchExDev/](../../../Net/FrenchExDev/) has a `CLAUDE.md`
file at its root. This file is the **fast index** that future Claude conversations read
to understand the package without re-deriving conventions from scratch.

## Rules

1. **Thin, not rich.** ~20–40 lines. Pointers, not duplicated content.
2. **No YAML frontmatter.** Plain Markdown.
3. **Link, don't copy.** Cross-link to the package's own `README.md` / `doc/`, and to
   centralized skills under [../../Programming/](../Programming/).
4. **"Notes for Claude" is the only original content.** 3–8 bullets of non-obvious
   facts: gotchas, version constraints, things to never do, things to always do. Do not
   list things that can be derived by reading the code.

## Template

```markdown
# <PackageName> — Claude Context

<One-sentence purpose, copied or distilled from the package README>

## Package docs
- [README](README.md)
- [Architecture](doc/ARCHITECTURE.md)
- [How-To](doc/HOW-TO.md)
- [Philosophy](doc/PHILOSOPHY.md)

## Relevant skills
- [<Skill name>](../../../Skills/Net/Programming/<SKILL>/PHILOSOPHY.md)
- [SOLID](../../../Skills/Net/Programming/SOLID/PHILOSOPHY.md)
- [Solution Layout](../../../Skills/Net/Programming/SOLUTION-LAYOUT/ARCHITECTURE.md)
- [Central Package Management](../../../Skills/Net/Programming/CENTRAL-PACKAGE-MANAGEMENT/ARCHITECTURE.md)

## Solution
- `FrenchExDev.Net.<PackageName>.slnx`

## Notes for Claude
- <Non-obvious fact 1>
- <Non-obvious fact 2>
- <Non-obvious fact 3>
```

## What goes in "Notes for Claude"

**DO** include:
- Pre-existing technical debt that Claude should not "fix" (e.g. SG assembly loading
  issue in Injectable)
- Version-specific bugs in upstream binaries (e.g. Vagrant 2.4.4–2.4.5
  `server_mode?` crash)
- Conventions that are easy to violate (e.g. "never add `Version=` to csproj")
- Things to never run (e.g. "Design project downloads run by the user, not Claude")
- Naming/ordering rules generated code expects

**DO NOT** include:
- Whatever can be learned from reading the README, ARCHITECTURE.md, or `git log`
- Anything that lives in [INDEX.md](../Programming/INDEX.md) for a skill
- Personal preferences that belong in user memory, not project memory
- Conjecture about how things "should" work
