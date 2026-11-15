# Instructions for AI assistants

## Workflow

- **Pull the latest changes before doing anything.** Run `git fetch origin` first and
  work from the current `origin/master`: start new work with
  `git switch -c <branch> origin/master`, or `git pull` when continuing on an existing
  branch. Never build on a stale checkout.
- **Always finish with a pull request.** Make the change on a branch (never commit to
  `master` directly), push the branch, and open a pull request against `master` with
  `gh pr create`. Say in the description what changed and how it was tested.

## README

- **`README.md` is for end users only.** It says what the app does, how to get and
  install it, and how to use it. It is also the package readme on nuget.org.
- **Never put anything technical or architectural in it.** That includes building and
  testing, the release process, CI and workflows, secrets, project structure, libraries
  and dependencies, where texts or translations are stored, design decisions, and how a
  feature works inside. All of that goes in `CONTRIBUTING.md`.
- **Describe behavior as the user sees it.** For example, write "a cancelled run leaves
  the ZIP file untouched", not how the code achieves that.
