# WRAPPER-VERSIONING — Claude Context

Reusable design-time pipeline for downloading items per version from remote APIs. Generic `DesignPipeline<TItem>` with middleware composition. 4 built-in collectors: GitHub releases, GitHub tags, GitLab releases, static. `SemaphoreSlim` parallelism with semver-aware comparison.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [SG](../SG/) — Roslyn extraction pattern
- [SOLID](../SOLID/) — 1-method IItemCollector

## Related packages
- [`FrenchExDev.Net.Wrapper.Versioning`](../../../../Net/FrenchExDev/Wrapper.Versioning/)

## Notes for Claude
- Without token, GitHub 403s after ~60 requests (rate limit)
- `--missing` checks file existence only, not content integrity
- `OutputFilePattern` MUST contain `{key}` placeholder
- `HttpClient` shared across workers — don't mutate headers from middleware
- `tagToVersion` returning null filters the tag out (useful for pre-release exclusion)
- `DotEnvLoader` recursively traverses `.env` files for tokens
- `ItemFilter` runs after collection — doesn't save API calls
