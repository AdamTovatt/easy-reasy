---
name: create-issue
description: Use when the user wants to create a GitHub issue from a rough problem description or feature idea
---

The user wants to create a GitHub issue. Their input is: `$ARGUMENTS`

If no description was provided, ask the user what the issue is about.

Follow these steps:

1. **Investigate the codebase.** Based on the user's description, search for relevant files, code patterns, and existing behavior related to the problem or feature. Understand enough context to write a well-informed issue.

2. **Check for duplicates.** Run `gh issue list --state open --limit 30` and scan for existing issues that cover the same topic. If a likely duplicate exists, tell the user and ask how they want to proceed.

3. **Draft the issue.** Based on your investigation, prepare:
   - **Title**: Short, clear, imperative (e.g. "Fix file upload for filenames with spaces" or "Add dark mode toggle to settings page")
   - **Body**: A structured description with these sections:
     ```
     ## Description
     What the problem is or what the feature should do.

     ## Context
     Relevant files, components, or architecture details you found in the codebase.

     ## Suggested approach
     A brief outline of how this could be implemented or fixed, if applicable.
     ```
   - **Label**: Pick the single most appropriate label from this list:
     - `bug` — Something isn't working
     - `enhancement` — New feature or request
     - `documentation` — Improvements or additions to documentation
     - `bite size` — Nice, small, not too complex
     - `a bit bigger` — Not very complex but has more system design considerations
     - `architecturally complex` — Requires careful consideration for correct architecture
     - `requires investigation` — Requires non-code investigation before deciding on action
     - `frontend` — Related to frontend
     - `claude config` — Related to configuring Claude / AI assisted development workflow

4. **Present the draft to the user.** Show the full issue (title, body, label) and ask if they want to:
   - Create it as-is
   - Modify something first
   - Cancel

5. **On approval, create the issue.** Run:
   ```
   gh issue create --title "<title>" --body "<body>" --label "<label>"
   ```
   Show the resulting issue URL to the user.
