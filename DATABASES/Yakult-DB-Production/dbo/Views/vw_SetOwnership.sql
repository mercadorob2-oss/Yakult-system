CREATE VIEW vw_SetOwnership AS
SELECT 
    s.SetId,
    s.SetCode,
    s.SetType,
    -- Original Request Info
    origBranch.Name AS OriginalBranch,
    origDept.Name AS OriginalDepartment,
    origEmp.Name AS OriginalRequestor,
    -- Current Ownership
    currBranch.Name AS CurrentBranch,
    currDept.Name AS CurrentDepartment,
    -- Transfer Status
    CASE 
        WHEN s.CurrentBranchId = origBranch.BranchId THEN 'Original Location'
        ELSE 'Transferred'
    END AS OwnershipStatus
FROM dbo.[Set] s
-- Original ownership (via Request → Employee)
LEFT JOIN dbo.Request r ON s.ReqId = r.ReqId
LEFT JOIN dbo.Employee origEmp ON r.EmpId = origEmp.EmpId
LEFT JOIN dbo.Branch origBranch ON origEmp.BranchId = origBranch.BranchId
LEFT JOIN dbo.Department origDept ON origEmp.DeptId = origDept.DeptId
-- Current ownership (direct columns)
LEFT JOIN dbo.Branch currBranch ON s.CurrentBranchId = currBranch.BranchId
LEFT JOIN dbo.Department currDept ON s.CurrentDepartmentId = currDept.DeptId;