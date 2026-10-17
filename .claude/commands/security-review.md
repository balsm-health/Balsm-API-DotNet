# Security Review

Review all changed or staged files for security vulnerabilities specific to the Balsm healthcare platform.

## Instructions

1. Run `git diff HEAD` (or `git diff --cached` if there are staged changes) to identify all changed files.
2. Read each changed file and analyze for the following security issues:

### PHI & Data Exposure
- PHI logged, printed, or exposed in error messages (log only record IDs, never content)
- Full request/response bodies logged (must never contain patient data)
- Internal system details or database schemas leaked in error responses
- Real patient data in test fixtures, comments, or commit messages (use synthetic data only)

### Authentication & Authorization
- Endpoints missing permission checks (every endpoint must verify user permissions)
- Authorization bypassed in tests or dev utilities
- Security controls weakened to fix a bug or simplify code
- Session data leaking across tenants or environments

### Injection & Input Validation
- SQL injection: user input concatenated into queries (must use parameterized queries)
- XSS: unsanitized user input rendered in UI
- Prompt injection: AI-facing inputs not sanitized
- Command injection in any shell or system calls
- Missing input validation on API side (FluentValidation required per command/query)

### Secrets & Configuration
- Hardcoded secrets, API keys, passwords, or connection strings (must use environment variables)
- Credentials in source code, comments, or configuration files
- Encryption keys or tokens committed to the repository

### Data Protection
- Sensitive fields (diagnoses, medications, test results) missing field-level encryption
- Patient data sent to external services without encryption and consent verification
- Hard-delete of clinical or patient data (must use soft-delete)
- Missing immutable audit log for patient record modifications

### Tenant Isolation
- Data access crossing tenant boundaries
- Cached data served across tenants (cache keys must include tenant ID)
- AI context bleeding between sessions or users

### Async Safety
- Blocking async: `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` (can cause deadlocks)
- `async void` methods (unhandled exceptions crash the process)
- Missing `CancellationToken` propagation

3. For each issue found, report:
   - File path and line number
   - Severity: CRITICAL / HIGH / MEDIUM / LOW
   - Description of the vulnerability
   - Suggested fix

4. If no issues are found, confirm the code passes security review.
