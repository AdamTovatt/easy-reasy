---
name: review-branch
description: Review all changes on the current branch compared to master
disable-model-invocation: true
---

Review all changes on the current branch compared to master, like a pull request review.

Use `git diff master...HEAD` to see the full diff and `git log master..HEAD --oneline` to understand the commit history.

Review for both categories:

**Correctness & Safety:**
- Bugs and logic errors
- Security vulnerabilities
- Race conditions or concurrency issues
- Incorrect error handling
- Edge cases that could cause failures
- Other problems or room for improvements

**Style & Architecture:**
- Code style issues (per the project's conventions in CLAUDE.md and README.md)
- Architectural cleanliness and separation of concerns
- Naming conventions and readability
- Maintainability concerns

Be specific — reference file names and line numbers. Only flag things that actually matter.
