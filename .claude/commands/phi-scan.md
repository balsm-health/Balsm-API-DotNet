# PHI Scan

Deep scan of changed files for Protected Health Information exposure risks.

## Instructions

1. Run `git diff HEAD` (or `git diff --cached`) to identify all changed files.
2. Read each changed file thoroughly and analyze for the following PHI exposure categories:

### Logging & Debug Output
- Patient names, addresses, phone numbers, email addresses in log statements
- Clinical content (diagnoses, medications, test results, clinical notes) in logs
- Full request/response bodies logged that may contain patient data
- Patient data in `Console.WriteLine` or similar debug output
- PHI in structured log message templates (even templated, the data is stored)
- Log statements containing exception messages that may include patient data from validation

### Error Messages & Responses
- API error responses including patient data in error details
- Exception messages containing clinical content or patient identifiers (beyond record IDs)
- Stack traces exposing patient data from method parameters or local variables
- Validation error messages that echo back sensitive input

### AI & External Services
- Patient data sent to external AI services in identifiable form
- AI prompts containing PHI without de-identification
- Prompts containing unnecessary patient context (must send minimum data required)
- External API calls including patient data without encryption
- Webhook payloads containing PHI

### Caching & Session
- PHI stored in cache (in-memory or distributed)
- Cache keys containing patient identifiers that could be enumerated
- Session data containing clinical information that persists after logout
- AI conversation context not cleared between sessions or users

### Code Artifacts
- Real patient data in test fixtures, seed data, or mock data
- PHI in code comments, TODO notes, or documentation
- Patient data in commit messages or PR descriptions
- Production data samples in configuration files

### Data in Transit
- PHI transmitted without TLS/encryption
- Patient data in URL query parameters (visible in logs, browser history, referrer headers)
- PHI in notification content without encryption
- Unencrypted PHI in message queue payloads

### Field-Level Check
These fields must be encrypted at rest:
- Diagnoses and clinical notes
- Medication details and prescription content
- Test results and lab values
- Mental health and substance abuse records
- HIV/STI status
- National ID numbers

3. For each issue found, report:
   - File path and line number
   - PHI category and risk level
   - Description of the exposure
   - Suggested remediation

4. If no issues are found, confirm the code passes PHI scan.
