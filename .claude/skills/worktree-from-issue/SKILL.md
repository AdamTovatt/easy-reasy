---
name: worktree-from-issue
description: Create a new worktree and branch from a GitHub issue
disable-model-invocation: true
---

The user wants to create a new worktree with a new branch based on a GitHub issue. The issue number (e.g. `#72` or `72`) is provided as the argument: `$ARGUMENTS`.

If no issue number argument was provided, ask the user which issue they want.

Follow these steps:

1. **Fetch the GitHub issue** using `gh issue view <number>` to get the issue title and body.

2. **Look at existing branch names** by running `git branch -r --sort=-committerdate` to understand the repo's naming conventions.

3. **Think of a good branch name** that:
   - Starts with the issue number (e.g. `72-descriptive-name`)
   - Uses kebab-case
   - Is concise but clearly describes what the issue is about
   - Matches the style of existing branch names in the repo
   - Has no "/" prefix

4. **Verify that the branch and worktree path (`../<repo-name>-<branch-name>`) aren't already in use** by checking `git branch -r` and `git worktree list`. If either already exists, pick a different name and repeat untill you find one that is free.

5. **Present the branch name to the user** and ask for confirmation before proceeding. For example:

   ```
   Issue #72: "Add export functionality for datasets"
   Proposed branch name: 72-dataset-export
   ```

   Wait for the user to approve or suggest a different name.

6. **Once approved, fetch latest from remote** by running `git fetch origin`.

7. **Create the worktree** from master:

   ```
   git worktree add ../<repo-name>-<branch-name> -b <branch-name> origin/master
   ```

8. **Tell the user how to start working there.** Print a message like:
   ```
   Worktree ready! To start Claude in the worktree, run:
   cd ../<repo-name>-<branch-name> && claude
   ```
