Run and review the test suite for a specific handler: $ARGUMENTS

## Steps

1. **Find the handler**: Locate `src/Handlers/$ARGUMENTSHandler.cs` and `tests/Handlers/$ARGUMENTSHandlerTests.cs`

2. **Run tests**: Execute `dotnet test tests/ --filter "FullyQualifiedName~$ARGUMENTS"`
   - Report pass/fail for each test method

3. **Coverage analysis**: Review the handler source and check that tests exist for:
   - [ ] Happy path with all fields populated
   - [ ] Missing optional fields (graceful degradation)
   - [ ] Malformed/garbage protobuf payload (logged and discarded)
   - [ ] XML special characters in string fields (< > & ' ")
   - [ ] Null/empty payload
   - [ ] Correct CoT type string per CLAUDE.md mapping
   - [ ] Correct stale time

4. **Suggest missing tests**: For any unchecked box above, write the missing test method and add it to the test file.

5. **Re-run**: Execute `dotnet test tests/ --filter "FullyQualifiedName~$ARGUMENTS"` again to confirm all new tests pass.

6. **Report**: Summary of coverage before and after, any issues found.
