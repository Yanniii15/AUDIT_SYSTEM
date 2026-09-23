using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 31))));

var jwtKey = builder.Configuration["JwtSettings:SecretKey"] ?? "AuditCkDayo_SuperSecret_Jwt_Security_Key_For_Mobile_2026_CkrDayo_Secure";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "AuditCkDayo";
builder.Services.AddSingleton(new AuditCkDayo.Services.JwtTokenService(jwtKey, jwtIssuer));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    })
    .AddJwtBearer(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtIssuer,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMobile", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddScoped<AuditCkDayo.Services.GoogleGeminiOcrService>();
builder.Services.AddScoped<AuditCkDayo.Services.IOcrService, AuditCkDayo.Services.FallbackOcrService>();
builder.Services.AddSingleton<AuditCkDayo.Services.IDiagnosticsPathProvider, AuditCkDayo.Services.AppDiagnosticsPathProvider>();
builder.Services.AddScoped<AuditCkDayo.Services.SystemDiagnosticsService>();
builder.Services.AddScoped<AuditCkDayo.Services.VoiceBiService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<AuditCkDayo.Services.CoverageService>();
builder.Services.AddScoped<AuditCkDayo.Services.SharedPcfFundService>();
builder.Services.AddScoped<AuditCkDayo.Services.ITreasuryAudioExportService, AuditCkDayo.Services.TreasuryAudioExportService>();
builder.Services.AddScoped<AuditCkDayo.Services.IDepositSlipOcrService, AuditCkDayo.Services.TesseractOcrService>();

var app = builder.Build();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database migration failed: {ex.Message}");
    }

    try
    {
        db.Database.ExecuteSqlRaw("UPDATE AuditItems SET Status = 'AwaitingBranchVerification' WHERE Status = '' OR Status IS NULL;");
        db.Database.ExecuteSqlRaw("ALTER TABLE AuditItems MODIFY COLUMN Status varchar(50) NOT NULL DEFAULT 'AwaitingBranchVerification';");
        db.Database.ExecuteSqlRaw("UPDATE AuditItems SET Status = 'AwaitingBranchVerification' WHERE Status = 'Pending';");
        db.Database.ExecuteSqlRaw("UPDATE AuditItems SET Status = 'AwaitingBranchVerification' WHERE Status = 'AwaitingBranchVerifi';");
        db.Database.ExecuteSqlRaw("UPDATE AuditItems SET Status = 'AwaitingManagerApproval' WHERE Status = 'AwaitingManagerAppro';");
        db.Database.ExecuteSqlRaw("UPDATE AuditItems SET Status = 'Approved' WHERE Notes LIKE '%August%' OR Description LIKE '%August%';");
        db.Database.ExecuteSqlRaw("UPDATE AuditItemDetails SET BranchVerificationStatus = 'Verified' WHERE AuditItemId IN (SELECT Id FROM AuditItems WHERE Notes LIKE '%August%' OR Description LIKE '%August%');");
        db.Database.ExecuteSqlRaw("UPDATE AuditItems SET ReceiptImageUrl = REPLACE(ReceiptImageUrl, '/uploads/', '/Audits/Receipt/') WHERE ReceiptImageUrl LIKE '/uploads/%';");
        db.Database.ExecuteSqlRaw("ALTER TABLE AuditItemDetails MODIFY COLUMN Quantity decimal(12,3) NOT NULL DEFAULT 1.000;");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database status cleanup failed: {ex.Message}");
    }

    var depositColumns = new (string Table, string Column, string Definition)[]
    {
        ("SalesReports", "DepositSlipImageUrl", "varchar(255) NULL"),
        ("SalesReports", "DepositedAmount", "decimal(12,2) NULL"),
        ("SalesReports", "DepositBankName", "varchar(100) NULL"),
        ("SalesReports", "DepositReferenceNumber", "varchar(100) NULL"),
        ("SalesReports", "DepositDate", "datetime NULL"),
        ("SalesReports", "DepositVarianceReason", "varchar(500) NULL"),
        ("SalesReports", "DepositUploadedByUserId", "int NULL"),
        ("SalesReports", "DepositUploadedAt", "datetime NULL"),
        ("SalesReports", "OpeningDepositSlipImageUrl", "varchar(255) NULL"),
        ("SalesReports", "OpeningDepositedAmount", "decimal(12,2) NULL"),
        ("SalesReports", "OpeningDepositBankName", "varchar(100) NULL"),
        ("SalesReports", "OpeningDepositReferenceNumber", "varchar(100) NULL"),
        ("SalesReports", "OpeningDepositDate", "datetime NULL"),
        ("SalesReports", "OpeningDepositVarianceReason", "varchar(500) NULL"),
        ("SalesReports", "OpeningDepositUploadedByUserId", "int NULL"),
        ("SalesReports", "OpeningDepositUploadedAt", "datetime NULL"),
        ("SalesReports", "ClosingDepositSlipImageUrl", "varchar(255) NULL"),
        ("SalesReports", "ClosingDepositedAmount", "decimal(12,2) NULL"),
        ("SalesReports", "ClosingDepositBankName", "varchar(100) NULL"),
        ("SalesReports", "ClosingDepositReferenceNumber", "varchar(100) NULL"),
        ("SalesReports", "ClosingDepositDate", "datetime NULL"),
        ("SalesReports", "ClosingDepositVarianceReason", "varchar(500) NULL"),
        ("SalesReports", "ClosingDepositUploadedByUserId", "int NULL"),
        ("SalesReports", "ClosingDepositUploadedAt", "datetime NULL"),
        ("CashFlowEntries", "SalesReportId", "int NULL")
    };

    foreach (var (table, column, definition) in depositColumns)
    {
        try
        {
            db.Database.ExecuteSqlRaw($"ALTER TABLE `{table}` ADD COLUMN `{column}` {definition};");
        }
        catch
        {
            // Column already exists, safe to ignore in MySQL
        }
    }

    try
    {
        var sqlPath = Path.Combine(AppContext.BaseDirectory, "insert_buyer_expenses.sql");
        if (File.Exists(sqlPath))
        {
            var alreadyImported = db.AuditItems.Any(a => a.Notes == "Imported from August 2026 Buyer Expense Sheets");
            if (!alreadyImported)
            {
                Console.WriteLine("[IMPORT] Importing August buyer expenses from sheets...");
                var sql = File.ReadAllText(sqlPath);
                db.Database.ExecuteSqlRaw(sql);
                Console.WriteLine("[IMPORT] Successfully imported 363 August buyer expense items!");
            }
            else
            {
                Console.WriteLine("[IMPORT] August buyer expenses already imported. Skipping.");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[IMPORT] Buyer expenses import failed: {ex.Message}");
    }

    try
    {
        DbSeeder.Seed(db, app.Environment.IsDevelopment());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database seeding failed: {ex.Message}");
    }
}
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".apk"] = "application/vnd.android.package-archive";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider
});
app.UseSession();
app.UseRouting();
app.UseCors("AllowMobile");
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();