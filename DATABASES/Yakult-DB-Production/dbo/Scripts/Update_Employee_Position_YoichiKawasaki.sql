-- Preview: confirm the target row before updating
SELECT EmpId, Name, Position
FROM dbo.[Employee]
WHERE Name = 'Yoichi Kawasaki';

-- Update position (replace '???' with the correct position value)
UPDATE dbo.[Employee]
SET
    Position     = '???',
    DateModified = SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time'
WHERE Name = 'Yoichi Kawasaki';
