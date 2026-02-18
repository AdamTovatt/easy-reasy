---
name: worktree
description: Create or update a git worktree for a branch
disable-model-invocation: true
---

The user wants to work on a branch in a separate worktree. The argument is provided as: `$ARGUMENTS`.

If no argument was provided, ask the user which branch or issue they want.

Follow these steps:

1. **Fetch latest from remote** by running `git fetch origin`.

2. **Determine the branch name:**
   - If the argument is an existing branch name (check with `git branch -a`), use it as-is.
   - If the argument is an issue number (e.g. `#82` or `82`), generate a descriptive branch name in the format `<number>-short-descriptive-kebab-case-name`. Use context from the issue (fetch it with `gh issue view <number>`) to pick a short but descriptive name. For example, issue #82 titled "Unify not-found and unauthorized responses" could become `82-unify-not-found-responses`.

3. **Determine the worktree path**: `../careless-<branch-name>` (relative to the repo root). For example, branch `azure-session-lifetime-management` becomes `../careless-azure-session-lifetime-management`.

4. **Check if the worktree already exists** by running `git worktree list` and checking if the path is already listed.

5. **Ask the user for confirmation** before creating or updating the worktree. Show the branch name and worktree path and ask if they want to proceed.

6. **If the worktree does NOT exist:**
   - Run `git worktree add ../careless-<branch-name> <branch-name>` to create it.
   - If that fails because the branch doesn't exist locally, try `git worktree add -b <branch-name> ../careless-<branch-name> origin/<branch-name>` to create a local branch tracking the remote.
   - If that also fails (branch doesn't exist on remote either), create a new branch from master: `git worktree add -b <branch-name> ../careless-<branch-name> origin/master`.
   - Tell the user what happened (checked out existing branch, or created new branch from master).

7. **If the worktree ALREADY exists:**
   - Run `git -C ../careless-<branch-name> pull` to get the latest changes.
   - Tell the user the worktree was already set up and latest changes have been pulled.

8. **Tell the user how to start working there.** Print a message like:

   ```
   Worktree ready! To start Claude in the worktree, run:
   cd ../careless-<branch-name> && claude
   ```
