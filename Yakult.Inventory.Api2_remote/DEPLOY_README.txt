================================================================================
YAKULT INVENTORY API2 - MOBILE REST API DEPLOYMENT
================================================================================

WHAT WAS ADDED:
---------------
1. api.ashx              - Main REST API router (handles ALL /api/* endpoints)
2. Global.asax (updated) - URL rewriting to route /api/* to api.ashx
3. Web.config (updated)  - CORS headers and module configuration

ENDPOINTS NOW AVAILABLE:
------------------------
These endpoints are now handled by api.ashx:

Auth:
  POST /api/auth/login           - Login with username/password, returns JWT token
  POST /api/auth/register        - Register new user

Health:
  GET  /api/health               - Health check with database info

SetUpdates:
  GET  /api/SetUpdates           - List set updates by setCode
  POST /api/SetUpdates/Upload    - Upload set updates
  GET  /api/SetUpdates/PendingCount
  GET  /api/SetUpdates/Processed
  GET  /api/SetUpdates/Pending

Items:
  POST /api/Items/CreateBatch         - Create batch items
  POST /api/Items/ReceiveSerialFromMobile - Queue serial for Windows app
  GET  /api/Items/Categories     - List item categories
  GET  /api/Items/Conditions     - List item conditions
  GET  /api/Items/Vendors        - List vendors

Sets:
  GET  /api/sets/by-token/{token} - Get set by QR token

Dispatch:
  PUT  /api/dispatch/{setId}     - Deploy a set
  GET  /api/dispatch/resolve-token/{token}
  GET  /api/dispatch/status

Borrow:
  GET  /api/Borrow/Resolve       - Resolve item by serial
  GET  /api/Borrow/Employees     - List/search employees
  POST /api/Borrow/Employees     - Create employee
  GET  /api/Borrow/Companies     - List companies
  GET  /api/Borrow/Branches      - List branches by company
  GET  /api/Borrow/Departments   - List departments by company
  POST /api/Borrow               - Create borrow record
  POST /api/Borrow/Return        - Return borrowed item
  POST /api/Borrow/Delete        - Delete borrow record
  GET  /api/Borrow/Open          - List open borrows
  GET  /api/Borrow/Home          - Home summary stats
  GET  /api/Borrow/History       - Borrow history

Reports (basic):
  GET  /api/Reports/Summary
  GET  /api/Reports/Module/{id}

EXISTING ENDPOINTS (unchanged):
-------------------------------
These legacy .ashx endpoints continue to work:
  GET  /dbinfo.ashx
  GET  /get-image.ashx?id={id}
  GET  /mobile-claim.ashx
  POST /mobile-confirm.ashx
  POST /mobile-cancel.ashx
  GET  /set-images.ashx?token={token}&set_code={code}
  POST /set-image-upload.ashx

DEPLOYMENT STEPS:
-----------------
1. Copy this entire folder to your IIS web root (e.g., C:\inetpub\wwwroot\YakultAPI)

2. In IIS Manager:
   - Create a new Application Pool (or use existing)
     * .NET CLR Version: v4.0
     * Managed Pipeline Mode: Integrated
   - Create a new Website or Application
     * Physical path: point to this folder
     * Port: 7014 (or your desired port)

3. Configure SQL Server permissions:
   - Ensure the app pool identity (or specified user) can access YIMS database
   - Required tables: [User], DepartmentAccount, Employee, Department, Company, 
     Branch, Item, ItemCategory, [Condition], Vendor, BorrowLog, [Set], SetItemUpdate

4. Update Web.config if needed:
   - connectionStrings: Update Data Source, User ID, Password if different
   - appSettings: AuthTokenSecret and AuthTokenExpiryHours

5. Test endpoints:
   - GET http://localhost:7014/api/health
   - POST http://localhost:7014/api/auth/login
     Body: {"username":"youruser","password":"yourpass"}

AUTHENTICATION:
---------------
- All /api/* endpoints (except login/register/health) require JWT token
- Include header: Authorization: Bearer {token}
- Token expires after 12 hours (configurable in Web.config)

PASSWORD HASHING:
-----------------
The API supports the existing password hashing from the desktop app:
- SHA256 with salt (PasswordHash/PasswordSalt columns)
- Legacy DepartmentAccount passwords auto-migrate on first login

TROUBLESHOOTING:
----------------
- If /api/* returns 404: Ensure Global.asax is in the root and IIS is using Integrated pipeline mode
- If CORS errors: Web.config already has CORS headers configured
- If login fails: Check SQL connection string and ensure tables exist
- Check Windows Event Viewer for ASP.NET errors

================================================================================
