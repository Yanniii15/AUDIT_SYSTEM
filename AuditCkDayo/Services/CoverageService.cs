using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using Microsoft.EntityFrameworkCore;

namespace AuditCkDayo.Services
{
    public class CoverageService
    {
        private readonly AuditDbContext _context;

        public CoverageService(AuditDbContext context)
        {
            _context = context;
        }

        public async Task<List<int>> GetCoveredManagerIdsAsync(int coveringManagerId, DateTime date, CoverageScope scope)
        {
            var day = date.Date;
            var coverages = await _context.ManagerCoverages
                .AsNoTracking()
                .Where(c => c.CoveringManagerId == coveringManagerId 
                    && c.IsActive 
                    && c.StartDate.Date <= day 
                    && c.EndDate.Date >= day)
                .ToListAsync();

            return coverages
                .Where(c => (c.Scope & scope) != CoverageScope.None)
                .Select(c => c.CoveredManagerId)
                .ToList();
        }

        public List<int> GetCoveredManagerIds(int coveringManagerId, DateTime date, CoverageScope scope)
        {
            var day = date.Date;
            var coverages = _context.ManagerCoverages
                .AsNoTracking()
                .Where(c => c.CoveringManagerId == coveringManagerId 
                    && c.IsActive 
                    && c.StartDate.Date <= day 
                    && c.EndDate.Date >= day)
                .ToList();

            return coverages
                .Where(c => (c.Scope & scope) != CoverageScope.None)
                .Select(c => c.CoveredManagerId)
                .ToList();
        }
    }
}
