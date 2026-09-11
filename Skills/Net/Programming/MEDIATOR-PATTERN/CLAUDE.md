# MEDIATOR-PATTERN — Claude Context

Request/event dispatch contracts only — 7 interfaces, 1 enum, zero runtime classes. CQRS markers (`ICommand`/`IQuery`), `IBehavior<TRequest, TResult>` pipeline with explicit `next`, per-call `PublishStrategy`, and `FakeMediator` test double.

## Skill docs
- [Philosophy](PHILOSOPHY.md) — design rationale and trade-offs
- [Architecture](ARCHITECTURE.md) — internal structure
- [How-To](HOW-TO.md) — step-by-step tasks
- [Requirements](REQUIREMENTS.md) — formal requirements

## Related skills
- [RESULT-PATTERN](../RESULT-PATTERN/) — handler return types

## Related packages
- Mediator contracts (no anchor package — contracts only)

## Notes for Claude
- No runtime provided — consumers choose: reflection-based, source-generated, or manual
- Do NOT throw from handlers for expected failures — use `Result<T>` return types
- Cross-cutting concerns (logging, validation, caching) go in behaviors, not handlers
- `FakeMediator.SendAsync` without a matching `Setup` throws (fail-fast, not verification)
- `PublishStrategy.FireAndForget` exceptions are swallowed — use for non-critical side effects
- Behaviors are generic — one `LoggingBehavior<TRequest, TResult>` covers all request types
- `INotification` = fan-out (0+ handlers); `IRequestHandler` = one-to-one
- Do NOT mock mediator with frameworks — use FakeMediator
