CREATE VIEW dbo.vw_ConsumableDispatchItems AS
SELECT 
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DispatchDate,
    s.Status,
    s.Remarks,
    s.CreatedAt,
    s.CreatedBy,
    
    -- Request/Employee Info
    r.ReqId,
    r.EmpId,
    e.Name AS EmployeeName,
    e.Position,
    
    -- Branch/Department/Company Info
    b.BranchId,
    b.Name AS BranchName,
    d.DeptId,
    d.Name AS DepartmentName,
    c.ComId,
    c.Name AS CompanyName,
    
    -- Item details from Request
    r.ItemId,
    i.Name AS ItemName,
    i.Description AS ItemDescription,
    i.ModelNumber,
    i.SerialNumber,
    ic.Name AS CategoryName,
    i.ItemType,
    r.Quantity,
    r.UnitPrice,
    (r.Quantity * r.UnitPrice) AS LineTotal,
    
    -- Vendor info
    i.VendorId,
    v.VendorName AS VendorName,
    v.TIN AS TIN,
    v.Address AS VendorAddress,
    
    -- Condition info
    i.ConditionId,
    cond.ConditionName,
    
    -- Current ownership (for transfers)
    s.CurrentBranchId,
    s.CurrentDepartmentId,
    currBranch.Name AS CurrentBranchName,
    currDept.Name AS CurrentDepartmentName
    
FROM dbo.[Set] s
INNER JOIN dbo.Request r ON s.SetId = r.SetId
LEFT JOIN dbo.Employee e ON r.EmpId = e.EmpId
LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
LEFT JOIN dbo.Company c ON e.ComId = c.ComId
LEFT JOIN dbo.Item i ON r.ItemId = i.ItemId
LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.Vendor v ON i.VendorId = v.VendorId
LEFT JOIN dbo.Condition cond ON i.ConditionId = cond.ConditionId
LEFT JOIN dbo.Branch currBranch ON s.CurrentBranchId = currBranch.BranchId
LEFT JOIN dbo.Department currDept ON s.CurrentDepartmentId = currDept.DeptId

WHERE s.SetType = 'Dispatched';
