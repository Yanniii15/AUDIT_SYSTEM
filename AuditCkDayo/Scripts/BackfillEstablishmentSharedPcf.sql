-- =============================================================================
-- Backfill: shared Establishment PCF fund (AddEstablishmentSharedPcf)
--
-- Run this AFTER applying the AddEstablishmentSharedPcf schema migration, and
-- ONLY when you have verified the shared-fund code locally against a snapshot.
--
-- What it does:
--   1. For each establishment that has active (non-deleted) staff, copy the
--      representative fund from its staff into the Establishment's
--      DailyStartingFloat / PcfBalance. The representative is the staff member
--      holding the highest balance (the branch's true fund, not a sum).
--   2. Zero out the personal PcfBalance / DailyStartingFloat of all branch
--      staff, since their money now lives on the shared Establishment fund.
--      Standalone users (EstablishmentId IS NULL) are untouched.
--
-- The script is idempotent and runs inside a single transaction.
-- =============================================================================

START TRANSACTION;

-- 1. Seed each establishment's shared float/balance from its top-holding staff member.
UPDATE Establishments e
SET e.DailyStartingFloat = COALESCE(
        (SELECT MAX(u.DailyStartingFloat)
           FROM Users u
          WHERE u.EstablishmentId = e.Id AND u.IsDeleted = 0), 0),
    e.PcfBalance = COALESCE(
        (SELECT MAX(u.PcfBalance)
           FROM Users u
          WHERE u.EstablishmentId = e.Id AND u.IsDeleted = 0), 0)
WHERE e.Id IN (
        SELECT DISTINCT EstablishmentId
          FROM Users
         WHERE EstablishmentId IS NOT NULL AND IsDeleted = 0
      );

-- 2. Zero the personal balances of branch staff (fund now held at establishment level).
UPDATE Users
SET PcfBalance = 0,
    DailyStartingFloat = 0
WHERE EstablishmentId IS NOT NULL
  AND IsDeleted = 0;

COMMIT;

-- Sanity check: each establishment's shared fund and its active staff count.
SELECT e.Id AS EstablishmentId,
       e.Name AS EstablishmentName,
       e.DailyStartingFloat,
       e.PcfBalance,
       (SELECT COUNT(*) FROM Users u
         WHERE u.EstablishmentId = e.Id AND u.IsDeleted = 0) AS ActiveStaff
  FROM Establishments e
 ORDER BY e.Id;
