using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.ViewModels;

namespace AuditCkDayo.Services
{
    public interface IDiagnosticsPathProvider
    {
        string RuntimeBasePath { get; }
        string ContentRootPath { get; }
    }

    public sealed class AppDiagnosticsPathProvider : IDiagnosticsPathProvider
    {
        private readonly IWebHostEnvironment _environment;

        public AppDiagnosticsPathProvider(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public string RuntimeBasePath => AppContext.BaseDirectory;
        public string ContentRootPath => _environment.ContentRootPath;
    }

    public sealed class StaticDiagnosticsPathProvider : IDiagnosticsPathProvider
    {
        public StaticDiagnosticsPathProvider(string runtimeBasePath, string contentRootPath)
        {
            RuntimeBasePath = runtimeBasePath;
            ContentRootPath = contentRootPath;
        }

        public string RuntimeBasePath { get; }
        public string ContentRootPath { get; }
    }

    public sealed class SystemDiagnosticsService
    {
        private readonly AuditDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IDiagnosticsPathProvider _paths;

        public SystemDiagnosticsService(AuditDbContext context, IConfiguration configuration, IDiagnosticsPathProvider paths)
        {
            _context = context;
            _configuration = configuration;
            _paths = paths;
        }

        public async Task<SystemDiagnosticsReport> RunAsync()
        {
            var groups = new List<SystemDiagnosticsGroup>
            {
                await BuildDatabaseGroupAsync(),
                await BuildAccountsGroupAsync(),
                BuildStorageGroup(),
                BuildOcrGroup(),
                BuildApplicationGroup()
            };

            return new SystemDiagnosticsReport
            {
                CheckedAt = DateTime.UtcNow,
                OverallStatus = groups.SelectMany(group => group.Checks).Any(check => check.Status == DiagnosticsCheckStatus.Fail)
                    ? DiagnosticsCheckStatus.Fail
                    : DiagnosticsCheckStatus.Pass,
                Groups = groups
            };
        }

        private async Task<SystemDiagnosticsGroup> BuildDatabaseGroupAsync()
        {
            var group = new SystemDiagnosticsGroup { Name = "Database" };
            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                group.Checks.Add(new SystemDiagnosticsCheck
                {
                    Name = "Connection",
                    Status = canConnect ? DiagnosticsCheckStatus.Pass : DiagnosticsCheckStatus.Fail,
                    Detail = canConnect ? "Database connection opened successfully." : "Database connection failed."
                });
            }
            catch (Exception ex)
            {
                group.Checks.Add(Fail("Connection", ex.Message));
            }

            try
            {
                var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync();
                group.Checks.Add(new SystemDiagnosticsCheck
                {
                    Name = "Migrations",
                    Status = DiagnosticsCheckStatus.Pass,
                    Detail = $"Applied migrations: {appliedMigrations.Count()}."
                });
            }
            catch (Exception ex)
            {
                group.Checks.Add(new SystemDiagnosticsCheck
                {
                    Name = "Migrations",
                    Status = DiagnosticsCheckStatus.Warn,
                    Detail = $"Migration metadata unavailable: {ex.Message}"
                });
            }

            try
            {
                var tableSummary = new[]
                {
                    $"Users={await _context.Users.CountAsync()}",
                    $"Establishments={await _context.Establishments.CountAsync()}",
                    $"AuditItems={await _context.AuditItems.CountAsync()}",
                    $"SalesReports={await _context.SalesReports.CountAsync()}",
                    $"TreasuryCashFlows={await _context.TreasuryCashFlows.CountAsync()}"
                };
                group.Checks.Add(new SystemDiagnosticsCheck
                {
                    Name = "Core tables",
                    Status = DiagnosticsCheckStatus.Pass,
                    Detail = string.Join(", ", tableSummary)
                });
            }
            catch (Exception ex)
            {
                group.Checks.Add(Fail("Core tables", ex.Message));
            }

            return group;
        }

        private async Task<SystemDiagnosticsGroup> BuildAccountsGroupAsync()
        {
            var group = new SystemDiagnosticsGroup { Name = "Accounts" };
            try
            {
                var activeUsers = await _context.Users
                    .Where(user => !user.IsDeleted)
                    .GroupBy(user => user.Role)
                    .Select(grouping => new { Role = grouping.Key, Count = grouping.Count() })
                    .ToDictionaryAsync(row => row.Role, row => row.Count);

                var required = new Dictionary<UserRole, int>
                {
                    [UserRole.Admin] = 1,
                    [UserRole.Owner] = 2,
                    [UserRole.Manager] = 2,
                    [UserRole.Buyer] = 2,
                    [UserRole.BranchStaff] = 2
                };

                var details = required
                    .Select(pair => $"{pair.Key}: {activeUsers.GetValueOrDefault(pair.Key)}/{pair.Value}")
                    .ToList();
                var allPresent = required.All(pair => activeUsers.GetValueOrDefault(pair.Key) >= pair.Value);

                group.Checks.Add(new SystemDiagnosticsCheck
                {
                    Name = "Required role accounts",
                    Status = allPresent ? DiagnosticsCheckStatus.Pass : DiagnosticsCheckStatus.Fail,
                    Detail = string.Join(", ", details)
                });
            }
            catch (Exception ex)
            {
                group.Checks.Add(Fail("Required role accounts", ex.Message));
            }

            return group;
        }

        private SystemDiagnosticsGroup BuildStorageGroup()
        {
            var group = new SystemDiagnosticsGroup { Name = "Storage / Editable Files" };
            group.Checks.Add(CheckWritableDirectory("Receipt uploads writable", Path.Combine(_paths.RuntimeBasePath, "App_Data", "uploads")));
            group.Checks.Add(CheckWritableDirectory("Sales report uploads writable", Path.Combine(_paths.RuntimeBasePath, "App_Data", "uploads", "sales-reports")));
            return group;
        }

        private SystemDiagnosticsGroup BuildOcrGroup()
        {
            var group = new SystemDiagnosticsGroup { Name = "OCR / External Services" };
            var geminiApiKey = _configuration["GoogleGemini:ApiKey"];
            group.Checks.Add(new SystemDiagnosticsCheck
            {
                Name = "Gemini API key",
                Status = string.IsNullOrWhiteSpace(geminiApiKey) ? DiagnosticsCheckStatus.Warn : DiagnosticsCheckStatus.Pass,
                Detail = string.IsNullOrWhiteSpace(geminiApiKey) ? "GoogleGemini:ApiKey is not configured. OCR can use local fallback if available." : "GoogleGemini:ApiKey is configured."
            });

            var tessdataPath = ResolveTessdataPath();
            group.Checks.Add(new SystemDiagnosticsCheck
            {
                Name = "Tesseract tessdata",
                Status = Directory.Exists(tessdataPath) ? DiagnosticsCheckStatus.Pass : DiagnosticsCheckStatus.Warn,
                Detail = Directory.Exists(tessdataPath) ? $"Found tessdata at {tessdataPath}." : $"tessdata folder not found at {tessdataPath}."
            });
            return group;
        }

        private SystemDiagnosticsGroup BuildApplicationGroup()
        {
            var group = new SystemDiagnosticsGroup { Name = "Application" };
            group.Checks.Add(new SystemDiagnosticsCheck
            {
                Name = "Runtime base path",
                Status = Directory.Exists(_paths.RuntimeBasePath) ? DiagnosticsCheckStatus.Pass : DiagnosticsCheckStatus.Fail,
                Detail = _paths.RuntimeBasePath
            });
            group.Checks.Add(new SystemDiagnosticsCheck
            {
                Name = "Content root path",
                Status = Directory.Exists(_paths.ContentRootPath) ? DiagnosticsCheckStatus.Pass : DiagnosticsCheckStatus.Fail,
                Detail = _paths.ContentRootPath
            });
            return group;
        }

        private SystemDiagnosticsCheck CheckWritableDirectory(string name, string directoryPath)
        {
            try
            {
                Directory.CreateDirectory(directoryPath);
                var probePath = Path.Combine(directoryPath, $"diagnostics-{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probePath, "ok");
                var probeContent = File.ReadAllText(probePath);
                File.Delete(probePath);

                return new SystemDiagnosticsCheck
                {
                    Name = name,
                    Status = probeContent == "ok" ? DiagnosticsCheckStatus.Pass : DiagnosticsCheckStatus.Fail,
                    Detail = $"Writable: {directoryPath}"
                };
            }
            catch (Exception ex)
            {
                return new SystemDiagnosticsCheck
                {
                    Name = name,
                    Status = DiagnosticsCheckStatus.Fail,
                    Detail = $"{directoryPath}: {ex.Message}"
                };
            }
        }

        private string ResolveTessdataPath()
        {
            var directPath = Path.Combine(_paths.RuntimeBasePath, "tessdata");
            if (Directory.Exists(directPath))
            {
                return directPath;
            }

            return Path.Combine(_paths.ContentRootPath, "tessdata");
        }

        private static SystemDiagnosticsCheck Fail(string name, string detail)
        {
            return new SystemDiagnosticsCheck
            {
                Name = name,
                Status = DiagnosticsCheckStatus.Fail,
                Detail = detail
            };
        }
    }
}
