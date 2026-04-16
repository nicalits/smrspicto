-- Clears requisitions (RS) and inventory only. Does NOT touch AspNet* (users/roles).
-- Run against the same database as ConnectionStrings:DefaultConnection (default: PICTO_SMRS).

SET NOCOUNT ON;

DELETE FROM [dbo].[RequisitionRecordItems];
DELETE FROM [dbo].[RequisitionRecords];

DELETE FROM [dbo].[InventoryItemSerials];
DELETE FROM [dbo].[InventoryItems];

-- Reset identity seeds so new rows start at 1 again
DBCC CHECKIDENT ('[dbo].[RequisitionRecordItems]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[RequisitionRecords]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[InventoryItemSerials]', RESEED, 0);
DBCC CHECKIDENT ('[dbo].[InventoryItems]', RESEED, 0);
