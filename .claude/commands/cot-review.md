Review all CoT XML generation in `src/CoT/` and all handlers in `src/Handlers/` for compliance with project requirements and security rules.

Check each of the following and report as a checklist with PASS/FAIL per item:

## Schema Compliance
- [ ] Every CoT builder produces well-formed XML (parse each output template with XmlDocument)
- [ ] CoT type strings match the mapping in CLAUDE.md (a-f-G-U-C for clients, a-f-G-U-C-I for routers, a-f-G-E-S for trackers)
- [ ] Stale times are set correctly (30 min default for node PLI, configurable)
- [ ] All required CoT fields are present: uid, type, time, start, stale, point (lat/lon/hae/ce/le)

## Security (SEC Requirements)
- [ ] SEC-04: No PSK values appear in ANY remarks, detail fields, log statements, or UI text
- [ ] SEC-07: All string data from mesh packets (shortname, longname, text messages) is XML-escaped before embedding in CoT
- [ ] SEC-02: Admin channel is excluded from outbound message paths
- [ ] SEC-08: Node ID validation is present — reject packets with spoofed self-ID

## Test Coverage
- [ ] Every CoT builder method has at least one unit test
- [ ] Every handler has happy path, missing fields, and malformed packet tests
- [ ] XML escaping is tested with special characters: < > & ' "
- [ ] List any CoT output path that does NOT have a corresponding test

## Channel/Team Color
- [ ] Channel-to-team-color assignment follows the mapping in CLAUDE.md
- [ ] Nodes heard on multiple channels use lowest-index channel color

Report the total PASS/FAIL count at the end. For any FAIL, include the file path, line number, and a brief description of what needs to change.
