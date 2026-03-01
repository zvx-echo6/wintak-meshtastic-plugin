Regenerate C# protobuf classes from Meshtastic .proto definitions and verify compatibility.

## Steps

1. **Backup**: Copy current `proto/generated/` to `proto/generated.bak/`

2. **Regenerate**: Run `protoc --csharp_out=proto/generated proto/meshtastic/*.proto`
   - Report any protoc warnings or errors

3. **Diff**: Compare `proto/generated/` with `proto/generated.bak/`
   - List new files (new message types)
   - List modified files with a summary of what changed (new fields, removed fields, enum changes)
   - List deleted files (removed message types)

4. **Build check**: Run `dotnet build src/`
   - If build fails due to proto changes, list the breaking changes and suggest fixes
   - Fix any compilation errors caused by renamed/removed fields

5. **Test check**: Run `dotnet test tests/`
   - If tests fail due to proto changes, update tests to match new schema

6. **Impact assessment**:
   - Are there new portnums we should add handlers for? Check against the portnum reference in docs/requirements.docx
   - Are there new fields in existing message types that our handlers should surface?
   - Any deprecation warnings?

7. **Cleanup**: Remove `proto/generated.bak/`

8. **Report**: Summary of changes, build status, test status, and recommendations for new handler work.
