# Contributing

1. Fork and clone the repo.
2. `dotnet build` and `dotnet test` must pass before you open a PR. Warnings are errors.
3. Public API needs XML docs; the build fails without them.
4. Add or update tests in `tests/dotnet-benchmark-bot.Tests` for any behavior change.
5. Use conventional commit prefixes: `feat:`, `fix:`, `test:`, `docs:`, `chore:`.
6. Keep PRs focused. One feature or fix per PR.

Open an issue first for larger changes (new detector strategies, new output formats, new CI providers) so the design can be agreed before code is written.
