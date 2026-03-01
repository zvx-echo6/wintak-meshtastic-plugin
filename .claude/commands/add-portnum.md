Implement a new Meshtastic portnum handler for: $ARGUMENTS

Follow the Portnum Handler Pattern in CLAUDE.md exactly:

1. Read the Meshtastic protobuf definition for this portnum in `proto/meshtastic/`. Understand the message structure, field types, and which fields are optional.

2. Create `src/Handlers/{Name}Handler.cs` implementing `IPacketHandler`:
   - Deserialize the portnum-specific payload from `MeshPacket.Decoded.Payload`
   - Extract relevant fields (position, telemetry, text, etc.)
   - Build CoT XML using `src/CoT/` builder utilities
   - Inject via CotDispatcher
   - Handle missing/optional fields gracefully (show "Unknown" or omit)
   - Log malformed packets at Warning level and return without crashing

3. Register the handler in `src/Plugin/HandlerRegistry.cs` with the correct portnum enum value

4. Create `tests/Handlers/{Name}HandlerTests.cs` with these test cases:
   - **Happy path**: valid packet with all fields → verify correct CoT XML output (parse XML, assert elements)
   - **Missing fields**: packet with optional fields absent → verify graceful degradation
   - **Malformed payload**: garbage bytes → verify Warning log and no exception thrown
   - **XML safety**: shortname/longname with special characters (<, >, &, quotes) → verify proper XML escaping

5. Run `dotnet build src/` — fix any compiler errors
6. Run `dotnet test tests/` — fix any test failures
7. Update CHANGELOG.md with the new handler entry

Check docs/requirements.docx Section 10 (Portnum Reference) for the priority and delivery phase of this portnum. Ensure all CoT output follows the schema rules in CLAUDE.md.
