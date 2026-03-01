Audit the codebase against all SEC-* requirements from docs/requirements-supplement.docx.

## Check each requirement and report PASS/FAIL:

### SEC-01: Channel PSK Enforcement
- [ ] Plugin only transmits on channels where the connected node holds the PSK
- [ ] Encrypted packets the node can't decrypt are silently discarded

### SEC-02: Admin Channel Protection
- [ ] Admin channel is identified from node config
- [ ] Admin channel is visually distinguished in the channel list UI
- [ ] Admin channel is EXCLUDED from the outbound channel selector by default
- [ ] Unlocking admin channel requires explicit settings toggle + confirmation dialog

### SEC-03: Admin Channel Confirmation
- [ ] If admin unlock exists, it shows the confirmation prompt text from the supplement
- [ ] Default state is locked

### SEC-04: No PSK Exposure
- Search ALL source files for any reference to PSK, encryption key, channel key
- [ ] No PSK values in log statements (search for Log, Logger, Console.Write, Debug.Write)
- [ ] No PSK values in CoT remarks or detail fields
- [ ] No PSK values in UI text, labels, or tooltips
- [ ] No PSK values in settings persistence files

### SEC-05: Credential Storage
- [ ] Connection hostname/port uses secure storage mechanism if available
- [ ] At minimum, not stored in plaintext in an easily accessible config file

### SEC-06: Classification Preservation
- [ ] Plugin does not strip classification markings from CoT events
- [ ] Plugin does not add classification markings (inherits from TAK Server)

### SEC-07: XML Injection Prevention
- Search all CoT builder methods for string data from mesh packets
- [ ] Every shortname insertion is XML-escaped
- [ ] Every longname insertion is XML-escaped
- [ ] Every text message insertion is XML-escaped
- [ ] Any other mesh-sourced string is XML-escaped
- [ ] Escaping covers: < > & ' "

### SEC-08: Node ID Validation
- [ ] Incoming packets are checked for plausible node IDs
- [ ] Packets that spoof the connected node's own ID are rejected

## Report
- Total PASS/FAIL count
- For each FAIL: file path, line number, description, and suggested fix
- Overall security posture assessment (Ready / Needs Work / Critical Issues)
