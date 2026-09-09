SET FOREIGN_KEY_CHECKS = 0;
START TRANSACTION;
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 5, 39862.83, 'Gloria Pron August Liquidation (Aug 01)', '2026-08-01 00:00:00', '2026-08-01 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Broccoli', 1.000, 5040.00, 5040.00, 5, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Kamote', 1.000, 850.00, 850.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 850.00, 850.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 2332.00, 2332.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1061.00, 1061.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 316.00, 316.00, 5, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Gas L300', 1.000, 3825.33, 3825.33, 2, 6, 'OPEX', 'Transportation & Freight', 22, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Lucky Star', 1.000, 500.00, 500.00, 5, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Lucky Star', 1.000, 970.00, 970.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 2228.50, 2228.50, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 2550.00, 2550.00, 4, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 4480.00, 4480.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 1775.00, 1775.00, 5, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Leche Flan', 1.000, 13000.00, 13000.00, 5, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 1, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Aluminum Foil', 1.000, 60.00, 60.00, 5, NULL, 'COGS', 'Kitchen Condiments & Spices', 19, 'HasReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 8, 2498.00, 'Gloria Pron August Liquidation (Aug 02)', '2026-08-02 00:00:00', '2026-08-02 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Market', 1.000, 380.00, 380.00, 1, 5, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 388.00, 388.00, 5, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 255.00, 255.00, 8, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Kimchi', 1.000, 1450.00, 1450.00, 8, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 8, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 4, 11965.00, 'Gloria Pron August Liquidation (Aug 03)', '2026-08-03 00:00:00', '2026-08-03 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Mani', 1.000, 525.00, 525.00, 8, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 2030.00, 2030.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 2152.00, 2152.00, 4, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Luckystar', 1.000, 920.00, 920.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 3059.00, 3059.00, 4, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 256.00, 256.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 2598.00, 2598.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 8, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Maam Lolit Massage', 1.000, 400.00, 400.00, 2, NULL, 'OPEX', 'Salaries & Labor', 21, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 8, 18171.50, 'Gloria Pron August Liquidation (Aug 04)', '2026-08-04 00:00:00', '2026-08-04 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Market', 1.000, 1720.00, 1720.00, 8, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1017.00, 1017.00, 5, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Shrimps', 1.000, 4300.00, 4300.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'SM', 1.000, 2665.50, 2665.50, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'SM', 1.000, 2566.00, 2566.00, 5, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 292.00, 292.00, 5, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'LCC', 1.000, 2406.00, 2406.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 2850.00, 2850.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 55.00, 55.00, 8, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Walis Tambo Monte Cielo', 1.000, 300.00, 300.00, 9, NULL, 'OPEX', 'Store & Cleaning Supplies', 23, 'HasReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 2, 6474.00, 'Gloria Pron August Liquidation (Aug 05)', '2026-08-05 00:00:00', '2026-08-05 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Market', 1.000, 190.00, 190.00, 5, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 697.00, 697.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 5562.00, 5562.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 2, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (12, 2, 1539.00, 'Elizabeth Tuscano August Liquidation (Aug 05)', '2026-08-05 00:00:00', '2026-08-05 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Kalabasa', 1.000, 80.00, 80.00, 5, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 230.00, 230.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Hardware', 1.000, 200.00, 200.00, 7, NULL, 'OPEX', 'Store & Cleaning Supplies', 23, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Delli Gold', 1.000, 709.00, 709.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Chocks To Go', 1.000, 320.00, 320.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (12, 1, 18247.00, 'Elizabeth Tuscano August Liquidation (Aug 06)', '2026-08-06 00:00:00', '2026-08-06 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Hito', 1.000, 680.00, 680.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Melon', 1.000, 570.00, 570.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1986.00, 1986.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 600.00, 600.00, 4, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 2961.00, 2961.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Luckystar', 1.000, 230.00, 230.00, 4, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 3363.00, 3363.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 5332.00, 5332.00, 4, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 393.50, 393.50, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 1220.00, 1220.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'LCC', 1.000, 186.50, 186.50, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Flower', 1.000, 700.00, 700.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 1, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (12, 5, 23975.00, 'Elizabeth Tuscano August Liquidation (Aug 07)', '2026-08-07 00:00:00', '2026-08-07 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Market', 1.000, 1948.00, 1948.00, 5, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 3276.00, 3276.00, 5, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, '3N Bakery', 1.000, 600.00, 600.00, 5, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, '3N Bakery', 1.000, 100.00, 100.00, 5, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 2683.00, 2683.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'SM', 1.000, 431.25, 431.25, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'SM', 1.000, 9633.00, 9633.00, 5, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 5248.75, 5248.75, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 55.00, 55.00, 2, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (12, 8, 10075.00, 'Elizabeth Tuscano August Liquidation (Aug 08)', '2026-08-08 00:00:00', '2026-08-08 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Pusit', 1.000, 2500.00, 2500.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Tahong', 1.000, 4500.00, 4500.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 280.00, 280.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Leche Flan', 1.000, 2670.00, 2670.00, 5, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Yosi Maam Lolit', 1.000, 100.00, 100.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 2, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (12, 2, 28434.00, 'Elizabeth Tuscano August Liquidation (Aug 09)', '2026-08-09 00:00:00', '2026-08-09 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Kapalmoks', 1.000, 3620.00, 3620.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1559.00, 1559.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1868.00, 1868.00, 4, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 3413.00, 3413.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 2598.00, 2598.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 3536.00, 3536.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 353.00, 353.00, 4, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Uling', 1.000, 100.00, 100.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 2, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified'),
(@currentAuditId, '1.2 Tilapia', 1.000, 216.00, 216.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '2 kls Pork', 1.000, 640.00, 640.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '1.5 Kamatis', 1.000, 150.00, 150.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Langka', 1.000, 50.00, 50.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Talong', 1.000, 60.00, 60.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Patatas', 1.000, 50.00, 50.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Gata', 1.000, 100.00, 100.00, 1, NULL, 'COGS', 'Kitchen Condiments & Spices', 19, 'HasReceipt', 'Verified'),
(@currentAuditId, '1.6 Chicken', 1.000, 304.00, 304.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '1/2 Sapsap', 1.000, 100.00, 100.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '1/2 Kalamansi', 1.000, 50.00, 50.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Pechay', 1.000, 50.00, 50.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Beans', 1.000, 20.00, 20.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Sili Labuyo', 1.000, 20.00, 20.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Malagkit', 1.000, 35.00, 35.00, 1, NULL, 'COGS', 'Rice & Grains', 18, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Sprite', 1.000, 65.00, 65.00, 1, NULL, 'COGS', 'Beverages', 9, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Mini Mart', 1.000, 57.00, 57.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'LCC Grocery', 1.000, 4765.00, 4765.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Barbie Parcel', 1.000, 795.00, 795.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Makoi Parcel', 1.000, 153.00, 153.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Ysabelle Parcel', 1.000, 88.00, 88.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Makoi Parcel', 1.000, 595.00, 595.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Bernie Gas', 1.000, 200.00, 200.00, 2, 6, 'OPEX', 'Transportation & Freight', 22, 'HasReceipt', 'Verified'),
(@currentAuditId, 'P. BBQ', 1.000, 450.00, 450.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'P. BBQ', 1.000, 315.00, 315.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Abunar', 1.000, 2034.00, 2034.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (18, 1, 4600.00, 'Myla Ricafrente August Liquidation (Aug 10)', '2026-08-10 00:00:00', '2026-08-10 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, '2 Reams Plastic Yelo', 1.000, 880.00, 880.00, 1, NULL, 'COGS', 'Packaging', 10, 'HasReceipt', 'Verified'),
(@currentAuditId, '3kg B.Sili', 1.000, 720.00, 720.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '2kg Sili Haba', 1.000, 140.00, 140.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '1kg Luya', 1.000, 160.00, 160.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '1kg Bawang', 1.000, 180.00, 180.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '2kg Paminta Durog', 1.000, 1080.00, 1080.00, 1, NULL, 'COGS', 'Kitchen Condiments & Spices', 19, 'HasReceipt', 'Verified'),
(@currentAuditId, '1kg Paminta Crack', 1.000, 480.00, 480.00, 1, NULL, 'COGS', 'Kitchen Condiments & Spices', 19, 'HasReceipt', 'Verified'),
(@currentAuditId, '1kg Paminta Buo', 1.000, 480.00, 480.00, 1, NULL, 'COGS', 'Kitchen Condiments & Spices', 19, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Gasoline Trike', 1.000, 300.00, 300.00, 1, NULL, 'OPEX', 'Transportation & Freight', 22, 'HasReceipt', 'Verified'),
(@currentAuditId, '2pcs Butane', 1.000, 180.00, 180.00, 1, NULL, 'OPEX', 'LPG & Cooking Gas', 20, 'HasReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (18, 1, 13515.00, 'Myla Ricafrente August Liquidation (Aug 13)', '2026-08-13 00:00:00', '2026-08-13 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, '30 Mais', 1.000, 735.00, 735.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '10 Pinya', 1.000, 650.00, 650.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Fruits', 1.000, 1385.00, 1385.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Barista Milk', 1.000, 880.00, 880.00, 1, NULL, 'COGS', 'Beverages', 9, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Rice', 1.000, 2690.00, 2690.00, 1, NULL, 'COGS', 'Rice & Grains', 18, 'HasReceipt', 'Verified'),
(@currentAuditId, '1 Rim 20x30 Plastic', 1.000, 850.00, 850.00, 1, NULL, 'COGS', 'Packaging', 10, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Gas Trike', 1.000, 300.00, 300.00, 1, NULL, 'OPEX', 'Transportation & Freight', 22, 'HasReceipt', 'Verified'),
(@currentAuditId, '7kg Bellpepper', 1.000, 1680.00, 1680.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '2kg Sili Sigang', 1.000, 280.00, 280.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Bawang Hubad', 1.000, 198.00, 198.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '2kg Kalamansi Sisig', 1.000, 160.00, 160.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '8pcs Native Manok', 1.000, 789.00, 789.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '2kg Kamatis', 1.000, 320.00, 320.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '1kg Bawang', 1.000, 120.00, 120.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '1/4kg Laurel', 1.000, 70.00, 70.00, 1, NULL, 'COGS', 'Kitchen Condiments & Spices', 19, 'HasReceipt', 'Verified'),
(@currentAuditId, '1kg Luya', 1.000, 160.00, 160.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Red Onion 3kg', 1.000, 270.00, 270.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '3kg White Onion', 1.000, 359.00, 359.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '2kg Pipino', 1.000, 142.00, 142.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '1kg Yellow Lemon', 1.000, 230.00, 230.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, '2kg Kalamansi', 1.000, 160.00, 160.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '5 Big Ginisa Mix', 1.000, 552.00, 552.00, 8, NULL, 'COGS', 'Kitchen Condiments & Spices', 19, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Kamote', 1.000, 535.00, 535.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 2, 30597.00, 'Gloria Pron August Liquidation (Aug 15)', '2026-08-15 00:00:00', '2026-08-15 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Bayad ki Kuya Sonny', 1.000, 500.00, 500.00, 2, NULL, 'OPEX', 'Salaries & Labor', 21, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 199.00, 199.00, 4, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Wrapper', 1.000, 250.00, 250.00, 8, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Hito', 1.000, 680.00, 680.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1640.00, 1640.00, 5, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 2206.00, 2206.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Flower', 1.000, 400.00, 400.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Flower', 1.000, 350.00, 350.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Caramel Bar', 1.000, 700.00, 700.00, 5, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Crabpaste', 1.000, 1600.00, 1600.00, 2, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 819.00, 819.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Luckystar', 1.000, 1000.00, 1000.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 547.00, 547.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 1700.00, 1700.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'SM', 1.000, 1963.00, 1963.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'SM', 1.000, 1911.00, 1911.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'SM', 1.000, 3487.00, 3487.00, 5, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 4512.00, 4512.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 3851.00, 3851.00, 4, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 620.00, 620.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 1607.00, 1607.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 55.00, 55.00, 2, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (18, 1, 10229.51, 'Myla Ricafrente August Liquidation (Aug 15)', '2026-08-15 00:00:00', '2026-08-15 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'LPG Trading', 1.000, 2840.00, 2840.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'LPG Trading', 1.000, 1420.00, 1420.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'LCC', 1.000, 310.00, 310.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'LCC', 1.000, 400.00, 400.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'LCC', 1.000, 84.00, 84.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'LCC', 1.000, 561.51, 561.51, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, '7-Eleven', 1.000, 320.00, 320.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'LCC', 1.000, 344.50, 344.50, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, '7-Eleven', 1.000, 900.50, 900.50, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, '7-Eleven', 1.000, 849.00, 849.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, '7-Eleven', 1.000, 580.00, 580.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, '7-Eleven', 1.000, 433.00, 433.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Carwash', 1.000, 450.00, 450.00, 2, 6, 'OPEX', 'Transportation & Freight', 22, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Chooks To Go', 1.000, 325.00, 325.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Ysabelle Parcel', 1.000, 412.00, 412.00, 9, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 2, 6918.00, 'Gloria Pron August Liquidation (Aug 16)', '2026-08-16 00:00:00', '2026-08-16 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Gas Pavillon', 1.000, 3000.00, 3000.00, 2, 6, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Gas L300', 1.000, 3768.00, 3768.00, 2, 6, 'OPEX', 'Transportation & Freight', 22, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Egg Staffmeal', 1.000, 150.00, 150.00, 1, 5, 'COGS', 'Food Ingredients', 8, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 2, 12650.00, 'Gloria Pron August Liquidation (Aug 17)', '2026-08-17 00:00:00', '2026-08-17 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Wrapper', 1.000, 250.00, 250.00, 8, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Hito', 1.000, 680.00, 680.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Farmstation', 1.000, 800.00, 800.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 270.00, 270.00, 4, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 3046.00, 3046.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Egg', 1.000, 495.00, 495.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 5171.00, 5171.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1663.00, 1663.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 2, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Snack Dayo', 1.000, 200.00, 200.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Hotdog', 1.000, 50.00, 50.00, 5, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (12, 1, 9499.75, 'Elizabeth Tuscano August Liquidation (Aug 17)', '2026-08-17 00:00:00', '2026-08-17 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Barbie Parcel', 1.000, 1495.00, 1495.00, 9, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Ysabelle Parcel', 1.000, 170.00, 170.00, 9, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Makoi Parcel', 1.000, 640.00, 640.00, 9, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'LCC', 1.000, 253.75, 253.75, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Lumpia Wrapper', 1.000, 35.00, 35.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Hotdog', 1.000, 85.00, 85.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, '7-Eleven', 1.000, 102.00, 102.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Daniel Allowance', 1.000, 1000.00, 1000.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Flower', 1.000, 500.00, 500.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Saging', 1.000, 76.00, 76.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Jose & Pamela', 1.000, 100.00, 100.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Pansit & Repolyo', 1.000, 100.00, 100.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, '1.5 kg Pork Ribs', 1.000, 405.00, 405.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '1 kg Porkchop', 1.000, 300.00, 300.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '1 Liempo', 1.000, 300.00, 300.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '1 kg Lumo Pork', 1.000, 300.00, 300.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Atay', 1.000, 160.00, 160.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Liver Spread & Carrots', 1.000, 100.00, 100.00, 1, NULL, 'COGS', 'Kitchen Condiments & Spices', 19, 'HasReceipt', 'Verified'),
(@currentAuditId, '1 kg Tilapia', 1.000, 180.00, 180.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '1/2 kg Borao', 1.000, 120.00, 120.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '1/2 Titso', 1.000, 100.00, 100.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '1/2 Hipon', 1.000, 170.00, 170.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '1.2 Kalamansi', 1.000, 120.00, 120.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '1.3 Kamatis', 1.000, 115.00, 115.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Saging Saba', 1.000, 118.00, 118.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Gabi', 1.000, 35.00, 35.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Gata', 1.000, 50.00, 50.00, 1, NULL, 'COGS', 'Kitchen Condiments & Spices', 19, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Pusit', 1.000, 100.00, 100.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '1 kg Sibuyas', 1.000, 120.00, 120.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '1 kg Bawang', 1.000, 100.00, 100.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Ogob', 1.000, 70.00, 70.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Luya', 1.000, 35.00, 35.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Pipino', 1.000, 40.00, 40.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '1 Tray Egg', 1.000, 250.00, 250.00, 9, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Harina', 1.000, 65.00, 65.00, 9, NULL, 'COGS', 'Kitchen Condiments & Spices', 19, 'HasReceipt', 'Verified'),
(@currentAuditId, '5 kg Tilapia', 1.000, 850.00, 850.00, 5, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '1 kg Pipino', 1.000, 120.00, 120.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '3 pcs Pinya', 1.000, 120.00, 120.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '1 kl Lettuce', 1.000, 500.00, 500.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 1, 39518.00, 'Gloria Pron August Liquidation (Aug 20)', '2026-08-20 00:00:00', '2026-08-20 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Walis Tingting & Tambo', 1.000, 590.00, 590.00, 1, NULL, 'OPEX', 'Store & Cleaning Supplies', 23, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Pusit', 1.000, 2500.00, 2500.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 260.00, 260.00, 8, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 5653.00, 5653.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1444.00, 1444.00, 4, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 2620.00, 2620.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 671.00, 671.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1395.00, 1395.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 4120.00, 4120.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 6137.00, 6137.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 3725.00, 3725.00, 4, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 2955.00, 2955.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 5155.00, 5155.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'LCC', 1.000, 2268.00, 2268.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 1, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (12, 1, 13690.00, 'Elizabeth Tuscano August Liquidation (Aug 20)', '2026-08-20 00:00:00', '2026-08-20 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, '1.3 Pork Ribs', 1.000, 338.00, 338.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '2 kls Porkchop', 1.000, 600.00, 600.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, '1.5 Kalamansi', 1.000, 120.00, 120.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Sibuyas', 1.000, 100.00, 100.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, '1 kg Gata', 1.000, 100.00, 100.00, 1, NULL, 'COGS', 'Kitchen Condiments & Spices', 19, 'HasReceipt', 'Verified'),
(@currentAuditId, '1.5 Manok', 1.000, 285.00, 285.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Asin', 1.000, 20.00, 20.00, 1, NULL, 'COGS', 'Kitchen Condiments & Spices', 19, 'HasReceipt', 'Verified'),
(@currentAuditId, '10 pcs Barbeque', 1.000, 450.00, 450.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Banana', 1.000, 120.00, 120.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Gulay Sigang', 1.000, 100.00, 100.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Gas Ber', 1.000, 200.00, 200.00, 2, 6, 'OPEX', 'Transportation & Freight', 22, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Bread & Lady''s Choice', 1.000, 100.00, 100.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, '1 Tray Egg', 1.000, 250.00, 250.00, 9, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'LCC', 1.000, 102.00, 102.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Makoi Parcel', 1.000, 298.00, 298.00, 9, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Makoi Parcel', 1.000, 499.00, 499.00, 9, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Makoi Parcel', 1.000, 598.00, 598.00, 9, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Grapes', 1.000, 100.00, 100.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Ysabelle Cash', 1.000, 1000.00, 1000.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Makoi Parcel', 1.000, 113.00, 113.00, 9, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'LCC', 1.000, 1602.50, 1602.50, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'LCC', 1.000, 1062.75, 1062.75, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, '7-Eleven', 1.000, 2440.00, 2440.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, '7-Eleven', 1.000, 1760.00, 1760.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'LCC', 1.000, 1331.75, 1331.75, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 1, 9055.00, 'Gloria Pron August Liquidation (Aug 21)', '2026-08-21 00:00:00', '2026-08-21 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Pineapple Juice', 1.000, 270.00, 270.00, 1, NULL, 'COGS', 'Beverages', 9, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Flower', 1.000, 1000.00, 1000.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1947.00, 1947.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 402.00, 402.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Lehman', 1.000, 357.00, 357.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Southstar Maam Lolit', 1.000, 95.00, 95.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Gas Maam Lolit', 1.000, 2000.00, 2000.00, 2, 6, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 2, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 1349.00, 1349.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market Siruma', 1.000, 1610.00, 1610.00, 7, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 1, 3040.00, 'Gloria Pron August Liquidation (Aug 22)', '2026-08-22 00:00:00', '2026-08-22 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Market', 1.000, 245.00, 245.00, 8, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Pork', 1.000, 1620.00, 1620.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Iceberg', 1.000, 150.00, 150.00, 8, NULL, 'OPEX', 'Transportation & Freight', 22, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 8, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Bagas John Paul', 1.000, 1000.00, 1000.00, 1, NULL, 'COGS', 'Rice & Grains', 18, 'HasReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 2, 27813.00, 'Gloria Pron August Liquidation (Aug 23)', '2026-08-23 00:00:00', '2026-08-23 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Butane', 1.000, 360.00, 360.00, 8, NULL, 'OPEX', 'LPG & Cooking Gas', 20, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1900.00, 1900.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 2190.00, 2190.00, 4, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 2744.00, 2744.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'SM', 1.000, 1837.00, 1837.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 2600.00, 2600.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 2244.00, 2244.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 1496.00, 1496.00, 4, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 5989.00, 5989.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 6074.00, 6074.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 354.00, 354.00, 4, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 2, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 2, 6165.00, 'Gloria Pron August Liquidation (Aug 24)', '2026-08-24 00:00:00', '2026-08-24 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Market', 1.000, 1310.00, 1310.00, 8, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Taba / Halo', 1.000, 1440.00, 1440.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Coco Lumber M-Plaza', 1.000, 3300.00, 3300.00, 2, 8, 'OPEX', 'Transportation & Freight', 22, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 8, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Fish Staffmeal', 1.000, 90.00, 90.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 1, 12375.00, 'Gloria Pron August Liquidation (Aug 26)', '2026-08-26 00:00:00', '2026-08-26 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Pork', 1.000, 1453.00, 1453.00, 1, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Rabut / Balat', 1.000, 610.00, 610.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Tahong', 1.000, 4000.00, 4000.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 3287.00, 3287.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 1, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Maam Lolit Comision', 1.000, 3000.00, 3000.00, 5, NULL, 'OPEX', 'Salaries & Labor', 21, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 1, 22363.00, 'Gloria Pron August Liquidation (Aug 27)', '2026-08-27 00:00:00', '2026-08-27 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Market', 1.000, 2970.00, 2970.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Melon', 1.000, 629.00, 629.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Mangga', 1.000, 3600.00, 3600.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Torch Gun & Butane', 1.000, 500.00, 500.00, 1, NULL, 'OPEX', 'LPG & Cooking Gas', 20, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 8946.50, 8946.50, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'LCC', 1.000, 1476.50, 1476.50, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 4016.00, 4016.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Staffmeal General Cleaning', 1.000, 200.00, 200.00, 1, 5, 'OPEX', 'Store & Cleaning Supplies', 23, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 1, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 2, 32547.00, 'Gloria Pron August Liquidation (Aug 28)', '2026-08-28 00:00:00', '2026-08-28 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Cal Manok', 1.000, 1050.00, 1050.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 2910.00, 2910.00, 8, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1020.00, 1020.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Crabpaste', 1.000, 750.00, 750.00, 4, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Luckystar', 1.000, 460.00, 460.00, 4, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Rice Construction M-Plaza', 1.000, 2200.00, 2200.00, 2, 8, 'COGS', 'Rice & Grains', 18, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Gas L300', 1.000, 4084.00, 4084.00, 2, 6, 'OPEX', 'Transportation & Freight', 22, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Bunny & Piggy', 1.000, 438.00, 438.00, 8, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 1475.00, 1475.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 2115.00, 2115.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 3893.00, 3893.00, 4, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 6659.00, 6659.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Flower', 1.000, 900.00, 900.00, 1, NULL, 'COGS', 'Vegetables & Produce', 17, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1433.00, 1433.00, 4, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 3135.00, 3135.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 2, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 7, 10514.00, 'Gloria Pron August Liquidation (Aug 29)', '2026-08-29 00:00:00', '2026-08-29 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Tilapia Sample Cater', 1.000, 170.00, 170.00, 5, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1561.00, 1561.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Hong', 1.000, 1421.00, 1421.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 262.00, 262.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Montemart', 1.000, 1280.00, 1280.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Lehman', 1.000, 595.00, 595.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Deliver Pato Farm', 1.000, 5200.00, 5200.00, 7, NULL, 'COGS', 'Meat & Poultry', 16, 'NoReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 25.00, 25.00, 1, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified');
INSERT INTO AuditItems (BuyerId, EstablishmentId, Amount, Description, EntryDate, SubmittedAt, Status, Notes) VALUES (11, 8, 26691.00, 'Gloria Pron August Liquidation (Aug 30)', '2026-08-30 00:00:00', '2026-08-30 17:00:00', 'Approved', 'Imported from August 2026 Buyer Expense Sheets');
SET @currentAuditId = LAST_INSERT_ID();
INSERT INTO AuditItemDetails (AuditItemId, ItemName, Quantity, Price, Total, AssignedEstablishmentId, CostCenterId, PnlSection, PnlCategoryName, PnlCategoryId, ReceiptStatus, BranchVerificationStatus) VALUES (@currentAuditId, 'Taba / Halo', 1.000, 4600.00, 4600.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Balat / Rabut', 1.000, 880.00, 880.00, 1, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Taba / Halo', 1.000, 1440.00, 1440.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Leche Flan', 1.000, 690.00, 690.00, 5, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 340.00, 340.00, 8, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Hito', 1.000, 680.00, 680.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Pusit', 1.000, 2500.00, 2500.00, 8, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 399.00, 399.00, 5, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 375.00, 375.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 789.00, 789.00, 4, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 3109.00, 3109.00, 2, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Egg', 1.000, 132.00, 132.00, 10, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market Porkchop Haws', 1.000, 1688.00, 1688.00, 10, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Market', 1.000, 1618.00, 1618.00, 1, NULL, 'COGS', 'Food Ingredients', 8, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH Porkchop Haws', 1.000, 302.25, 302.25, 10, NULL, 'COGS', 'Meat & Poultry', 16, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 2318.75, 2318.75, 4, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'TH', 1.000, 4780.00, 4780.00, 2, NULL, 'Other', 'Miscellaneous', 14, 'HasReceipt', 'Verified'),
(@currentAuditId, 'Parking', 1.000, 50.00, 50.00, 2, NULL, 'OPEX', 'Transportation & Freight', 22, 'NoReceipt', 'Verified');
COMMIT;
SET FOREIGN_KEY_CHECKS = 1;
