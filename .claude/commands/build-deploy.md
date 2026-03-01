Build, test, and package the plugin for deployment.

## Steps

1. **Clean build**: Run `dotnet build src/ -c Release`
   - If build fails, fix errors and retry
   - Treat warnings as errors: `dotnet build src/ -c Release -warnaserror`

2. **Run tests**: Run `dotnet test tests/`
   - All tests must pass before proceeding
   - Report total pass/fail/skip counts

3. **Package**: Copy build output to `deploy/` folder:
   - Copy the main plugin .dll
   - Copy all dependency .dlls (Google.Protobuf, etc.)
   - Do NOT copy test assemblies, .pdb files (unless debug build requested), or source files

4. **Validate deployment package**:
   - List all files in `deploy/` with file sizes
   - Verify NO Python files (.py), NSSM references, or external service configs are present
   - Verify the main plugin .dll exists and is non-zero size
   - Check that Google.Protobuf.dll is included

5. **Report**:
   - Build status (success/fail)
   - Test results (pass/fail/skip counts)
   - Deploy folder contents with sizes
   - Total deployment size
   - Any warnings or issues found
