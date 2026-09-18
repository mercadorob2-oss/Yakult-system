CREATE VIEW dbo.vw_ConsumableDispatches AS
SELECT 
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DispatchDate,
    s.Status,
    s.Remarks,
    s.CreatedAt,
    s.CreatedBy,
    
    -- Get info from first request in the set
    MIN(e.EmpId) AS RequestorEmpId,
    MIN(e.Name) AS RequestorName,
    MIN(e.Position) AS RequestorPosition,
    MIN(b.Name) AS BranchName,
    MIN(d.Name) AS DepartmentName,
    MIN(c.Name) AS CompanyName,
    
    -- Aggregated item info
    COUNT(r.ReqId) AS ItemCount,
    SUM(r.Quantity) AS TotalQuantity,
    SUM(r.Quantity * r.UnitPrice) AS TotalAmount,
    
    -- Vendor info
    MIN(i.VendorId) AS PrimaryVendorId,
    MIN(v.VendorName) AS PrimaryVendorName,
    
    -- Current ownership
    s.CurrentBranchId,
    s.CurrentDepartmentId,
    currBranch.Name AS CurrentBranchName,
    currDept.Name AS CurrentDepartmentName

FROM dbo.[Set] s
LEFT JOIN dbo.Request r ON s.SetId = r.SetId
LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
LEFT JOIN dbo.Company c ON e.ComId = c.ComId
LEFT JOIN dbo.Item i ON r.ItemId = i.ItemId
LEFT JOIN dbo.Vendor v ON i.VendorId = v.VendorId
LEFT JOIN dbo.Branch currBranch ON s.CurrentBranchId = currBranch.BranchId
LEFT JOIN dbo.Department currDept ON s.CurrentDepartmentId = currDept.DeptId

WHERE s.SetType = 'Dispatched'

GROUP BY 
    s.SetId, s.SetCode, s.SetType, s.DispatchDate, s.Status, 
    s.Remarks, s.CreatedAt, s.CreatedBy,
    s.CurrentBranchId, s.CurrentDepartmentId,
    currBranch.Name, currDept.Name;
