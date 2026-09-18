-- Update ItemId 693 (HP LASERJET 76X BLACK) from Brand New to Refilled.
-- RefillStatus: NULL = Brand New, 'Available' = Refilled
UPDATE dbo.Item
SET RefillStatus = 'Available',
    DateModified = GETDATE(),
    ModifiedBy   = 22
WHERE ItemId = 693;
