# Issue tracker: GitHub

Issues and product requirements for this project live in [Hoangnam3899/Simulate-automotive](https://github.com/Hoangnam3899/Simulate-automotive/issues).

Use the GitHub CLI with `--repo Hoangnam3899/Simulate-automotive` until this local directory has that repository configured as its Git remote.

## Conventions

- **Create an issue:** `gh issue create --repo Hoangnam3899/Simulate-automotive --title "..." --body "..."`
- **Read an issue:** `gh issue view <number> --repo Hoangnam3899/Simulate-automotive --comments`
- **List issues:** `gh issue list --repo Hoangnam3899/Simulate-automotive --state open`
- **Comment on an issue:** `gh issue comment <number> --repo Hoangnam3899/Simulate-automotive --body "..."`
- **Apply or remove labels:** `gh issue edit <number> --repo Hoangnam3899/Simulate-automotive --add-label "..."` or `--remove-label "..."`
- **Close an issue:** `gh issue close <number> --repo Hoangnam3899/Simulate-automotive --comment "..."`

## Pull requests as a triage surface

**PRs as a request surface: no.** External pull requests are not treated as feature requests by default.

## When a skill says "publish to the issue tracker"

Create a GitHub issue in `Hoangnam3899/Simulate-automotive`.

## When a skill says "fetch the relevant ticket"

Run `gh issue view <number> --repo Hoangnam3899/Simulate-automotive --comments`.
