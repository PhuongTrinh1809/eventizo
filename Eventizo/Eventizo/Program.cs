using Eventizo.Data;
using Eventizo.Helper;
using Eventizo.Middlewares;
using Eventizo.Hubs;
using Eventizo.Models;
using Eventizo.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Eventizo.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<PayOSService>();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repository
builder.Services.AddScoped<IProductRepository, EFProductRepository>();
builder.Services.AddScoped<ICategoryRepository, EFCategoryRepository>();
builder.Services.AddScoped<IEventRepository, EFEventRepository>();
builder.Services.AddScoped<IEventTypeRepository, EFEventTypeRepository>();
builder.Services.AddScoped<ICustomerRepository, EFCustomerRepository>();
builder.Services.AddScoped<PointService>();
builder.Services.AddScoped<EventReminderService>();
builder.Services.AddHostedService<ReminderBackgroundWorker>();

builder.Services.AddSingleton<BlockchainService>();

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

// Cookie Authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";

    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddRazorPages();
builder.Services.AddTransient<IEmailSender, EmailSender>();

// SignalR
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();

var app = builder.Build();

// --- Seed Roles & Admins ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        // Seed Roles
        await SeedData.SeedRolesAsync(services);

        // Seed Admin Users TMH1→TMH5
        await SeedData.SeedAdminUsersAsync(services);

        Console.WriteLine("Seeding Roles and Admins completed.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("Seeding error: " + ex.Message);
        if (ex.InnerException != null)
            Console.WriteLine("Inner Exception: " + ex.InnerException.Message);
    }
}

// --- Middleware pipeline ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseSession();

//app.UseMiddleware<WaitingRoomMiddleware>();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<SessionValidationMiddleware>();

app.MapRazorPages();

// Controller routes
app.MapControllerRoute(
    name: "Admin",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// SignalR hub
app.MapHub<ChatHub>("/chatHub");

app.Run();
