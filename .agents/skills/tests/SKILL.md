---
name: tests
description: Required whenever adding, planning, extending, or changing tests. Prohibits unit tests for database models and EF mapping metadata. Requires a concise, scenario-by-scenario value assessment before writing tests; add only coverage that protects meaningful application behavior, not a large suite for its own sake. Use alongside test-alignment when fixing stale tests.
---

# Write only tests that earn their maintenance cost

## Hard rules

- **Do not write unit tests for database models/entities.** Do not test their getters, setters, property types, defaults, nullability, or object initialization.
- **Do not write EF model-metadata tests.** Assertions about table/column names, indexes, foreign keys, navigation properties, enum-to-string mappings, and configured database types merely restate configuration. They are prohibited even when no database connection is opened.
- Application services may use entities in their tests. Test the service's behavior, not the shape of the entity it uses.
- **Do not add a huge test suite by default.** A new class, property, enum member, branch, or migration does not automatically need a test. Do not chase test counts or coverage percentages.
- Do not test framework behavior, compiler guarantees, trivial assignments, or implementation details that have no meaningful application outcome.
- Do not add extra behavior to production code just to create something to test.

## Required workflow: assess scenarios before writing tests

1. Read the behavior under test and its existing coverage. Establish the intended contract; do not invent requirements or edge-case policies.
2. List the plausible use cases and edge cases for the requested change. Keep the list relevant; do not manufacture hypothetical scenarios to inflate it.
3. **Assess every candidate individually in a concise table in your response before editing test code:**

   | Scenario | Benefit / bug it catches | Cost / reason to skip | Include or skip |
   |---|---|---|---|
   | Normal successful operation | Protects the main observable result | Small fixture | Include |
   | Missing required input | Prevents an incorrect result instead of the agreed failure outcome | Low cost; distinct behavior | Include |
   | Entity property accepts a value | None; tests an assignment | No regression value; prohibited model test | Skip |
   | Every enum value using the same converter | No distinct application behavior | Repetition and maintenance churn; prohibited metadata test | Skip |

   Replace these examples with the actual scenarios. Each row needs a specific, brief benefit and cost assessment, plus an explicit decision. A blanket statement such as "add edge-case tests" is not sufficient.
4. Select the smallest useful set. Each included test must protect a concrete behavior, known regression, or material failure risk that is not already covered. If none qualify, say so and add no tests.
5. Write only the selected tests. Assert observable outcomes, use straightforward setup, and avoid coupling to private helpers or internal structure.
6. Run the relevant tests and report the result. If a new scenario emerges, assess its value before adding another test.

## Keep coverage focused

- Prefer one representative example for a class of equivalent inputs. Use parameterized cases only when each case adds meaningful behavioral coverage; do not enumerate every enum member or duplicate the same assertion across many values.
- Add boundary cases when they exercise a materially different outcome, not simply because more input combinations exist.
- Prefer a public-behavior test that protects the requirement over multiple tests for implementation helpers.
- Do not make private methods public solely for testing or use reflection to test them.
- When behavior is deliberately removed, delete obsolete tests instead of preserving unsupported features through mocks or new abstractions.
- Match the scope of the task. Do not expand a small change into an unrelated testing campaign.

## Database integration tests are a separate decision

Do not replace prohibited model tests with automatic database integration tests. A database integration test is justified only for a concrete persistence behavior or regression that matters to the application, not to mirror schema configuration. Apply the same scenario assessment and load `../db-working-patterns/SKILL.md` before any database work.

## Aligning stale tests

Also load `../test-alignment/SKILL.md` when fixing tests after a code change. Its test-only editing restriction still applies. Reassess the value of stale tests rather than blindly preserving them; remove model-only and redundant tests instead of recreating them.
