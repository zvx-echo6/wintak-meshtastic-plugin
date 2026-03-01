Document a Phase 0 SDK spike finding for: $ARGUMENTS

## Investigation Report

Create a markdown file at `docs/sdk-findings/$ARGUMENTS.md` with the following sections:

### 1. Summary
One paragraph: what was investigated and why it matters for the plugin.

### 2. SDK API / Method Tested
- The exact class, method, or API endpoint tested
- Which WinTAK SDK assembly it lives in
- Method signature and parameters

### 3. Code Sample
Include a minimal working code sample that demonstrates the capability. This should be copy-pasteable into the skeleton plugin.

### 4. Findings
- Does it work as we assumed in the requirements?
- Any limitations, quirks, or differences from expectations
- Any required workarounds

### 5. Impacted Requirements
List every requirement ID from docs/requirements.docx that this finding affects (e.g., CHN-02, MSG-01, TEL-03).

### 6. Recommendation
State whether we should use the **PRIMARY** or **FALLBACK** design from docs/requirements-supplement.docx for the impacted features. Explain why.

### 7. CLAUDE.md Updates
If this finding changes how we build, test, or structure the code:
- Propose specific lines to add/change in CLAUDE.md
- Apply the changes after confirmation

## After creating the finding document:
- Read the existing docs/sdk-findings/ directory and check for conflicts with other findings
- If this finding changes the recommended approach for other features, note the cross-references
