# DESIGN-PHASED-PROJECT — Claude Context

Decompose code-generation projects into 3 manually-run phases: Design (scrape external
data into JSON) -> Attributes (declare triggers) -> Build (SG emits code). Design output
is checked into source control for hermetic builds.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [BINARY-WRAPPER](../BINARY-WRAPPER/) — primary consumer
- [COMPOSE-BUNDLE](../COMPOSE-BUNDLE/) — schema consumer

## Related packages
- [`FrenchExDev.Net.BinaryWrapper.Design.Lib`](../../../../Net/FrenchExDev/BinaryWrapper/)

## Notes for Claude
- Never run Design phase on `dotnet build` — requires containers/network; must be manual CLI only
- SkippedCommands must be explicit — `help`, `completion`, and any hanging/recursive commands
- Two-phase scraping: Phase 1 builds container image (cached); Phase 2 scrapes in that image (fast, parallelizable)
- Cleanup in finally block is mandatory — never leave orphan containers/images
- Broken versions must be documented, not silently skipped
- `--reparse` mode iterates parser logic without re-running containers
