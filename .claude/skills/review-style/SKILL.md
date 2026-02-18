---
name: review-style
description: Review unstaged git changes for code style and architectural cleanliness
disable-model-invocation: true
---

Look at the currently unstaged changes in git from a purely code style and architectural perspective.

Obtain the diff to review using this fallback chain:
1. Run `git diff`. If this produces output, use it as the diff.
2. If empty, run `git diff --cached`. If this produces output, use it as the diff.
3. If both are empty, run `git diff master...HEAD` to see all committed changes on the current branch relative to master.

If all three are empty, report that there is nothing to review and stop.

Review the changes for:

1. Code style issues (per the project's conventions in CLAUDE.md and README.md)
2. Architectural cleanliness and proper separation of concerns
3. Naming conventions and readability
4. Code structure and organization
5. Maintainability concerns

Do NOT think about logical bugs, security problems, or correctness. Focus only on ensuring we have a clean, well-structured, maintainable code base. Be specific — reference file names and line numbers. Only flag things that actually matter.
