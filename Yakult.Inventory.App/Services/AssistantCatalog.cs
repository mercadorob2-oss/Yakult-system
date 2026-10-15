using System;
using System.Collections.Generic;
using System.Linq;

namespace Yakult.Inventory.App.Services
{
    /// <summary>Reviewed SELECT sources and exposed fields. No SQL fragment comes from a model or question.</summary>
    public static class AssistantCatalog
    {
        private static readonly IReadOnlyList<AssistantCapability> _all = Build().AsReadOnly();
        public static IReadOnlyList<AssistantCapability> All { get { return _all; } }
        public static AssistantCapability Find(string id) { return _all.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)); }
        private static string[] Split(string value) { return (value ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries); }

        private static AssistantCapability Add(List<AssistantCapability> list, string id, string title, string module,
            string portals, string pages, string aliases, string sql, string columns, string search,
            string groups = "Status", string totals = "", string utc = "CreatedAt", string date = "CreatedAt")
        {
            var capability = new AssistantCapability { Id=id, Title=title, Module=module, Source=module+" → "+title,
                PortalKeys=Split(portals), PageKeys=Split(pages), Aliases=Split(aliases), BaseSql=sql,
                Columns=Split(columns), SearchColumns=Split(search), GroupColumns=Split(groups), AggregateColumns=Split(totals),
                UtcDateColumns=Split(utc), DefaultDateField=date };
            list.Add(capability); return capability;
        }

        private static List<AssistantCapability> Build()
        {
            var list=new List<AssistantCapability>();
            const string itemJoins="dbo.Item i LEFT JOIN dbo.Condition c ON c.ConditionID=i.ConditionID";
            var c=Add(list,"items","Items and stock","Inventory","InventorySystem","ViewItemsPage",
                "stock|item|items|keyboard|laptop|mouse|monitor|memory|ram|printer|battery|cable|inventory stock|phone|phones|cellphone|cellphones|cell phone|cell phones|smartphone|smartphones|mobile phone|mobile phones",
                @"SELECT CAST(i.ItemId AS BIGINT) Id,i.Name Label,i.ModelNumber Model,i.SerialNumber Serial,i.Category Category,
                c.ConditionName Condition,CAST(ISNULL(i.StockOnHand,0) AS DECIMAL(28,4)) Units,i.DateCreated CreatedAt,
                CASE WHEN i.Active=1 THEN 'Active' ELSE 'Inactive' END Status,
                CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ArchiveStatus ar WHERE ar.EntityType='Item' AND ar.EntityId=i.ItemId AND ar.IsArchived=1) THEN 1 ELSE 0 END AS BIT) Archived
                FROM "+itemJoins,"Id|Label|Model|Serial|Category|Condition|Units|CreatedAt|Status|Archived","Label|Model|Serial|Category","Category|Condition|Status","Units");
            c.ArchiveColumn="Archived"; c.CategoryScope=true;c.HistoryCapability="item-history";

            c=Add(list,"inventory","Inventory movements","Inventory","InventorySystem|Reports","ViewInventoryPage",
                "inventory|inventory movement|posted quantity|posting|stock movement",
                @"SELECT CAST(v.InvId AS BIGINT) Id,i.Name Label,i.ModelNumber Model,i.Category Category,v.EntryType Status,
                v.Quantity Units,v.DatePosted CreatedAt,v.ReqId RequestId,v.SetId SetId,u.Name PostedBy,
                CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ArchiveStatus ar WHERE ar.EntityType='Inventory' AND ar.EntityId=v.InvId AND ar.IsArchived=1) THEN 1 ELSE 0 END AS BIT) Archived
                FROM dbo.Inventory v LEFT JOIN dbo.Item i ON i.ItemId=v.ItemId LEFT JOIN dbo.[User] u ON u.UserId=v.PostedBy",
                "Id|Label|Model|Category|Status|Units|CreatedAt|RequestId|SetId|PostedBy|Archived","Label|Model|PostedBy","Status|Category","Units");
            c.ArchiveColumn="Archived";c.CategoryScope=true;

            c=Add(list,"assets","Assets","Inventory","InventorySystem|Reports","ViewAssetPage",
                "asset|assets|asset serial|fixed asset",
                @"SELECT CAST(a.AssetId AS BIGINT) Id,COALESCE(i.Name,a.ModelNumber,a.SerialNumber) Label,a.SerialNumber Serial,a.ModelNumber Model,
                i.Category Category,v.VendorName Vendor,a.CreatedAt CreatedAt,CASE WHEN a.IsActive=1 THEN 'Active' ELSE 'Inactive' END Status
                FROM dbo.Asset a LEFT JOIN dbo.Item i ON i.ItemId=a.ItemId LEFT JOIN dbo.Vendor v ON v.VendorID=a.VendorId",
                "Id|Label|Serial|Model|Category|Vendor|CreatedAt|Status","Label|Model|Serial|Vendor","Category|Status","","", "CreatedAt"); c.CategoryScope=true;

            c=Add(list,"warranties","Warranty","Inventory","InventorySystem|Reports","ViewWarrantyPage",
                "warranty|warranties|warranty expiration",
                @"SELECT CAST(i.ItemId AS BIGINT) Id,i.Name Label,i.ModelNumber Model,i.SerialNumber Serial,i.Category Category,
                i.WarrantyStartDate StartDate,i.WarrantyEndDate EndDate,i.DateCreated CreatedAt,
                CASE WHEN i.WarrantyEndDate<@TodayLocal THEN 'Expired' ELSE 'Active' END Status
                FROM dbo.Item i WHERE i.Active=1 AND i.WarrantyEndDate IS NOT NULL
                AND NOT EXISTS(SELECT 1 FROM dbo.ArchiveStatus ar WHERE ar.EntityType='Item' AND ar.EntityId=i.ItemId AND ar.IsArchived=1)",
                "Id|Label|Model|Serial|Category|StartDate|EndDate|CreatedAt|Status","Label|Model|Serial","Category|Status","","CreatedAt","EndDate");c.CategoryScope=true;

            Add(list,"employees","Employees","Inventory","InventorySystem","ViewEmployeesPage",
                "employee|employees|staff|person|who is|do you know",
                @"SELECT CAST(e.EmpId AS BIGINT) Id,e.Name Label,e.EmployeeNumber EmployeeNumber,e.Position Position,t.Code Title,
                d.Name Department,b.Name Branch,co.Name Company,e.DateCreated CreatedAt,CASE WHEN e.Active=1 THEN 'Active' ELSE 'Inactive' END Status
                FROM dbo.Employee e LEFT JOIN dbo.Title t ON t.TitleId=e.TitleId LEFT JOIN dbo.Department d ON d.DeptId=e.DeptId
                LEFT JOIN dbo.Branch b ON b.BranchId=e.BranchId LEFT JOIN dbo.Company co ON co.ComId=e.ComId",
                "Id|Label|EmployeeNumber|Position|Title|Department|Branch|Company|CreatedAt|Status","Label|EmployeeNumber","Department|Branch|Company|Position|Status");
            Add(list,"companies","Companies","Inventory","InventorySystem","ViewCompaniesPage","company|companies",
                "SELECT CAST(ComId AS BIGINT) Id,Name Label,Description Description,DateCreated CreatedAt,CASE WHEN Active=1 THEN 'Active' ELSE 'Inactive' END Status FROM dbo.Company",
                "Id|Label|Description|CreatedAt|Status","Label|Description");
            Add(list,"branches","Branches","Inventory","InventorySystem","ViewBranchesPage","branch|branches|distributor|distributors",
                "SELECT CAST(BranchId AS BIGINT) Id,Name Label,Description Description,BranchType BranchType,DateCreated CreatedAt,CASE WHEN Active=1 THEN 'Active' ELSE 'Inactive' END Status FROM dbo.Branch",
                "Id|Label|Description|BranchType|CreatedAt|Status","Label|Description|BranchType","Status|BranchType");
            Add(list,"departments","Departments","Inventory","InventorySystem","ViewDepartmentsPage","department|departments|section",
                "SELECT CAST(DeptId AS BIGINT) Id,Name Label,Section Section,DateCreated CreatedAt,CASE WHEN Active=1 THEN 'Active' ELSE 'Inactive' END Status FROM dbo.Department",
                "Id|Label|Section|CreatedAt|Status","Label|Section","Status|Section");
            Add(list,"vendors","Vendors","Inventory","InventorySystem","ViewVendorsPage","vendor|vendors|supplier|suppliers",
                "SELECT CAST(VendorID AS BIGINT) Id,VendorName Label,CreatedDate CreatedAt,CASE WHEN IsActive=1 THEN 'Active' ELSE 'Inactive' END Status FROM dbo.Vendor",
                "Id|Label|CreatedAt|Status","Label","Status","","","CreatedAt");

            c=Add(list,"sets","Sets and ownership","Inventory","InventorySystem|Reports","ViewSetsPage",
                "set|sets|deployed equipment|ownership|assigned equipment|my equipment|my assets",
                @"SELECT CAST(s.SetId AS BIGINT) Id,s.SetCode Label,s.Status Status,s.SetType SetType,s.CreatedAt CreatedAt,
                setOwner.EmpId EmployeeId,e.Name Employee,d.Name Department,b.Name Branch,co.Name Company,
                CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.ArchiveStatus ar WHERE ar.EntityType='Set' AND ar.EntityId=s.SetId AND ar.IsArchived=1) THEN 1 ELSE 0 END AS BIT) Archived
                FROM dbo.[Set] s OUTER APPLY (SELECT TOP (1) ro.EmpId FROM dbo.Request ro WHERE ro.SetId=s.SetId ORDER BY ro.ReqId) setOwner
                LEFT JOIN dbo.Employee e ON e.EmpId=setOwner.EmpId
                LEFT JOIN dbo.Department d ON d.DeptId=s.CurrentDepartmentId LEFT JOIN dbo.Branch b ON b.BranchId=s.CurrentBranchId
                LEFT JOIN dbo.Company co ON co.ComId=s.ComId WHERE ISNULL(s.IsInvoice,0)=0 AND s.Active=1",
                "Id|Label|Status|SetType|CreatedAt|EmployeeId|Employee|Department|Branch|Company|Archived","Label|Employee|Department|Branch|Company","Status|SetType|Employee|Department|Branch|Company");
            c.ArchiveColumn="Archived";c.PersonalPredicate="d.EmployeeId=@EmployeeId";c.HistoryCapability="set-items";
            c=Add(list,"set-items","Set contents","Inventory","InventorySystem|Reports","ViewSetsPage",
                "set contents|set item|set items",
                @"SELECT CAST(si.SetItemId AS BIGINT) Id,s.SetCode Label,CAST(si.SetId AS BIGINT) SetId,i.Name Item,i.ModelNumber Model,i.SerialNumber Serial,i.Category Category,
                si.Quantity Units,si.CreatedAt CreatedAt,s.Status Status,setOwner.EmpId EmployeeId FROM dbo.SetItem si INNER JOIN dbo.[Set] s ON s.SetId=si.SetId
                OUTER APPLY (SELECT TOP (1) ro.EmpId FROM dbo.Request ro WHERE ro.SetId=s.SetId ORDER BY ro.ReqId) setOwner
                INNER JOIN dbo.Item i ON i.ItemId=si.ItemId WHERE ISNULL(s.IsInvoice,0)=0 AND s.Active=1
                AND NOT EXISTS(SELECT 1 FROM dbo.ArchiveStatus ar WHERE ar.EntityType='Set' AND ar.EntityId=s.SetId AND ar.IsArchived=1)",
                "Id|Label|SetId|Item|Model|Serial|Category|Units|CreatedAt|Status|EmployeeId","Label|Item|Model|Serial","Category|Status","Units");c.ParentColumn="SetId";c.CategoryScope=true;c.PersonalPredicate="d.EmployeeId=@EmployeeId";

            c=Add(list,"requests","Requests","Requests","InventorySystem|RequesterPortal","ViewRequestsPage",
                "request|requests|pending request|unfulfilled|partially fulfilled",
                @"SELECT CAST(r.ReqId AS BIGINT) Id,CONCAT('REQ-',r.ReqId) Label,i.Name Item,i.ModelNumber Model,i.Category Category,r.Status Status,
                r.Quantity RequestedUnits,ISNULL(r.IssuedQty,0) IssuedUnits,
                CASE WHEN ISNULL(s.Status,'')='Dispatched' THEN 0 WHEN r.Quantity>ISNULL(r.IssuedQty,0) THEN r.Quantity-ISNULL(r.IssuedQty,0) ELSE 0 END OutstandingUnits,
                r.DateCreated CreatedAt,r.CreatedBy CreatedBy,r.EmpId EmployeeId,e.Name Employee,d.Name Department,b.Name Branch,
                CAST(r.SetId AS BIGINT) SetId,r.Active Active FROM dbo.Request r INNER JOIN dbo.Item i ON i.ItemId=r.ItemId
                LEFT JOIN dbo.[Set] s ON s.SetId=r.SetId LEFT JOIN dbo.Employee e ON e.EmpId=r.EmpId
                LEFT JOIN dbo.Department d ON d.DeptId=r.DeptId LEFT JOIN dbo.Branch b ON b.BranchId=r.BranchId WHERE r.Active=1",
                "Id|Label|Item|Model|Category|Status|RequestedUnits|IssuedUnits|OutstandingUnits|CreatedAt|CreatedBy|EmployeeId|Employee|Department|Branch|SetId|Active",
                "Label|Item|Model|Employee","Status|Category|Employee|Department|Branch","RequestedUnits|IssuedUnits|OutstandingUnits");
            c.RequesterScope=true;c.PersonalPredicate="d.CreatedBy=@UserId";c.CategoryScope=true;

            c=Add(list,"consumables","Consumable models","Consumables","InventorySystem|CartridgeManagement","ConsumableModelsPage",
                "consumable|consumables|toner|ink|print head|printhead",
                @"SELECT CAST(cm.ConsumableModelId AS BIGINT) Id,cm.ModelNumber Label,cm.ModelNumber Model,cm.Category Category,cm.CreatedAt CreatedAt,
                'Active' Status,stock.Units Units FROM dbo.ConsumableModel cm OUTER APPLY
                (SELECT ISNULL(SUM(CAST(ISNULL(i.StockOnHand,0) AS BIGINT)),0) Units FROM dbo.Item i WHERE i.Active=1 AND i.ConsumableModelId=cm.ConsumableModelId
                AND NOT EXISTS(SELECT 1 FROM dbo.ArchiveStatus ar WHERE ar.EntityType='Item' AND ar.EntityId=i.ItemId AND ar.IsArchived=1)) stock WHERE cm.IsActive=1",
                "Id|Label|Model|Category|CreatedAt|Status|Units","Label|Category","Category|Status","Units","","CreatedAt");c.CategoryScope=true;
            c=Add(list,"cartridges","Cartridge models","Consumables","CartridgeManagement","",
                "cartridge|cartridges|refilled stock|brand new stock",
                @"SELECT CAST(cm.CartridgeModelId AS BIGINT) Id,cm.ModelNumber Label,cm.ModelNumber Model,cm.Brand Brand,'Cartridge' Category,
                cm.CreatedAt CreatedAt,'Active' Status,stock.BrandNewUnits,stock.RefilledUnits,stock.BrandNewUnits+stock.RefilledUnits Units
                FROM dbo.CartridgeModel cm OUTER APPLY (SELECT
                ISNULL(SUM(CASE WHEN i.RefillStatus IS NULL THEN CAST(ISNULL(i.StockOnHand,0) AS BIGINT) ELSE 0 END),0) BrandNewUnits,
                ISNULL(SUM(CASE WHEN i.RefillStatus='Available' THEN CAST(ISNULL(i.StockOnHand,0) AS BIGINT) ELSE 0 END),0) RefilledUnits
                FROM dbo.Item i WHERE i.CartridgeModelId=cm.CartridgeModelId AND i.Active=1 AND i.Category='Cartridge'
                AND ISNULL(i.Remarks,'') NOT LIKE '%IT custody%'
                AND NOT EXISTS(SELECT 1 FROM dbo.ArchiveStatus ar WHERE ar.EntityType='Item' AND ar.EntityId=i.ItemId AND ar.IsArchived=1)) stock WHERE cm.IsActive=1",
                "Id|Label|Model|Brand|Category|CreatedAt|Status|BrandNewUnits|RefilledUnits|Units","Label|Brand","Brand|Status","Units|BrandNewUnits|RefilledUnits","","CreatedAt");c.CategoryScope=true;
            c=Add(list,"refills","Refill and vendor batches","Consumables","CartridgeManagement","",
                "refill batch|refill batches|vendor batch|refill|refills|batch",
                @"SELECT CAST(vb.BatchId AS BIGINT) Id,CONCAT('Batch ',vb.BatchId) Label,vb.Status Status,v.VendorName Vendor,vb.BatchPurpose Purpose,
                vb.CreatedDate CreatedAt,vb.OriginalQty SentUnits,vb.ReturnedQty ReturnedUnits,cm.ModelNumber Model,'Cartridge' Category
                FROM dbo.VendorCartridgeBatch vb INNER JOIN dbo.Vendor v ON v.VendorID=vb.VendorId LEFT JOIN dbo.CartridgeModel cm ON cm.CartridgeModelId=vb.CartridgeModelId",
                "Id|Label|Status|Vendor|Purpose|CreatedAt|SentUnits|ReturnedUnits|Model|Category","Label|Vendor|Model","Status|Vendor|Purpose","SentUnits|ReturnedUnits","","CreatedAt");c.CategoryScope=true;
            c=Add(list,"exchanges","Cartridge exchanges","Consumables","CartridgeManagement","",
                "exchange|exchanges|unfulfilled cartridge",
                @"SELECT CAST(x.UnfulfilledId AS BIGINT) Id,CONCAT('Exchange ',x.UnfulfilledId) Label,x.CartridgeModel Model,x.Status Status,
                x.CreatedDate CreatedAt,x.RequestedQty RequestedUnits,x.IssuedFullQty IssuedUnits,x.UnfulfilledQty OutstandingUnits,
                e.Name Employee,x.EmpId EmployeeId,b.Name Branch,d.Name Department,'Cartridge' Category
                FROM dbo.UnfulfilledCartridgeExchange x LEFT JOIN dbo.Employee e ON e.EmpId=x.EmpId LEFT JOIN dbo.Branch b ON b.BranchId=x.BranchId
                LEFT JOIN dbo.Department d ON d.DeptId=x.DeptId",
                "Id|Label|Model|Status|CreatedAt|RequestedUnits|IssuedUnits|OutstandingUnits|Employee|EmployeeId|Branch|Department|Category",
                "Label|Model|Employee","Status|Employee|Branch|Department","RequestedUnits|IssuedUnits|OutstandingUnits","","CreatedAt");c.CategoryScope=true;c.PersonalPredicate="d.EmployeeId=@EmployeeId";
            c=Add(list,"approvals","Request authorizations","Requests","RequesterPortal|CartridgeManagement","",
                "approval|approvals|authorization|authorizations",
                @"SELECT CAST(a.AuthorizationId AS BIGINT) Id,CONCAT('Authorization ',a.AuthorizationId) Label,a.Status Status,a.CreatedDate CreatedAt,
                a.EmployeeId EmployeeId,e.Name Employee,d.Name Department,a.SubmittedByUserId CreatedBy,a.AssignedToUserId AssignedUserId,
                LEFT(a.RequestedModels,200) Models FROM dbo.CartridgeAuthorization a LEFT JOIN dbo.Employee e ON e.EmpId=a.EmployeeId
                LEFT JOIN dbo.Department d ON d.DeptId=a.DepartmentId",
                "Id|Label|Status|CreatedAt|EmployeeId|Employee|Department|CreatedBy|AssignedUserId|Models","Label|Employee|Models","Status|Employee|Department","","","CreatedAt");
            c.PersonalPredicate="(d.EmployeeId=@EmployeeId OR d.CreatedBy=@UserId OR d.AssignedUserId=@UserId)";
            c.ForcedScopePredicate="(d.EmployeeId=@EmployeeId OR d.CreatedBy=@UserId OR d.AssignedUserId=@UserId)";

            c=Add(list,"tickets","IT tickets","Tickets","CallITMonitoring","",
                "ticket|tickets|helpdesk|help desk|call monitoring|it call",
                @"SELECT CAST(t.TicketId AS BIGINT) Id,t.TicketCode Label,t.Status Status,t.Priority Priority,t.CallerName Caller,
                LEFT(t.Issue,500) Issue,LEFT(t.ProvidedSolution,500) Solution,t.CreatedAt CreatedAt,t.UpdatedAt UpdatedAt,t.SolvedAt SolvedAt,
                t.CreatedByUserId CreatedBy,t.AssignedToUserId AssignedUserId,t.AssignedToEmpId EmployeeId,e.Name Employee,
                d.Name Department,b.Name Branch,co.Name Company FROM dbo.CallTicket t LEFT JOIN dbo.Employee e ON e.EmpId=t.AssignedToEmpId
                LEFT JOIN dbo.Department d ON d.DeptId=t.DeptId LEFT JOIN dbo.Branch b ON b.BranchId=t.BranchId LEFT JOIN dbo.Company co ON co.ComId=t.ComId",
                "Id|Label|Status|Priority|Caller|Issue|Solution|CreatedAt|UpdatedAt|SolvedAt|CreatedBy|AssignedUserId|EmployeeId|Employee|Department|Branch|Company",
                "Label|Caller|Issue|Employee","Status|Priority|Employee|Department|Branch|Company");
            c.PersonalPredicate="(d.CreatedBy=@UserId OR d.AssignedUserId=@UserId OR d.EmployeeId=@EmployeeId)";c.HistoryCapability="ticket-history";
            c=Add(list,"ticket-history","Ticket history","Tickets","CallITMonitoring","",
                "ticket history|ticket activity|ticket changes",
                @"SELECT h.HistoryId Id,t.TicketCode Label,CAST(h.TicketId AS BIGINT) TicketId,h.FieldName Field,h.OldValue OldValue,
                h.NewValue NewValue,h.ChangedAt CreatedAt,u.Name ChangedBy,t.Status Status,t.CreatedByUserId CreatedBy,
                t.AssignedToUserId AssignedUserId,t.AssignedToEmpId EmployeeId FROM dbo.CallTicketHistory h
                INNER JOIN dbo.CallTicket t ON t.TicketId=h.TicketId LEFT JOIN dbo.[User] u ON u.UserId=h.ChangedByUserId",
                "Id|Label|TicketId|Field|OldValue|NewValue|CreatedAt|ChangedBy|Status|CreatedBy|AssignedUserId|EmployeeId","Label|Field|NewValue|ChangedBy","Field|NewValue");
            c.ParentColumn="TicketId";c.PersonalPredicate="(d.CreatedBy=@UserId OR d.AssignedUserId=@UserId OR d.EmployeeId=@EmployeeId)";

            c=Add(list,"borrows","Borrowed equipment","Borrowing","BorrowItems","",
                "borrow|borrows|borrowed|loans|loan|return history|borrow aging|loan aging",
                @"SELECT CAST(b.BorrowId AS BIGINT) Id,b.ItemName Label,b.ModelNumber Model,b.SerialNumber Serial,b.BorrowedByEmpName Employee,
                b.BorrowedByEmpId EmployeeId,b.BorrowedByDeptName Department,b.BorrowedAtUtc CreatedAt,b.ReturnedAtUtc ReturnedAt,
                DATEDIFF_BIG(second,b.BorrowedAtUtc,COALESCE(b.ReturnedAtUtc,SYSUTCDATETIME()))/86400 DurationDays,
                CASE WHEN b.ReturnedAtUtc IS NULL THEN 'Open' ELSE 'Returned' END Status FROM dbo.BorrowLog b",
                "Id|Label|Model|Serial|Employee|EmployeeId|Department|CreatedAt|ReturnedAt|DurationDays|Status","Label|Model|Serial|Employee","Status|Employee|Department","","CreatedAt|ReturnedAt");c.PersonalPredicate="d.EmployeeId=@EmployeeId";
            c=Add(list,"repairs","Repair tickets","Repairs","RepairTechnicianPortal","",
                "repair|repairs|repair ticket|technician|service history",
                @"SELECT CAST(r.RepairTicketId AS BIGINT) Id,r.TicketCode Label,r.ItemNameSnapshot Item,r.ItemSerialSnapshot Serial,
                r.Status Status,r.Priority Priority,r.CreatedAt CreatedAt,r.CompletedAt CompletedAt,r.AssignedTechEmpId EmployeeId,e.Name Employee,
                r.SubmittedByUserId CreatedBy,LEFT(r.Problem,300) Problem,LEFT(r.Diagnosis,300) Diagnosis,LEFT(r.Resolution,300) Resolution,
                d.Name Department,b.Name Branch,co.Name Company FROM dbo.RepairTicket r LEFT JOIN dbo.Employee e ON e.EmpId=r.AssignedTechEmpId
                LEFT JOIN dbo.Department d ON d.DeptId=r.DeptId LEFT JOIN dbo.Branch b ON b.BranchId=r.BranchId LEFT JOIN dbo.Company co ON co.ComId=r.ComId",
                "Id|Label|Item|Serial|Status|Priority|CreatedAt|CompletedAt|EmployeeId|Employee|CreatedBy|Problem|Diagnosis|Resolution|Department|Branch|Company",
                "Label|Item|Serial|Employee","Status|Priority|Employee|Department|Branch|Company","","CreatedAt|CompletedAt");
            c.PersonalPredicate="(d.EmployeeId=@EmployeeId OR d.CreatedBy=@UserId)";c.HistoryCapability="repair-history";
            c=Add(list,"repair-history","Repair history","Repairs","RepairTechnicianPortal","","repair history|repair changes",
                @"SELECT h.HistoryId Id,r.TicketCode Label,CAST(h.RepairTicketId AS BIGINT) RepairTicketId,h.FieldName Field,h.OldValue OldValue,
                h.NewValue NewValue,h.ChangedAt CreatedAt,u.Name ChangedBy,r.Status Status,r.AssignedTechEmpId EmployeeId,r.SubmittedByUserId CreatedBy
                FROM dbo.RepairTicketHistory h INNER JOIN dbo.RepairTicket r ON r.RepairTicketId=h.RepairTicketId LEFT JOIN dbo.[User] u ON u.UserId=h.ChangedByUserId",
                "Id|Label|RepairTicketId|Field|OldValue|NewValue|CreatedAt|ChangedBy|Status|EmployeeId|CreatedBy","Label|Field|NewValue|ChangedBy","Field|NewValue");
            c.ParentColumn="RepairTicketId";c.PersonalPredicate="(d.EmployeeId=@EmployeeId OR d.CreatedBy=@UserId)";
            c=Add(list,"repair-parts","Repair parts","Repairs","RepairTechnicianPortal","","repair part|repair parts|parts",
                @"SELECT CAST(p.RepairPartId AS BIGINT) Id,p.PartDisplayName Label,CAST(p.RepairTicketId AS BIGINT) RepairTicketId,
                r.TicketCode Ticket,p.Status Status,p.Severity Priority,p.CreatedAt CreatedAt,r.AssignedTechEmpId EmployeeId,r.SubmittedByUserId CreatedBy
                FROM dbo.RepairPart p INNER JOIN dbo.RepairTicket r ON r.RepairTicketId=p.RepairTicketId",
                "Id|Label|RepairTicketId|Ticket|Status|Priority|CreatedAt|EmployeeId|CreatedBy","Label|Ticket","Status|Priority");
            c.ParentColumn="RepairTicketId";c.PersonalPredicate="(d.EmployeeId=@EmployeeId OR d.CreatedBy=@UserId)";

            c=Add(list,"invoices","Invoices","Reports","InventorySystem|Reports","ViewInvoicePage",
                "invoice|invoices|financial|amount due|vat|wht|discount|purchase order|po number",
                @"SELECT CAST(v.SetId AS BIGINT) Id,v.SetCode Label,v.InvoiceNumber InvoiceNumber,v.PONumber PurchaseOrder,v.Status Status,
                v.InvoiceDate CreatedAt,v.StartDate StartDate,v.EndDate EndDate,v.CompanyName Company,v.Site Site,
                v.Subtotal Subtotal,v.VatAmount Vat,v.WhtAmount Withholding,v.DiscountAmount Discount,v.TotalAmountDue AmountDue
                FROM dbo.vw_Invoices v INNER JOIN dbo.[Set] s ON s.SetId=v.SetId
                WHERE s.Active=1 AND NOT EXISTS(SELECT 1 FROM dbo.ArchiveStatus ar WHERE ar.EntityType='Set' AND ar.EntityId=s.SetId AND ar.IsArchived=1)",
                "Id|Label|InvoiceNumber|PurchaseOrder|Status|CreatedAt|StartDate|EndDate|Company|Site|Subtotal|Vat|Withholding|Discount|AmountDue",
                "Label|InvoiceNumber|PurchaseOrder|Company|Site","Status|Company|Site","Subtotal|Vat|Withholding|Discount|AmountDue","","CreatedAt");c.HistoryCapability="invoice-items";
            c=Add(list,"invoice-items","Invoice line items","Reports","InventorySystem|Reports","ViewInvoicePage",
                "invoice item|invoice items|invoice lines|parent tag|sub type group",
                @"SELECT CAST(si.SetItemId AS BIGINT) Id,s.SetCode Label,CAST(si.SetId AS BIGINT) SetId,i.Name Item,i.Category Category,
                si.SubType SubType,pt.Label ParentTag,si.ReferenceCode ReferenceCode,si.Quantity Units,si.UnitPrice UnitPrice,si.Amount Amount,
                si.CreatedAt CreatedAt,si.BeginDate StartDate,si.EndDate EndDate,si.RenewalStatus Status
                FROM dbo.SetItem si INNER JOIN dbo.[Set] s ON s.SetId=si.SetId INNER JOIN dbo.Item i ON i.ItemId=si.ItemId
                LEFT JOIN dbo.SetItemParentTagGroup pt ON pt.ParentTagGroupId=si.ParentTagGroupId WHERE s.IsInvoice=1 AND s.Active=1
                AND NOT EXISTS(SELECT 1 FROM dbo.ArchiveStatus ar WHERE ar.EntityType='Set' AND ar.EntityId=s.SetId AND ar.IsArchived=1)",
                "Id|Label|SetId|Item|Category|SubType|ParentTag|ReferenceCode|Units|UnitPrice|Amount|CreatedAt|StartDate|EndDate|Status",
                "Label|Item|ReferenceCode|ParentTag","SubType|ParentTag|Category|Status","Units|Amount");c.ParentColumn="SetId";c.CategoryScope=true;
            c=Add(list,"renewals","Renewals","Reports","InventorySystem|Reports","ViewRenewalPage",
                "renewal|renewals|expire|expiry|expiration|expiring|subscription|license expiration",
                @"SELECT CAST(r.RenewalId AS BIGINT) Id,COALESCE(i.Name,CONCAT('Renewal ',r.RenewalId)) Label,r.PartNumber PartNumber,
                i.Category Category,r.RenewalStatus Status,r.CreatedAt CreatedAt,r.NewStartDate StartDate,r.NewEndDate EndDate,
                r.RenewalAmount Amount,CAST(r.IsArchived AS BIT) Archived FROM dbo.Renewals r LEFT JOIN dbo.Item i ON i.ItemId=r.ItemId",
                "Id|Label|PartNumber|Category|Status|CreatedAt|StartDate|EndDate|Amount|Archived","Label|PartNumber","Category|Status","Amount","","EndDate");c.ArchiveColumn="Archived";c.CategoryScope=true;
            Add(list,"receipts","Receipts","Reports","InventorySystem|Reports","ViewReceiptsPage","receipt|receipts|si number|dr number",
                @"SELECT CAST(r.ReceiptSetId AS BIGINT) Id,CONCAT('Receipt ',r.ReceiptSetId) Label,r.SetId SetId,r.Supplier Vendor,
                r.SiNumber InvoiceNumber,r.DrNumber DeliveryNumber,r.PoNumber PurchaseOrder,r.CreatedAt CreatedAt FROM dbo.ReceiptSet r",
                "Id|Label|SetId|Vendor|InvoiceNumber|DeliveryNumber|PurchaseOrder|CreatedAt","Label|Vendor|InvoiceNumber|DeliveryNumber|PurchaseOrder","Vendor","","","CreatedAt");
            Add(list,"archive","Archived records","Inventory","InventorySystem","ViewArchivePage","archive|archived|archives",
                @"SELECT CAST(ArchiveId AS BIGINT) Id,CONCAT(EntityType,' #',EntityId) Label,EntityType EntityType,EntityId EntityId,
                ArchiveReason Reason,ArchivedBy ArchivedBy,ArchivedAt CreatedAt,'Archived' Status FROM dbo.ArchiveStatus WHERE IsArchived=1",
                "Id|Label|EntityType|EntityId|Reason|ArchivedBy|CreatedAt|Status","Label|Reason|ArchivedBy","EntityType","","","CreatedAt");
            c=Add(list,"accounts","User accounts","Admin","AdminPortal","UserAccountManagementPage","account|accounts|user|users|login account",
                @"SELECT CAST(u.UserId AS BIGINT) Id,u.Name Label,e.Name Employee,u.DateCreated CreatedAt,
                CASE WHEN u.IsActive=1 THEN 'Active' ELSE 'Inactive' END Status,
                CASE WHEN u.IsDeveloper=1 THEN 'Developer' WHEN u.IsSuperAdmin=1 THEN 'Super Admin' ELSE 'Standard' END AccessLevel
                FROM dbo.[User] u LEFT JOIN dbo.Employee e ON e.EmpId=u.EmpId",
                "Id|Label|Employee|CreatedAt|Status|AccessLevel","Label|Employee","Status|AccessLevel");c.AdminOnly=true;
            c=Add(list,"roles","Role memberships","Admin","AdminPortal|InventorySystem","","role|roles|role membership|role memberships",
                @"SELECT CAST(ur.UserId AS BIGINT)*100000+CAST(ur.RoleId AS BIGINT) Id,u.Name Label,r.RoleName Role,r.Description Description,
                ur.DateAssigned CreatedAt,CASE WHEN r.IsActive=1 THEN 'Active' ELSE 'Inactive' END Status
                FROM dbo.UserRole ur INNER JOIN dbo.[User] u ON u.UserId=ur.UserId INNER JOIN dbo.Role r ON r.RoleId=ur.RoleId",
                "Id|Label|Role|Description|CreatedAt|Status","Label|Role","Role|Status","","","CreatedAt");c.AdminOnly=true;
            c=Add(list,"audit","User activity","Admin","AdminPortal|InventorySystem","","audit|activity log|user activity|audit trail",
                @"SELECT CAST(a.ActivityId AS BIGINT) Id,u.Name Label,a.ActionType Action,a.EntityType EntityType,a.EntityId EntityId,
                LEFT(a.Description,300) Description,a.CreatedDate CreatedAt FROM dbo.UserActivityLog a INNER JOIN dbo.[User] u ON u.UserId=a.UserId",
                "Id|Label|Action|EntityType|EntityId|Description|CreatedAt","Label|Action|EntityType","Action|EntityType");c.AdminOnly=true;
            c=Add(list,"policies","Published company policies","Workspace","InventorySystem|RequesterPortal|CallITMonitoring|BorrowItems|CartridgeManagement|RepairTechnicianPortal|Reports|AdminPortal","",
                "policy|policies|guideline|handbook|memo|rules",
                @"SELECT CAST(PolicyId AS BIGINT) Id,Title Label,Category Category,Summary Summary,LEFT(Body,1600) Excerpt,
                EffectiveDate EffectiveDate,CreatedAt CreatedAt FROM dbo.CompanyPolicy WHERE IsPublished=1",
                "Id|Label|Category|Summary|Excerpt|EffectiveDate|CreatedAt","Label|Category|Summary|Excerpt","Category");
            c=Add(list,"item-history","Item movement audit","Inventory","InventorySystem","ViewItemMovementAuditPage",
                "item history|movement audit|item audit",
                @"SELECT CAST(a.Id AS BIGINT) Id,COALESCE(i.Name,a.SerialNumber,CONCAT('Item ',a.ItemId)) Label,a.ItemId ItemId,
                a.SerialNumber Serial,i.Category Category,a.Action Action,a.ActionTime CreatedAt,a.EmployeeId EmployeeId,a.EmployeeName Employee,
                a.DepartmentName Department,a.BranchName Branch,a.Direction Direction,a.Status Status,a.SetCode SetCode
                FROM dbo.ItemAuditTrail a LEFT JOIN dbo.Item i ON i.ItemId=a.ItemId",
                "Id|Label|ItemId|Serial|Category|Action|CreatedAt|EmployeeId|Employee|Department|Branch|Direction|Status|SetCode",
                "Label|Serial|Employee|SetCode","Action|Status|Employee|Department|Branch|Category","","","CreatedAt");c.CategoryScope=true;c.PersonalPredicate="d.EmployeeId=@EmployeeId";c.ParentColumn="ItemId";
            c=Add(list,"ownership-history","Ownership history","Inventory","InventorySystem","ViewOwnershipHistoryPage",
                "ownership history|ownership transfer|ownership changes|site history",
                @"SELECT CAST(a.Id AS BIGINT) Id,CONCAT('SET-',a.EntityId) Label,CAST(a.EntityId AS BIGINT) SetId,a.Timestamp CreatedAt,a.UserName ChangedBy,
                JSON_VALUE(CASE WHEN ISJSON(a.OldValues)=1 THEN a.OldValues ELSE '{}' END,'$.EmployeeName') PreviousEmployee,
                JSON_VALUE(CASE WHEN ISJSON(a.NewValues)=1 THEN a.NewValues ELSE '{}' END,'$.EmployeeName') Employee,
                JSON_VALUE(CASE WHEN ISJSON(a.NewValues)=1 THEN a.NewValues ELSE '{}' END,'$.DepartmentName') Department,
                JSON_VALUE(CASE WHEN ISJSON(a.NewValues)=1 THEN a.NewValues ELSE '{}' END,'$.BranchName') Branch,a.Action Status
                FROM dbo.AuditTrail a WHERE a.EntityType='Set' AND a.Action='OwnershipTransfer'",
                "Id|Label|SetId|CreatedAt|ChangedBy|PreviousEmployee|Employee|Department|Branch|Status","Label|Employee|PreviousEmployee|ChangedBy","Employee|Department|Branch","","","CreatedAt");c.ParentColumn="SetId";
            c=Add(list,"set-updates","Set item updates","Inventory","InventorySystem","ViewUpdatesPage","set item updates|set updates|item updates|set update",
                @"SELECT CAST(u.UpdateId AS BIGINT) Id,u.SetCode Label,CAST(u.SetId AS BIGINT) SetId,u.SerialNumber Serial,u.ModelNumber Model,
                u.PreviousStatus PreviousStatus,u.NewStatus Status,u.CreatedAt CreatedAt,u.UpdatedByName ChangedBy,
                CASE WHEN u.Processed=1 THEN 'Processed' ELSE 'Unprocessed' END Processing FROM dbo.SetItemUpdate u",
                "Id|Label|SetId|Serial|Model|PreviousStatus|Status|CreatedAt|ChangedBy|Processing","Label|Serial|Model|ChangedBy","Status|Processing");c.ParentColumn="SetId";
            c=Add(list,"item-repairs","Item repair history","Inventory","InventorySystem","ViewRepairedItemsPage","repaired items|item repair history",
                @"SELECT CAST(h.RepairId AS BIGINT) Id,COALESCE(i.Name,h.SerialNumber) Label,h.ItemId ItemId,h.SerialNumber Serial,
                i.Category Category,h.NewStatus Status,h.ConditionName Condition,h.RepairAction Action,h.CreatedAt CreatedAt,h.ProcessedByName ChangedBy
                FROM dbo.ItemRepairHistory h LEFT JOIN dbo.Item i ON i.ItemId=h.ItemId",
                "Id|Label|ItemId|Serial|Category|Status|Condition|Action|CreatedAt|ChangedBy","Label|Serial|ChangedBy","Status|Condition|Category","","","CreatedAt");c.CategoryScope=true;c.ParentColumn="ItemId";
            Add(list,"categories","Item categories","Inventory","InventorySystem","ViewCategoriesPage","category|categories|item categories",
                "SELECT CAST(CategoryId AS BIGINT) Id,Name Label,Description Description,DateCreated CreatedAt,CASE WHEN Active=1 THEN 'Active' ELSE 'Inactive' END Status FROM dbo.ItemCategory",
                "Id|Label|Description|CreatedAt|Status","Label|Description","Status","","","CreatedAt");
            Add(list,"conditions","Item conditions","Inventory","InventorySystem","ViewItemsPage","item conditions|condition catalog",
                "SELECT CAST(ConditionID AS BIGINT) Id,ConditionName Label FROM dbo.Condition","Id|Label","Label","","","",null);
            Add(list,"invoice-preparation","Invoice Sub Groups","Reports","InventorySystem|Reports","ViewInvoicePreparationPage","invoice sub groups|invoice preparation|preparation groups",
                @"SELECT CAST(p.PreparationId AS BIGINT) Id,COALESCE(p.ReferenceCode,CONCAT('Preparation ',p.PreparationId)) Label,
                p.SubType SubType,p.Status Status,p.CreatedAt CreatedAt,p.BeginDate StartDate,p.EndDate EndDate,p.GeneratedSetId SetId
                FROM dbo.InvoicePreparation p",
                "Id|Label|SubType|Status|CreatedAt|StartDate|EndDate|SetId","Label","SubType|Status");
            foreach(bool confirmed in new[] {false,true})
            {
                c=Add(list,confirmed?"nonlicensed":"license-review",confirmed?"Non-Licensed Invoices":"Invoice License Review","Reports","InventorySystem|Reports",
                    confirmed?"NonLicensedInvoicesPage":"InvoiceLicenseReviewPage",confirmed?"non licensed|nonlicensed|confirmed non licensed":"license review|pending license review",
                    @"SELECT ROW_NUMBER() OVER(ORDER BY v.SetId,v.ItemId,v.ItemCode) Id,v.InvoiceNumber Label,CAST(v.SetId AS BIGINT) SetId,
                    v.ItemName Item,v.ModelNumber Model,v.SerialNumber Serial,v.CategoryName Category,v.CompanyName Company,v.Department Department,v.Employee Employee,
                    v.CreatedAt CreatedAt,v.Quantity Units,v.UnitPrice UnitPrice,v.LineTotal Amount,"+(confirmed?"'NonLicensed'":"'Pending review'")+@" Status
                    FROM dbo."+(confirmed?"vw_ConfirmedNonLicensedInvoiceItems":"vw_InvoiceNonLicensedItems")+" v",
                    "Id|Label|SetId|Item|Model|Serial|Category|Company|Department|Employee|CreatedAt|Units|UnitPrice|Amount|Status",
                    "Label|Item|Model|Serial|Employee","Company|Department|Category|Status","Units|Amount");c.ParentColumn="SetId";c.CategoryScope=true;
            }
            Add(list,"ticket-email","Ticket email logs","Tickets","CallITMonitoring","","email logs|email log|ticket email|sent emails|failed emails",
                @"SELECT l.EmailLogId Id,COALESCE(t.TicketCode,CONCAT('Email ',l.EmailLogId)) Label,l.TicketId TicketId,
                l.EmailType EmailType,l.Status Status,l.DateSent CreatedAt FROM dbo.CallEmailLog l LEFT JOIN dbo.CallTicket t ON t.TicketId=l.TicketId",
                "Id|Label|TicketId|EmailType|Status|CreatedAt","Label|EmailType","Status|EmailType").ParentColumn="TicketId";
            c=Add(list,"department-accounts","Department Accounts","Admin","AdminPortal|InventorySystem","DepartmentAccountsPage","department accounts|department account",
                @"SELECT CAST(Id AS BIGINT) Id,Username Label,CompanyName Company,DepartmentName Department,BranchName Branch,
                DateCreated CreatedAt,CASE WHEN IsActive=1 THEN 'Active' ELSE 'Inactive' END Status FROM dbo.DepartmentAccount",
                "Id|Label|Company|Department|Branch|CreatedAt|Status","Label|Company|Department|Branch","Status|Company|Department|Branch","","","CreatedAt");c.AdminOnly=true;
            c=Add(list,"holidays","Holidays","Admin","AdminPortal","HolidayManagementPage","holiday|holidays|calendar holidays",
                @"SELECT CAST(HolidayId AS BIGINT) Id,HolidayName Label,HolidayDate StartDate,HolidayType HolidayType,
                CASE WHEN IsRecurring=1 THEN 'Recurring rule' ELSE 'Single date' END Recurrence,CreatedDate CreatedAt,
                CASE WHEN IsActive=1 THEN 'Active' ELSE 'Inactive' END Status FROM dbo.CompanyHoliday",
                "Id|Label|StartDate|HolidayType|Recurrence|CreatedAt|Status","Label|HolidayType","Status|HolidayType","","","StartDate");c.AdminOnly=true;
            c=Add(list,"permissions","Individual permission overrides","Admin","AdminPortal","UserPortalAccessPage","individual permission overrides|permission overrides|permission|permissions|page access|category access",
                @"SELECT CAST(p.UserId AS BIGINT)*100000+CAST(p.PermissionItemId AS BIGINT) Id,u.Name Label,pi.PermissionType PermissionType,
                pi.ItemKey ItemKey,pi.DisplayName Permission,CASE WHEN p.IsGranted=1 THEN 'Granted' ELSE 'Denied' END Status,p.DateModified CreatedAt
                FROM dbo.UserPermissionItem p INNER JOIN dbo.[User] u ON u.UserId=p.UserId
                INNER JOIN dbo.PermissionItem pi ON pi.PermissionItemId=p.PermissionItemId WHERE pi.IsActive=1",
                "Id|Label|PermissionType|ItemKey|Permission|Status|CreatedAt","Label|ItemKey|Permission","PermissionType|Status","","","CreatedAt");c.AdminOnly=true;c.DeveloperOnly=true;
            c=Add(list,"system-email","System email logs","Admin","AdminPortal","EmailLogsPage","system email|system email logs|delivery logs|admin email logs",
                @"SELECT CAST(l.LogId AS BIGINT) Id,COALESCE(l.TemplateKey,CONCAT('Email ',l.LogId)) Label,l.TemplateKey Template,
                l.Status Status,l.EntityType EntityType,l.EntityId EntityId,l.SentDate CreatedAt,u.Name SentBy
                FROM dbo.SystemEmailLog l LEFT JOIN dbo.[User] u ON u.UserId=l.SentByUserId",
                "Id|Label|Template|Status|EntityType|EntityId|CreatedAt|SentBy","Label|Template|SentBy","Status|Template|EntityType");c.AdminOnly=true;
            c=Add(list,"user-positions","User position summary","Admin","AdminPortal","UserPositionsPage","user positions|position summary|employee positions",
                @"SELECT ROW_NUMBER() OVER(ORDER BY v.Position,v.CompanyName,v.BranchName) Id,v.Position Label,
                v.CompanyName Company,v.BranchName Branch,v.EmployeeCount Employees FROM dbo.vw_UserPositionSummary v",
                "Id|Label|Company|Branch|Employees","Label|Company|Branch","Company|Branch","Employees","",null);c.AdminOnly=true;
            c=Add(list,"approver-titles","Approver titles","Admin","AdminPortal","AccountPermissionsPage","approver titles|approver management|approval roles",
                @"SELECT CAST(TitleId AS BIGINT) Id,PositionTitle Label,ApprovalRole Role,RolePriority RolePriority,DateCreated CreatedAt,
                CASE WHEN IsActive=1 THEN 'Active' ELSE 'Inactive' END Status FROM dbo.ApprovalRoleTitle",
                "Id|Label|Role|RolePriority|CreatedAt|Status","Label|Role","Role|Status");c.AdminOnly=true;
            foreach(bool branches in new[] {true,false})
            {
                c=Add(list,branches?"branch-acronyms":"department-acronyms",branches?"Branch acronyms":"Department acronyms","Admin","AdminPortal",
                    branches?"BranchAcronymPage":"DepartmentAcronymPage",branches?"branch acronyms|branch acronym":"department acronyms|department acronym",
                    "SELECT CAST("+(branches?"BranchId":"DeptId")+" AS BIGINT) Id,Name Label,Acronym Acronym,CASE WHEN Active=1 THEN 'Active' ELSE 'Inactive' END Status FROM dbo."+(branches?"Branch":"Department"),
                    "Id|Label|Acronym|Status","Label|Acronym","Status","","",null);c.AdminOnly=true;
            }
            c=Add(list,"role-portals","Role portal grants","Admin","AdminPortal","PortalAccessPage","role portal grants|role portal access|portal access",
                @"SELECT CAST(rpa.RoleId AS BIGINT)*100000+rpa.PortalId Id,r.RoleName Label,p.PortalKey Portal,'Granted' Status
                FROM dbo.RolePortalAccess rpa INNER JOIN dbo.Role r ON r.RoleId=rpa.RoleId INNER JOIN dbo.Portal p ON p.PortalId=rpa.PortalId WHERE r.IsActive=1 AND p.IsActive=1",
                "Id|Label|Portal|Status","Label|Portal","Portal|Status","","",null);c.AdminOnly=true;c.DeveloperOnly=true;
            c=Add(list,"user-portals","Individual portal overrides","Admin","AdminPortal","UserPortalAccessPage","individual portal overrides|user portal overrides|user portal access|individual portal access",
                @"SELECT CAST(upa.UserId AS BIGINT)*100000+upa.PortalId Id,u.Name Label,p.PortalKey Portal,CASE WHEN upa.IsGranted=1 THEN 'Granted' ELSE 'Denied' END Status
                FROM dbo.UserPortalAccess upa INNER JOIN dbo.[User] u ON u.UserId=upa.UserId INNER JOIN dbo.Portal p ON p.PortalId=upa.PortalId WHERE p.IsActive=1",
                "Id|Label|Portal|Status","Label|Portal","Portal|Status","","",null);c.AdminOnly=true;c.DeveloperOnly=true;
            c=Add(list,"role-permissions","Role permission overrides","Admin","AdminPortal","UserPortalAccessPage","role permission overrides|role permissions|role page access|role category access",
                @"SELECT CAST(rp.RoleId AS BIGINT)*100000+rp.PermissionItemId Id,r.RoleName Label,pi.PermissionType PermissionType,pi.ItemKey ItemKey,
                pi.DisplayName Permission,CASE WHEN rp.IsGranted=1 THEN 'Granted' ELSE 'Denied' END Status,rp.DateModified CreatedAt
                FROM dbo.RolePermissionItem rp INNER JOIN dbo.Role r ON r.RoleId=rp.RoleId INNER JOIN dbo.PermissionItem pi ON pi.PermissionItemId=rp.PermissionItemId WHERE pi.IsActive=1",
                "Id|Label|PermissionType|ItemKey|Permission|Status|CreatedAt","Label|ItemKey|Permission","PermissionType|Status","","","CreatedAt");c.AdminOnly=true;c.DeveloperOnly=true;
            list.First(x=>x.Id=="accounts").SuperAdminOnly=true;
            list.First(x=>x.Id=="license-review").SuperAdminOnly=true;
            list.First(x=>x.Id=="nonlicensed").SuperAdminOnly=true;
            c=list.First(x=>x.Id=="employees");c.AlternatePortal="AdminPortal";c.AlternatePage="EmployeeManagementPage";
            c=list.First(x=>x.Id=="requests");c.AlternatePortal="AdminPortal";c.AlternatePage="DepartmentRequestHistoryPage";
            c=list.First(x=>x.Id=="consumables");c.AlternatePortal="AdminPortal";c.AlternatePage="ConsumableStockMonitorPage";
            foreach(var master in list.Where(x=>new[] { "items","assets","employees","companies","branches","departments","vendors","categories","department-accounts","holidays" }.Contains(x.Id))) master.DefaultStatus="Active";
            return list;
        }
    }
}
