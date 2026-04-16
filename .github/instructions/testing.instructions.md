# Testing Standards — Scaffold

These standards apply to all code changes in the Scaffold repository. Every agent and contributor must follow them.

## Philosophy

Tests must verify that features **work correctly**, not merely that they **do something**. A test that asserts a function returns a non-null value is not useful — a test that asserts a function returns the correct value for a given input is. Tests are documentation of expected behavior and the primary defense against regressions.

## Coverage Requirements

| Metric | Minimum | Target | Notes |
|---|---|---|---|
| **Line coverage (overall)** | 80% | 90%+ | CI should warn below 80%, block below 75% |
| **Line coverage (new code)** | 90% | 95%+ | New code has no excuse for low coverage |
| **Branch coverage** | 70% | 80%+ | Ensures conditional logic is exercised |
| **Critical paths** | 100% | 100% | Auth, data migration, payment, validation |

Coverage is measured with `dotnet test --collect:"XPlat Code Coverage"` (backend) and should be measured with equivalent tooling for frontend when added.

**Coverage is necessary but not sufficient.** 100% line coverage with weak assertions is worse than 80% coverage with strong, behavioral assertions.

## Test Types Required

### Unit Tests (Always Required)

Every new function, method, or component must have unit tests that verify:

- **Correct output for valid inputs** — not just "returns something" but "returns the right thing"
- **Edge cases** — null/empty inputs, boundary values (0, -1, MAX_INT), single-element collections, empty collections
- **Error handling** — invalid inputs produce the correct error type, message, and status code
- **State transitions** — for stateful code, verify the object is in the correct state after each operation

**Backend (.NET / xUnit):**
- One test class per production class
- Test method naming: `MethodName_Scenario_ExpectedResult` (e.g., `Assess_WithEmptySchema_ReturnsEmptyReport`)
- Use `[Theory]` with `[InlineData]` for parameterized tests covering multiple inputs
- Use Moq for dependency isolation — never hit real databases, APIs, or file systems in unit tests
- Assert on specific values, not just non-null: `Assert.Equal(expected, actual)` not `Assert.NotNull(result)`

**Frontend (React / TypeScript):**
- Component tests must verify rendered output matches expected state, not just that the component renders without crashing
- Test user interactions: click handlers produce the correct state changes, form submissions send the correct data
- Test conditional rendering: verify elements appear/disappear based on props and state
- Test error states: what does the user see when an API call fails?
- Test loading states: verify spinners/skeletons appear during async operations
- Use React Testing Library's `getByRole`, `getByText` — test what the user sees, not implementation details
- Never test implementation details like component state values or internal method calls

### Integration Tests (Required for Component Interactions)

- API controller tests using `WebApplicationFactory` with in-memory database
- Test the full request → controller → service → repository → response pipeline
- Verify HTTP status codes, response shapes, and error envelopes
- Test auth requirements: unauthenticated requests return 401, unauthorized return 403
- Test database interactions: verify data is actually persisted and queryable
- Test SignalR hub connections and message delivery

### End-to-End Tests (Required for Critical User Paths)

- Cover the core user journey: create project → assess → plan → migrate
- Verify that the frontend and backend work together correctly
- Test real browser interactions (when Playwright tests are added)
- Integration tests against real SQL Server run in CI on main branch

### Regression Tests (Required for Every Bug Fix)

- **Every bug fix must include a test that fails without the fix and passes with it**
- The test must be specific to the bug — not a broad test that happens to cover it
- Name the test to reference the bug: `MigrationPlan_ScheduledAtInPast_DoesNotExecute_Regression42`

## What Makes a Good Test

### ✅ Good: Tests Behavior Correctly

```csharp
[Fact]
public async Task Assess_WithForeignKeys_ReportsCorrectDependencyCount()
{
    var schema = CreateSchemaWithForeignKeys(count: 5);
    var result = await assessor.AssessAsync(connectionInfo, schema);
    Assert.Equal(5, result.Schema.ForeignKeys.Count);
    Assert.All(result.Schema.ForeignKeys, fk => Assert.NotEmpty(fk.ReferencedTable));
}
```

### ❌ Bad: Tests That Something Happens

```csharp
[Fact]
public async Task Assess_DoesNotThrow()
{
    var result = await assessor.AssessAsync(connectionInfo, schema);
    Assert.NotNull(result);  // Tells us nothing about correctness
}
```

### ✅ Good: Frontend Tests User-Visible Behavior

```typescript
it('displays error message when assessment fails', async () => {
  server.use(rest.get('/api/assessments/:id', (req, res, ctx) => res(ctx.status(500))));
  render(<AssessmentReport projectId="1" />);
  expect(await screen.findByText(/failed to load assessment/i)).toBeInTheDocument();
});
```

### ❌ Bad: Frontend Tests Implementation Details

```typescript
it('sets loading state', () => {
  const { result } = renderHook(() => useAssessment('1'));
  expect(result.current.isLoading).toBe(true);  // Tests internal state, not user experience
});
```

## Pre-Commit Testing Requirements

**All tests must pass before any commit is made.** This is non-negotiable.

1. Run `dotnet test` — all backend tests must pass
2. Run `npx tsc --noEmit` in `src/Scaffold.Web` — no TypeScript errors
3. Run frontend tests when they exist
4. If any test fails, fix the issue before committing — do not skip or disable tests

## Test Quality Checklist

Before considering testing complete for any change, verify:

- [ ] Every acceptance criterion has at least one test
- [ ] Happy path is tested with realistic data (not empty objects or default values)
- [ ] Error paths are tested: what happens when inputs are invalid, services are unavailable, or data is malformed?
- [ ] Edge cases are tested: empty collections, null values, boundary values, concurrent access
- [ ] Assertions verify **correct values**, not just **non-null/non-empty**
- [ ] Tests are independent: each test sets up its own state and cleans up after itself
- [ ] Test names describe the scenario and expected outcome
- [ ] No flaky tests: tests pass consistently, not intermittently
- [ ] Frontend tests verify what the user sees, not internal component state
- [ ] Integration tests verify the full stack, not just individual layers

## Scaffold-Specific Testing Patterns

- Use `SynchronousProgress<T>` instead of `Progress<T>` in migration tests (avoids async race conditions)
- Use `CustomWebApplicationFactory` with in-memory DB for API integration tests
- Use `TestAuthHandler` for authenticated test requests
- Use `StubMigrationEngine` to replace real migration engine in API tests
- Guard `db.Database.Migrate()` with `IsRelational()` for in-memory test safety
- EF Core JSON column types require special handling in test assertions — compare serialized forms or use deep equality
