---
name: review-changes
description: Review unstaged git changes for bugs, logic errors, and security problems
disable-model-invocation: true
---

Look at the currently unstaged changes in git and review them strictly for:

1. Bugs and logic errors
2. Security vulnerabilities
3. Race conditions or concurrency issues
4. Incorrect error handling
5. Edge cases that could cause failures
6. Other similar problems or room for improvements

Obtain the diff to review using this fallback chain:

1. Run `git diff`. If this produces output, use it as the diff.
2. If empty, run `git diff --cached`. If this produces output, use it as the diff.
3. If both are empty, run `git diff master...HEAD` to see all committed changes on the current branch relative to master.

If all three are empty, report that there is nothing to review and stop.

Be specific — reference file names and line numbers. Only flag things that actually matter.
