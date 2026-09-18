$Manual = [ordered]@{
    Title = 'Borrow Items - IT Support and Operations Manual'
    Subtitle = 'WinForms Borrow and Return Ledger Administration, Operations, and Troubleshooting Guide'
    Version = 'v1.0'
    Date = '2026-03-11'
    Notes = @(
        'This manual is intended for IT support, IT technical staff, and application support personnel responsible for operating and supporting the Borrow Items subsystem in the Yakult Inventory WinForms application.'
        'The Borrow Items subsystem is a standalone serialized-item borrow ledger. It depends on the WinForms application, SQL connectivity, employee master data, and the BorrowLog schema.'
    )
    Sections = @(
        @{
            Title = '1) Purpose and Scope'
            Paragraphs = @(
                'Borrow Items is the WinForms subsystem used to log borrowing and returning of serialized hardware items. It allows support personnel to resolve an item by serial number, assign the borrower, log the borrow, resolve an open row later, and log the return into the same audit row.',
                'This manual covers access, schema readiness, borrow and return operation, add-new-item paths, employee lookup behavior, monitoring, history review, CSV export, and support handling. It is written for IT support and operational staff rather than casual requesters.'
            )
            Bullets = @(
                'Primary UI: Yakult.Inventory.App/Forms/BorrowItemsDashboard.cs.'
                'Primary data layer: Yakult.Inventory.App/Repositories/BorrowItemsRepository.cs.'
                'Primary schema script: Yakult.Inventory.App/Database/BorrowItems.Prod.Install.sql.'
            )
        }
        @{
            Title = '2) Main Dashboard Overview'
            Paragraphs = @(
                'The main dashboard is divided into a left-side operations area and a right-side monitoring area. The left side contains the Borrow Item and Return Item panels. The right side contains KPI cards, open rows, history rows, pagination, refresh, export, and portal navigation.',
                'Support should treat this dashboard as both a transaction screen and an operational monitoring screen. A healthy module loads the employee lookups, open/history tabs, and KPI indicators together.'
            )
            Bullets = @(
                'Borrow Item panel is used for resolving and logging new borrow rows.'
                'Return Item panel is used for finding and closing open rows.'
                'Open KPI shows current open borrow count.'
                'Oldest Open KPI shows the age of the oldest active borrow record.'
            )
            Images = @('borrow_dashboard.png')
        }
        @{
            Title = '3) Borrow Item Panel'
            Paragraphs = @(
                'The Borrow Item panel is the normal starting point for a borrow transaction. The operator scans or types a serial, clicks Resolve, reviews the item details, selects company, department, and borrower, then confirms the borrow.',
                'If the serial exists in inventory and no open borrow exists, the Borrow button becomes the normal transaction path. If the item cannot be found, the operator can move into the Add New branch.'
            )
            Bullets = @(
                'Resolve checks inventory by exact serial number.'
                'If an open row already exists for the same item, the system blocks the borrow.'
                'Borrow requires a valid listed employee with a department.'
                'The transaction is confirmed again through a borrow confirmation dialog.'
            )
            Images = @('borrow_item_section.png')
        }
        @{
            Title = '4) Return Item Panel'
            Paragraphs = @(
                'The Return Item panel is used to close an existing open borrow row. The operator enters or scans a serial and uses Find Open to load the current active borrow record.',
                'Once the row is resolved, the dashboard shows who borrowed the item and when it was borrowed. The operator then selects the actual returner and confirms the return.'
            )
            Bullets = @(
                'Find Open searches the current open borrow ledger, not the inventory master alone.'
                'Return requires a valid listed employee with a department.'
                'Return is confirmed again through a return confirmation dialog.'
                'The same borrow row is updated rather than creating a separate return row.'
            )
            Images = @('return_item_section.png')
        }
        @{
            Title = '5) Open Rows, History, and KPI Monitoring'
            Paragraphs = @(
                'The right side of the dashboard provides operational visibility. Open Borrows shows active custody rows. History shows completed borrow-return records. The KPI area summarizes total open count and the age of the oldest active borrow.',
                'This view is the main support checkpoint for disputes about whether an item is still open, whether it was returned, or whether the borrow module is refreshing correctly after transactions.'
            )
            Bullets = @(
                'Open Borrows includes elapsed time for currently active rows.'
                'History includes borrowed date, returned date, and returned-by value.'
                'Return Selected acts on the currently selected open row.'
                'CSV Export exports the active tab only.'
            )
            Images = @('borrow_table_list.png', 'borrow_history_table.png')
        }
        @{
            Title = '6) Borrower Selection - Listed Employee'
            Paragraphs = @(
                'The normal borrower path is Listed employee. In this mode, the operator chooses from employee records loaded from the active employee master with valid department assignments.',
                'Company and Department act as lookup filters. The actual borrow row uses the selected employee identity and its associated department values.'
            )
            Bullets = @(
                'Employee display format is Name - Department.'
                'Employees without departments are not valid for borrow logging.'
                'Listed employee is the primary supported transaction path.'
            )
            Images = @('borrowed_by_listem_employee.png')
        }
        @{
            Title = '7) Borrower Selection - Not Listed Employee'
            Paragraphs = @(
                'If the borrower does not yet exist in the lookup, the operator can switch to Not listed (Add employee). This opens the quick-add employee dialog and allows a new employee record to be created during the transaction flow.',
                'After creation, the employee becomes usable only if the record contains the required department relationship. Support should not assume the employee is valid for logging until the lookup repopulates correctly.'
            )
            Bullets = @(
                'Quick Add Employee is an operational shortcut, not a replacement for good master-data governance.'
                'Employees without departments will not become valid borrower selections.'
                'This same pattern applies to returner selection when needed.'
            )
            Images = @('borrowed_by_not_listem_employee.png')
            Callouts = @(
                @{ Kind = 'warn'; Text = 'If a newly added employee does not appear in the selection list, verify that the employee was saved with a valid Department before escalating the issue.' }
            )
        }
        @{
            Title = '8) Add Item / Borrow - Listed Inventory Path'
            Paragraphs = @(
                'The Add Item / Borrow dialog has a Listed in Inventory path for cases where the operator entered the add flow but the item already exists in the inventory master. In this branch, the operator resolves an existing serial and logs a normal borrow without creating a new item.',
                'This path is useful when the serial was initially treated as unknown operationally but the inventory master already contains the item.'
            )
            Bullets = @(
                'No new item is created in this path.'
                'The dialog returns the resolved serial and selected borrower back into the main borrow workflow.'
                'This is effectively a guided shortcut to a normal borrow transaction.'
            )
            Images = @('item_borrow_Listed_Inventory.png')
        }
        @{
            Title = '9) Add Item / Borrow - External or New Item Path'
            Paragraphs = @(
                'The Not Listed (External/New) branch is used when the serial does not yet exist in inventory. The operator fills out the add-item form, then saves the new item so the application can create the inventory record first.',
                'After the item is created, the dashboard attempts to log the borrow immediately when schema and borrower data are available. If immediate borrow cannot happen, the system instructs the operator to borrow the item later using the resulting serial.'
            )
            Bullets = @(
                'This path creates a new inventory item before borrow logging occurs.'
                'Borrow-friendly defaults are applied in the code for the created item.'
                'If schema is missing, the operator can still create the item but cannot log the borrow.'
            )
            Images = @('item_borrow_Listed_not_Inventory.png')
        }
        @{
            Title = '10) Schema Readiness and Data Dependencies'
            Paragraphs = @(
                'Full Borrow Items functionality depends on the existence of dbo.BorrowLog in the connected database. The module checks schema availability on startup and displays a warning if the schema is missing.',
                'The subsystem also depends on Item, Employee, Department, and User master data. Missing or incomplete records in those dependencies directly affect whether a borrow or return can be logged.'
            )
            Bullets = @(
                'Missing BorrowLog means borrow and return logging are unavailable.'
                'The module still allows inventory item creation in some schema-missing scenarios.'
                'Borrow and return both require employees with departments.'
                'Only one open borrow is allowed per item.'
            )
            Callouts = @(
                @{ Kind = 'tip'; Text = 'If the dashboard opens but warns that the schema is missing, the application is reaching the database. The problem is the target database schema, not the basic app launch.' }
            )
        }
        @{
            Title = '11) Support Troubleshooting Pattern'
            Paragraphs = @(
                'Support should always isolate the failure point first. In Borrow Items, the most common breakpoints are access, schema, serial resolution, open-row conflicts, employee master data, and environment mismatch.',
                'When investigating, collect screen evidence, the exact serial number, the username, and whether the issue occurred during borrow, return, or add-new-item flow. Use CSV export when the row state itself is disputed.'
            )
            Bullets = @(
                'If the portal card is missing, start with role permissions.'
                'If the dashboard warns about schema, verify BorrowItems.Prod.Install.sql deployment.'
                'If a serial cannot be found, verify the item exists in inventory for that environment.'
                'If a return says no open row exists, verify the row has not already been closed or logged in another environment.'
                'If an employee is missing, check Department assignment.'
            )
        }
        @{
            Title = '12) Quick Support Checklist'
            Bullets = @(
                'Can the user see the Borrow Items card?'
                'Does the dashboard open without schema warning?'
                'Can a known serial resolve correctly?'
                'Can a valid listed employee be selected?'
                'Does a borrow appear in Open Borrows?'
                'Does a return move the row into History?'
                'Can the module export CSV successfully?'
                'Have screenshots and row evidence been collected before escalation?'
            )
        }
    )
}
