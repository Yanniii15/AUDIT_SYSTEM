using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 31))));

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
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
        db.Database.ExecuteSqlRaw("ALTER TABLE AuditItems MODIFY COLUMN Status varchar(50) NOT NULL;");
        db.Database.ExecuteSqlRaw("UPDATE AuditItems SET Status = 'AwaitingBranchVerification' WHERE Status = 'Pending';");
        db.Database.ExecuteSqlRaw("UPDATE AuditItems SET Status = 'AwaitingBranchVerification' WHERE Status = 'AwaitingBranchVerifi';");
        db.Database.ExecuteSqlRaw("UPDATE AuditItems SET Status = 'AwaitingManagerApproval' WHERE Status = 'AwaitingManagerAppro';");
        db.Database.ExecuteSqlRaw("UPDATE AuditItems SET ReceiptImageUrl = REPLACE(ReceiptImageUrl, '/uploads/', '/Audits/Receipt/') WHERE ReceiptImageUrl LIKE '/uploads/%';");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database status cleanup failed: {ex.Message}");
    }

    try
    {
        DbSeeder.Seed(db);
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
app.UseStaticFiles();
app.UseSession();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
