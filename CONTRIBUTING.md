# Contributing

Contributions are welcome through issues, discussions, and pull requests. If you plan to work on a larger change, open an issue or discussion first so the implementation approach can be aligned before code is written.

There is currently no `CODE_OF_CONDUCT.md` in this repository. One will be added.

All contributors must sign the GroundControl Individual Contributor License Agreement before a pull request can be merged. Signing is handled automatically through [CLA Assistant](https://cla-assistant.io/). On your first pull request, a bot will comment with a link to sign. This is a one-time action and uses your GitHub account for authentication.

The CLA grants Mohammadreza Taikandi a perpetual, worldwide, royalty-free license to use, modify, sublicense, and relicense your contribution under any license, including commercial licenses. You retain copyright in your contribution.

GroundControl requires a CLA because the project includes a commercial licensing component. The CLA ensures contributions can be sublicensed cleanly to paying customers. Contributions made to BSL-licensed projects convert to Apache License 2.0 four years after the release date of the version they ship in, along with the rest of that version's BSL-licensed code.

Your contribution follows the license of the project you change. Contributions to Apache-licensed projects are licensed under Apache License 2.0. Contributions to BSL-licensed projects are licensed under BSL 1.1 and convert to Apache License 2.0 on that version's Change Date.

Use a short, descriptive branch name. Prefixes such as `feature/`, `fix/`, `docs/`, or `chore/` are preferred when they make the branch purpose clearer.

Commit messages should follow the repository's conventional commit guidance in `.github/git-commit-instructions.md` using the format `<type>[scope]: <description>`.

Before opening a pull request:

- Run `dotnet build`
- Run `dotnet test`
- Add or update tests when behavior changes
- Keep the pull request scoped to a single change where practical

Code style is governed by `.editorconfig` and the repository guidance in `.claude/rules/csharp-style.md`. Additional area-specific guidance exists in `.claude/rules/cli.md`, `.claude/rules/data-access.md`, `.claude/rules/feature-slices.md`, and `.claude/rules/testing.md`.