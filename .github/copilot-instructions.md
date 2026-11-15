# Instructions for AI assistants

## Workflow

- **Pull the latest changes before doing anything.** Run `git fetch origin` first and
  work from the current `origin/master`: start new work with
  `git switch -c <branch> origin/master`, or `git pull` when continuing on an existing
  branch. Never build on a stale checkout.
- **Always finish with a pull request.** Make the change on a branch (never commit to
  `master` directly), push the branch, and open a pull request against `master` with
  `gh pr create`. Say in the description what changed and how it was tested.
