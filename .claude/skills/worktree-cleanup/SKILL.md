---
name: worktree-cleanup
description: Clean up worktrees for branches that have been merged and deleted
disable-model-invocation: true
---

The user wants to clean up worktrees that are no longer needed because their branches have been merged and deleted on GitHub.

Follow these steps:

1. **Fetch latest from remote** by running `git fetch origin --prune` to update remote tracking refs and prune deleted branches.

2. **List all worktrees** by running `git worktree list` to get all current worktrees and their branches.

3. **Identify candidates for cleanup.** For each worktree (excluding the main worktree at the repo root):
   - Get the branch name from the worktree list output.
   - Check if the branch still exists on the remote by running `git branch -r --list "origin/<branch-name>"`.
   - If the remote branch is gone, it's a cleanup candidate.

4. **If no candidates are found**, tell the user all worktrees are still active and there's nothing to clean up.

5. **Present the candidates to the user.** Show a summary like:

   ```
   Found worktrees for branches that no longer exist on remote:

   - ../<repo-name>-72-dataset-export (branch: 72-dataset-export)
   - ../<repo-name>-85-fix-login-bug (branch: 85-fix-login-bug)
   ```

   Ask the user which ones they want to remove, or if they want to remove all of them.

6. **For each worktree the user confirms for removal:**
   - Run `git worktree remove <worktree-path>` to remove the worktree.
   - If that fails because of uncommitted changes, inform the user and ask if they want to force it with `git worktree remove --force <worktree-path>`.
   - Delete the local branch with `git branch -d <branch-name>`. If it fails (not fully merged), inform the user and ask if they want to force it with `git branch -D <branch-name>`.

7. **Run `git worktree prune`** to clean up any stale worktree references.

8. **Show a summary** of what was cleaned up:
   ```
   Cleaned up 2 worktree(s):
   - ../<repo-name>-72-dataset-export
   - ../<repo-name>-85-fix-login-bug
   ```
