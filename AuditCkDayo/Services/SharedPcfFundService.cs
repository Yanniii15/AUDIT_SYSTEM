using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace AuditCkDayo.Services
{
    /// <summary>
    /// Centralizes all PCF balance reads and mutations.
    /// Users that belong to an establishment share that establishment's single
    /// PCF fund (PcfBalance / DailyStartingFloat). Standalone users keep a personal balance.
    /// </summary>
    public class SharedPcfFundService
    {
        private readonly Data.AuditDbContext _db;

        public SharedPcfFundService(Data.AuditDbContext db)
        {
            _db = db;
        }

        private async Task<Models.Establishment?> ResolveEstablishmentAsync(int? establishmentId)
        {
            if (!establishmentId.HasValue)
            {
                return null;
            }
            return await _db.Establishments.FirstOrDefaultAsync(e => e.Id == establishmentId.Value);
        }

        /// <summary>
        /// Only BranchStaff share their establishment's fund. Buyers (and other roles)
        /// keep an independent personal PCF balance even if they are linked to a branch.
        /// </summary>
        public static bool UsesSharedFund(Models.User user)
            => user.Role == Models.UserRole.BranchStaff && user.EstablishmentId.HasValue;

        /// <summary>Available (current) spendable PCF for a user, from the shared establishment fund when applicable.</summary>
        public async Task<decimal> GetAvailableBalanceAsync(Models.User user)
        {
            if (UsesSharedFund(user))
            {
                var est = await ResolveEstablishmentAsync(user.EstablishmentId.Value);
                return est?.PcfBalance ?? 0m;
            }
            return user.PcfBalance;
        }

        /// <summary>Daily starting float for a user, from the shared establishment fund when applicable.</summary>
        public async Task<decimal> GetStartingFloatAsync(Models.User user)
        {
            if (UsesSharedFund(user))
            {
                var est = await ResolveEstablishmentAsync(user.EstablishmentId.Value);
                return est?.DailyStartingFloat ?? 0m;
            }
            return user.DailyStartingFloat;
        }

        /// <summary>Deduct from the shared fund (or personal balance for non-shared users).</summary>
        public async Task DebitAsync(Models.User user, decimal amount, bool adjustStartingFloat = false)
        {
            if (UsesSharedFund(user))
            {
                var est = await ResolveEstablishmentAsync(user.EstablishmentId.Value);
                if (est != null)
                {
                    est.PcfBalance -= amount;
                    if (adjustStartingFloat)
                    {
                        est.DailyStartingFloat -= amount;
                    }
                    return;
                }
            }
            user.PcfBalance -= amount;
            if (adjustStartingFloat)
            {
                user.DailyStartingFloat -= amount;
            }
        }

        /// <summary>Credit the shared fund (or personal balance for non-shared users).</summary>
        public async Task CreditAsync(Models.User user, decimal amount, bool adjustStartingFloat = false)
        {
            if (UsesSharedFund(user))
            {
                var est = await ResolveEstablishmentAsync(user.EstablishmentId.Value);
                if (est != null)
                {
                    est.PcfBalance += amount;
                    if (adjustStartingFloat)
                    {
                        est.DailyStartingFloat += amount;
                    }
                    return;
                }
            }
            user.PcfBalance += amount;
            if (adjustStartingFloat)
            {
                user.DailyStartingFloat += amount;
            }
        }

        /// <summary>
        /// If the fund's spendable balance is fully exhausted (0 or less) after a
        /// surrender, collapse the daily starting float to 0 as well, since the
        /// branch no longer holds any fund. On a partial surrender the float is kept.
        /// </summary>
        public async Task ResetFloatOnFullSurrenderAsync(Models.User user)
        {
            if (UsesSharedFund(user))
            {
                var est = await ResolveEstablishmentAsync(user.EstablishmentId.Value);
                if (est != null && est.PcfBalance <= 0m)
                {
                    est.DailyStartingFloat = 0m;
                }
                return;
            }
            if (user.PcfBalance <= 0m)
            {
                user.DailyStartingFloat = 0m;
            }
        }

        /// <summary>
        /// Shared-aware aggregate: sums each establishment's shared balance once
        /// (only for BranchStaff sharing a fund), plus the personal balance of every
        /// other (non-shared) user such as Buyers and Managers.
        /// </summary>
        public static async Task<decimal> SumSharedAwareAsync(IQueryable<Models.User> users)
        {
            var branchSum = await users
                .Where(u => u.Role == Models.UserRole.BranchStaff && u.EstablishmentId.HasValue)
                .Select(u => u.Establishment!.PcfBalance)
                .Distinct()
                .SumAsync();
            var personalSum = await users
                .Where(u => !(u.Role == Models.UserRole.BranchStaff && u.EstablishmentId.HasValue))
                .SumAsync(u => u.PcfBalance);
            return branchSum + personalSum;
        }
    }
}