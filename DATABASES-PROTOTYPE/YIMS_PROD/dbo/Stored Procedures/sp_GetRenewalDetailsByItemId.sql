
CREATE PROCEDURE [dbo].[sp_GetRenewalDetailsByItemId]
    @ItemId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        i.ItemId,
        i.Name AS ItemName,
        i.Description,
        i.ItemType,
        i.SerialNumber,
        i.ModelNumber,
        i.LicenseNumber,
        i.Amount,
        i.StartDate,
        i.EndDate,
        i.DateCreated,
        i.DateModified,
        i.Category,
        i.Remarks AS ItemRemarks,
        i.Active,

        -- Category info
        ic.Name AS CategoryName,

        -- Vendor info
        v.VendorName AS VendorName,
        NULL AS VendorContact,
        NULL AS VendorPhone,
        NULL AS VendorEmail,

        -- Condition info
        cond.ConditionName AS ConditionName,

        -- Calculate days until expiry
        CASE
            WHEN i.EndDate IS NULL THEN NULL
            ELSE DATEDIFF(DAY, GETDATE(), i.EndDate)
        END AS DaysUntilExpiry,

        -- Calculate expiry status
        CASE
            WHEN i.EndDate IS NULL THEN 'No Expiry Date'
            WHEN DATEDIFF(DAY, GETDATE(), i.EndDate) < 0 THEN 'Expired'
            WHEN DATEDIFF(DAY, GETDATE(), i.EndDate) <= 30 THEN 'Expiring Soon'
            WHEN DATEDIFF(DAY, GETDATE(), i.EndDate) <= 90 THEN 'Warning'
            ELSE 'Active'
        END AS ExpiryStatus,

        -- Created by user
        u.Name AS CreatedByUsername,

        -- Current renewal status (latest renewal record)
        (SELECT TOP 1 RenewalStatus
         FROM dbo.Renewals
         WHERE ItemId = i.ItemId
         ORDER BY CreatedAt DESC) AS CurrentRenewalStatus,

        -- Financial Information (from Set table via SetItem)
        ISNULL(s.Subtotal, 0) AS Subtotal,
        ISNULL(s.VatAmount, 0) AS VatAmount,
        ISNULL(s.WhtAmount, 0) AS WhtAmount,
        ISNULL(s.DiscountAmount, 0) AS DiscountAmount,
        ISNULL(s.TotalAmountDue, 0) AS TotalAmountDue,

        -- Site Information (from Set table)
        s.CurrentBranchId,
        ISNULL(b.Name, 'N/A') AS CurrentBranchName,
        s.CurrentDepartmentId,
        ISNULL(d.Name, 'N/A') AS CurrentDepartmentName,
        s.Site AS SiteName

    FROM dbo.Item i
    LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
    LEFT JOIN dbo.Vendor v ON i.VendorId = v.VendorID
    LEFT JOIN dbo.Condition cond ON i.ConditionID = cond.ConditionID
    LEFT JOIN dbo.[User] u ON i.CreatedBy = u.UserId
    -- Join with Set table through SetItem to get financial and site info
    LEFT JOIN dbo.SetItem si ON i.ItemId = si.ItemId
    LEFT JOIN dbo.[Set] s ON si.SetId = s.SetId AND s.SetType IN ('Software/License', 'Service')
    LEFT JOIN dbo.Branch b ON s.CurrentBranchId = b.BranchId
    LEFT JOIN dbo.Department d ON s.CurrentDepartmentId = d.DeptId
    WHERE i.ItemId = @ItemId
        AND i.ItemType IN ('Software/License', 'Service')
END
