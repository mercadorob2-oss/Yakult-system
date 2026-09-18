# Borrow Items Documentation (Diataxis + Corporate Control)

This folder contains formal technical and support documentation for the Borrow Items subsystem in the WinForms application.

Primary implementation area:

- `Yakult.Inventory.App/Forms/BorrowItemsDashboard.cs`
- `Yakult.Inventory.App/Forms/BorrowItems/*`
- `Yakult.Inventory.App/Repositories/BorrowItemsRepository.cs`
- `Yakult.Inventory.App/Database/BorrowItems.Prod.Install.sql`

The documentation follows the **Diataxis** model:

- **Tutorials**: onboarding and guided learning
- **How-to guides**: task-based support procedures
- **Reference**: stable facts, schema, roles, controls, and operational rules
- **Explanation**: architecture, design intent, and ledger logic

All documents are written in a corporate-style format with document control, purpose, scope, procedure, and revision history sections where appropriate.

## Start here (recommended order)

- `docs/borrow/explanation/overview.md`
- `docs/borrow/how-to/admin-runbook.md`
- `docs/borrow/how-to/troubleshooting.md`

## Index

### Tutorials

- `docs/borrow/tutorials/borrow-support-onboarding.md`

### How-to guides

- `docs/borrow/how-to/admin-runbook.md`
- `docs/borrow/how-to/verify-schema-and-access.md`
- `docs/borrow/how-to/operate-borrow-and-return.md`
- `docs/borrow/how-to/troubleshooting.md`

### Reference

- `docs/borrow/reference/database.md`
- `docs/borrow/reference/screens-roles-and-controls.md`
- `docs/borrow/reference/policies-and-known-gaps.md`

### Explanation

- `docs/borrow/explanation/overview.md`
- `docs/borrow/explanation/technical-design.md`
- `docs/borrow/explanation/borrow-ledger-and-workflow-logic.md`
