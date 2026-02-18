---
name: handle-issue
description: Analyze a GitHub issue and develop an implementation plan
disable-model-invocation: true
---

The user wants to work on a GitHub issue. The issue number (e.g. `#47` or `47`) is provided as the argument: `$ARGUMENTS`.

If no issue number argument was provided, ask the user which issue they want to work on.

Follow these steps:

1. **Fetch the GitHub issue** using `gh issue view <number>` to get the issue title, body, and any comments.

2. **Summarize the issue** clearly and concisely for the user. Explain what it says we should do.

3. **Evaluate whether the issue contains a fully formed plan:**
   - **If it IS a fully formed plan** (clear steps, well-defined scope, actionable):
     - Present the summary and ask the user if they want to proceed with the plan as described in the issue.
     - If they want to proceed, enter plan mode and write a detailed implementation plan based on the issue's plan, exploring the relevant parts of the codebase to fill in specifics (file paths, method names, etc.).
     - If they do NOT want to proceed with that plan, continue to step 4.

   - **If it is NOT a fully formed plan** (vague, high-level, or missing details):
     - Continue to step 4.

4. **Explore the codebase** to understand the relevant parts and get a better understanding of how to address the issue. Read related files, understand existing patterns, and think about what the best approach would be.

5. **Ask clarifying questions.** Do not hesitate to ask the user questions that might be relevant to make sure you understand the goals correctly. Ask too many clarifying questions rather than too few.

6. **Present your thoughts.** When you have a complete understanding, share your final thoughts on how we should proceed with addressing the issue.

7. **Enter plan mode.** When the user agrees with the approach, enter plan mode and write a detailed implementation plan. Continue to examine the codebase in detail within plan mode to make the plan as specific and actionable as possible.
