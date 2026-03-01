Generate a CHANGELOG.md entry from recent git changes.

## Steps

1. **Find the last tag**: Run `git describe --tags --abbrev=0` to find the most recent version tag.
   - If no tags exist, use the initial commit as the baseline.

2. **Get the diff**: Run `git log --oneline <last_tag>..HEAD` to list all commits since the last tag.

3. **Categorize changes** into these sections:
   - **Added**: New handlers, new UI panels, new features
   - **Changed**: Modified behavior, updated defaults, refactored internals
   - **Fixed**: Bug fixes, crash fixes, test fixes
   - **Security**: Changes related to SEC-* requirements
   - **Removed**: Deprecated features, removed code

4. **Map to requirements**: Where possible, reference the requirement ID (e.g., "Added TELEMETRY_APP handler (TEL-01, TEL-02)")

5. **Draft the entry** in Keep a Changelog format:
   ```
   ## [version] - YYYY-MM-DD
   ### Added
   - ...
   ### Changed
   - ...
   ```

6. **Prepend** the new entry to CHANGELOG.md (after the header, before previous entries).

7. **Review**: Show the draft entry for confirmation before writing.
