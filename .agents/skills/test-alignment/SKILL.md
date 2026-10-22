---
name: test-alignment
description: Use when the user asks to fix, update, or align tests that no longer compile or pass because the code has changed. Edits test files only; never modifies source code under test.
---

## Summary
Use when asked to "fix tests", "make tests pass", or "fix failing tests". The intent is always that the **tests are stale** (out of sync with intended behavior), not that production code is wrong.

## Rule
**Do not edit source code under test.** Only edit test files.

This includes (but is not limited to):
- `*.Tests*/` projects
- `*Test*.cs`, `*Tests.cs`, `*Specs.cs` files
- Test fixtures, mocks, fakes, builders, and assertion helpers

If a test fails because production code "looks wrong", assume the production code is correct and the test is stale — update the test to match the current intended behavior. If you believe production code is genuinely buggy, **stop and ask** before editing it.

## Workflow
1. Run the failing test(s) and read the failure carefully.
2. Identify the gap between the test's expectation and the current behavior.
3. Update the test (assertions, setup, expected values) to reflect the intended behavior.
4. Re-run to confirm green.
5. If the test cannot be made green without changing production code, **report back** instead of editing source.