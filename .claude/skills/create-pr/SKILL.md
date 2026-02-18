---
name: create-pr
description: Create a pull request with a structured description covering why, what, challenges, and future work
---

The user wants to create a pull request for the current branch. Optional arguments (e.g. issue number, extra context): `$ARGUMENTS`.

Follow these steps:

1. **Assess what you already know.** If the conversation already contains sufficient context about the changes (e.g. you just implemented the feature), skip to step 3. Otherwise, run the following to understand what this branch does:
   - `git log master..HEAD --oneline` to see all commits
   - `git diff master...HEAD --stat` for an overview (read full diffs only if needed for specific files)
   - `git branch --show-current` to get the branch name

2. **Check for a related GitHub issue.** Look at the branch name for an issue number prefix (e.g. `72-feature-name` means issue #72). If found, fetch it with `gh issue view <number>`. Also check `$ARGUMENTS` for an explicit issue reference. If no issue is found, that's fine — proceed without one.

3. **Check remote state.** Verify the branch is pushed and up to date with the remote. If not, tell the user they need to push first and stop.

4. **Draft the PR.** Prepare:
   - **Title**: Short, clear, under 70 characters (e.g. "Add dataset export functionality")
   - **Body** with these sections:

     ```
     ## Why?
     Short explanation of why this PR exists. If it closes a GitHub issue, mention it here (e.g. "Closes #72").

     ## What?
     Summary of what the PR does — the key changes and their purpose.

     ## Challenges
     Summary of any challenges that had to be overcome and/or architectural design decisions that were made.

     ## Left for future work
     - List of any identified things that should be addressed in future work but were left out because they were out of scope or not critical.
     ```

   - **Omit "Challenges" and/or "Left for future work"** if they are not applicable. Don't force content into these sections if there's nothing meaningful to say.

5. **Present the draft to the user.** Show the full PR (title and body) and ask if they want to:
   - Create it as-is
   - Modify something first
   - Cancel

6. **On approval, create the PR.** Run:
   ```
   gh pr create --title "<title>" --body "$(cat <<'EOF'
   <body>
   EOF
   )"
   ```
   Show the resulting PR URL to the user.
