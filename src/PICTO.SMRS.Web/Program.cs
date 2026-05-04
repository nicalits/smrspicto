using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PICTO.SMRS.Web.Data;
using PICTO.SMRS.Web.Security;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddTokenProvider<DataProtectorTokenProvider<IdentityUser>>(TokenOptions.DefaultProvider)
    .AddTokenProvider<PhoneNumberTokenProvider<IdentityUser>>(TokenOptions.DefaultPhoneProvider)
    .AddTokenProvider<EmailTokenProvider<IdentityUser>>(TokenOptions.DefaultEmailProvider)
    .AddDefaultUI();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy(SmrsPolicies.OverviewAccess, policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.Identity?.IsAuthenticated == true
            && !ctx.User.IsInRole(SmrsRoles.Employee)));
    options.AddPolicy(SmrsPolicies.UserManagement, policy =>
        policy.RequireRole(
            SmrsRoles.DepartmentHead));
    options.AddPolicy(SmrsPolicies.RequisitionApproval, policy =>
        policy.RequireRole(
            SmrsRoles.DepartmentHead,
            SmrsRoles.ItDivisionHead,
            SmrsRoles.OfficeDivisionHead));
    options.AddPolicy(SmrsPolicies.RequisitionChecker, policy =>
        policy.RequireRole(SmrsRoles.Encoder, SmrsRoles.DepartmentHead));
    options.AddPolicy(SmrsPolicies.BorrowApproval, policy =>
        policy.RequireRole(SmrsRoles.Encoder, SmrsRoles.DepartmentHead));
    options.AddPolicy(SmrsPolicies.InventoryAccess, policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.Identity?.IsAuthenticated == true
            && !ctx.User.IsInRole(SmrsRoles.Employee)));
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
});

var app = builder.Build();

// Keep schema in sync with the current EF model before handling requests.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.ExecuteSqlRawAsync("""
        IF COL_LENGTH('InventoryItems', 'UnitPrice') IS NULL
        BEGIN
            ALTER TABLE [InventoryItems]
            ADD [UnitPrice] decimal(18,2) NOT NULL CONSTRAINT [DF_InventoryItems_UnitPrice] DEFAULT (0);
        END
        """);
    await db.Database.MigrateAsync();
    await db.Database.ExecuteSqlRawAsync("""
        IF COL_LENGTH('InventoryItems', 'ReservedQuantity') IS NOT NULL
        BEGIN
            DECLARE @constraintName sysname;

            SELECT @constraintName = dc.name
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
            INNER JOIN sys.tables t ON t.object_id = c.object_id
            WHERE t.name = 'InventoryItems'
                AND c.name = 'ReservedQuantity';

            IF @constraintName IS NOT NULL
                EXEC('ALTER TABLE [InventoryItems] DROP CONSTRAINT [' + @constraintName + ']');

            ALTER TABLE [InventoryItems] DROP COLUMN [ReservedQuantity];
        END
        """);
}

await IdentitySeeder.SeedAsync(app.Services, app.Configuration, app.Logger);

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
// wwwroot must be served before authorization: FallbackPolicy requires auth for all endpoints,
// and MapStaticAssets is endpoint-based, so CSS/JS would otherwise fail for anonymous users (e.g. login).
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.Use(async (context, next) =>
{
    var v = context.Request.Path.Value;
    if (v is not null)
    {
        var blockManage2Fa = v.StartsWith("/Identity/Account/Manage/", StringComparison.OrdinalIgnoreCase)
            && (v.Contains("TwoFactorAuthentication", StringComparison.OrdinalIgnoreCase)
                || v.Contains("EnableAuthenticator", StringComparison.OrdinalIgnoreCase)
                || v.Contains("Disable2fa", StringComparison.OrdinalIgnoreCase)
                || v.Contains("GenerateRecoveryCodes", StringComparison.OrdinalIgnoreCase)
                || v.Contains("ResetAuthenticator", StringComparison.OrdinalIgnoreCase)
                || v.Contains("ShowRecoveryCodes", StringComparison.OrdinalIgnoreCase));
        var blockLogin2Fa = v.StartsWith("/Identity/Account/LoginWith2fa", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("/Identity/Account/LoginWithRecoveryCode", StringComparison.OrdinalIgnoreCase);
        if (blockManage2Fa || blockLogin2Fa)
        {
            if (context.User.Identity?.IsAuthenticated == true)
                context.Response.Redirect("/Identity/Account/Manage/Index");
            else
                context.Response.Redirect("/Identity/Account/Login");
            return;
        }
    }

    await next();
});
app.UseAuthorization();

app.MapStaticAssets().Add(staticEndpoint =>
    staticEndpoint.Metadata.Add(new AllowAnonymousAttribute()));

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.Run();
