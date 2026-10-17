# Audit Trail Check

Review changed files for audit trail completeness on sensitive operations.

## Instructions

1. Run `git diff HEAD` (or `git diff --cached`) to identify all changed files.
2. Read each changed file that touches patient records, clinical data, financial operations, or administrative changes.
3. Analyze for the following:

### Patient Record Modifications
- Create, update, or delete operations on patient records missing audit log entry
- Audit entry missing required fields:
  - Previous value (snapshot before change)
  - New value (snapshot after change)
  - User ID who performed the action
  - Timestamp (UTC)
  - Action type (create, update, soft-delete, restore, view)
  - Correlation ID for request tracing
  - Module/bounded context where the action occurred
- Audit records that can be modified or deleted (must be append-only/immutable)
- Bulk operations skipping individual record audit entries

### Clinical Operations
- Prescription creation, modification, dispensation without audit entry
- Lab order placement, specimen processing, result verification without audit trail
- Radiology order and report lifecycle events not audited
- Clinical note creation or amendment without audit record
- Consent grant, modification, or revocation not audited
- Care team membership changes not logged
- Referral creation and status changes not tracked

### Financial Operations
- Payment processing without audit trail
- Billing code changes without audit entry
- Insurance claim submissions not logged
- Financial transaction modifications without recording the modifier and reason

### Administrative Operations
- Permission changes (grant/revoke) not audited
- User account creation, deactivation, role changes not tracked
- System configuration changes not audited

### Sensitive Data Access
- Read access to certain sensitive records not logged:
  - Mental health records
  - Substance abuse records
  - HIV/STI status
  - Records accessed during emergency override ("break the glass")
- Break-the-glass access not logged with justification and user ID

4. For each issue found, report:
   - File path and line number
   - Operation type missing audit trail
   - Severity: CRITICAL / HIGH / MEDIUM
   - Suggested fix

5. If no issues are found, confirm the code passes audit trail check.
