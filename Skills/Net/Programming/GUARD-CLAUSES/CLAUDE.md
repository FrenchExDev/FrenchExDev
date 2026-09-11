# GUARD-CLAUSES — Claude Context

Minimal inline guard methods at API boundaries. Three APIs: `Guard.Against.*` (throws), `Guard.ToResult.*` (returns Result), `Guard.Ensure.*` (invariants). Auto-captures parameter names via `[CallerArgumentExpression]`. Always returns the validated value for inline assignment.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [RESULT-PATTERN](../RESULT-PATTERN/) — ToResult bridge

## Related packages
- None — zero NuGet dependencies

## Notes for Claude
- `Against.*` throws `ArgumentNullException` / `ArgumentException` / `ArgumentOutOfRangeException`
- `Ensure.*` throws `InvalidOperationException` (postcondition, not argument error)
- Guards never log and never mutate — strictly validation
- `ToResult` uses `ValidationResult` error type (bridges to domain validation)
- Do NOT catch exceptions from `Against.*` — they indicate programming errors
- `netstandard2.0` polyfill for `[CallerArgumentExpression]` on legacy .NET
- Guards check structure only (null, empty, range) — not domain rules
