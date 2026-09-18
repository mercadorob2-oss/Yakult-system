# How-To: Operate Borrow and Return Transactions

## Document Control

| Field | Value |
| --- | --- |
| Document owner | ITD / IT Support |
| Audience | IT technical + IT support |
| System | Borrow Items Subsystem |
| Version | 1.0 |
| Effective date | 2026-03-11 |
| Last updated | 2026-03-11 |

## 1. Purpose

This procedure defines the operational steps for logging a borrow, logging a return, and handling non-listed items or non-listed employees.

## 2. Preconditions

- Borrow Items dashboard is open
- schema is installed for full logging
- operator has a valid logged-in user session
- relevant item and employee data exist, unless intentionally using add-new flow

## 3. Borrow Existing Inventory Item

1. In the `Borrow Item` panel, enter or scan the serial number.
2. Click `Resolve`.
3. Review:
   - item name
   - model / item display
   - description
4. Select:
   - company
   - department
   - employee
5. Click `Borrow`.
6. Review the confirmation dialog.
7. Click `Confirm borrow`.

Expected result:

- borrow is logged
- serial appears in `Open Borrows`
- return section can be prefilled for the same serial

## 4. Borrow Item Not Yet In Inventory

1. Enter or scan a serial number that does not exist.
2. Click `Resolve`.
3. When prompted, choose to add the item now.
4. In `Add Item / Borrow`, stay on `Not Listed (External/New)` if creating a new record.
5. Complete required item details.
6. Select borrower if required.
7. Save the dialog.

Expected result:

- item is created in inventory
- borrow is also logged immediately when schema and borrower are available

If immediate borrow does not occur:

- record the created serial
- return to the dashboard
- perform a standard borrow using that serial

## 5. Borrow Listed Inventory Through Add Dialog

This branch is used when the operator opened the add-and-borrow dialog but the item actually already exists in inventory.

1. In `Add Item / Borrow`, select `Listed in Inventory`.
2. Enter the serial.
3. Click `Resolve`.
4. Review item details.
5. Select borrower.
6. Click `Borrow`.

Expected result:

- no new item is created
- a normal borrow log row is inserted

## 6. Use Not Listed Employee

If the borrower or returner is not available in the dropdown:

1. Choose `Not listed (Add employee)`.
2. Open quick-add employee.
3. Fill required employee data.
4. Save the employee.
5. Return to borrow or return operation.

Important:

- the employee must have a valid department to become usable for borrow logging

## 7. Return Borrowed Item

1. In the `Return Item` panel, enter or scan the serial number.
2. Click `Find Open`.
3. Confirm the system displays the borrower and borrowed timestamp.
4. Select the actual returner.
5. Click `Return`.
6. Review the confirmation dialog.
7. Click `Confirm return`.

Expected result:

- the open row is closed
- the row moves to `History`

## 8. Return Selected From Open Grid

Use this shortcut when the item is visible in `Open Borrows`.

1. Select the desired row in `Open Borrows`.
2. Click `Return Selected`.
3. Review the prefilled return panel and confirmation dialog.
4. Confirm the return.

Expected result:

- selected row is returned without retyping the serial manually

## 9. Interpret Open And History Tabs

### 9.1 Open Borrows

Used for:

- current custody review
- quick return selection
- elapsed aging review

### 9.2 History

Used for:

- completed borrow/return audit review
- verifying who borrowed and who returned
- export evidence for past transactions

## 10. Good Operational Practice

- verify the returner, do not rely on the default selection blindly
- export CSV when transaction state is disputed
- do not use production for test item creation unless authorized
- verify employee department correctness before logging

## 11. Revision History

| Version | Date | Change |
| --- | --- | --- |
| 1.0 | 2026-03-11 | Initial release |
