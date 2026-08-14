using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AuditCkDayo.Services
{
    public class VoiceBiService
    {
        private readonly AuditDbContext _context;

        public VoiceBiService(AuditDbContext context)
        {
            _context = context;
        }

        public async Task<string> GetPnlSummaryJsonAsync(DateTime startDate, DateTime endDate)
        {
            var auditItems = await _context.AuditItems
                .AsNoTracking()
                .Include(a => a.Buyer)
                .Include(a => a.Establishment)
                .Include(a => a.Details)
                .Include(a => a.Images)
                .Where(a => a.Status == AuditStatus.Approved)
                .ToListAsync();

            var salesReports = await _context.SalesReports
                .AsNoTracking()
                .Include(r => r.DocumentRecord)
                .Include(r => r.Establishment)
                .Where(r => r.Status == SalesReportStatus.Confirmed)
                .ToListAsync();

            var pnl = PnlReportViewModel.Build(auditItems, salesReports, startDate, endDate);

            var summary = new
            {
                pnl.StartDate,
                pnl.EndDate,
                pnl.BranchName,
                TotalSales = pnl.TotalSales.ToString("N2"),
                CogsTotal = pnl.CogsTotal.ToString("N2"),
                GrossProfit = pnl.GrossProfit.ToString("N2"),
                OpexTotal = pnl.OpexTotal.ToString("N2"),
                MonthlyFixedCostTotal = pnl.MonthlyFixedCostTotal.ToString("N2"),
                OtherTotal = pnl.OtherTotal.ToString("N2"),
                TotalExpenses = pnl.TotalExpenses.ToString("N2"),
                NetProfit = pnl.NetProfit.ToString("N2"),
                NetProfitPercentage = $"{pnl.NetProfitPercentage}%",
                Branches = pnl.Branches.Select(b => new
                {
                    b.BranchName,
                    Sales = b.Sales.ToString("N2"),
                    Cogs = b.Cogs.ToString("N2"),
                    Opex = b.Opex.ToString("N2"),
                    MonthlyFixedCost = b.MonthlyFixedCost.ToString("N2"),
                    Other = b.Other.ToString("N2"),
                    GrossProfit = b.GrossProfit.ToString("N2"),
                    NetProfit = b.NetProfit.ToString("N2"),
                    NetProfitPercentage = $"{b.NetProfitPercentage}%"
                }).ToList()
            };

            return JsonSerializer.Serialize(summary);
        }
    }
}
