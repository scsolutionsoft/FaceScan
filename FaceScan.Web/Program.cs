using FaceScan.Web.Data;
using FaceScan.Web.Helpers;
using FaceScan.Web.Helpers.Logging;
using FaceScan.Web.Hubs;
using FaceScan.Web.Models.Entities;
using FaceScan.Web.Models.Options;
using FaceScan.Web.Repositories;
using FaceScan.Web.Repositories.Interfaces;
using FaceScan.Web.Services;
using FaceScan.Web.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var logsPath = Path.Combine(builder.Environment.ContentRootPath, "Logs");
Directory.CreateDirectory(logsPath);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddProvider(new SimpleFileLoggerProvider(logsPath));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.CommandTimeout(60);
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        });
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.User.RequireUniqueEmail = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

builder.Services.Configure<FaceRecognitionOptions>(builder.Configuration.GetSection("FaceRecognition"));
builder.Services.Configure<UploadSettings>(builder.Configuration.GetSection("UploadSettings"));
builder.Services.Configure<AttendanceSettings>(builder.Configuration.GetSection("AttendanceSettings"));

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddSignalR();
builder.Services.AddHttpClient("IpCamera")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // Accept self-signed certs common on NVR/DVR firmware
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    })
    .SetHandlerLifetime(TimeSpan.FromMinutes(5));

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IStudentRepository, StudentRepository>();

builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IStudentImportService, StudentImportService>();
builder.Services.AddScoped<MockFaceRecognitionService>();
builder.Services.AddScoped<DlibFaceRecognitionService>();
builder.Services.AddScoped<MultiEngineFacePythonClient>();
builder.Services.AddScoped<HybridFaceRecognitionService>();
builder.Services.AddScoped<IFaceRecognitionServiceResolver, FaceRecognitionServiceResolver>();
builder.Services.AddScoped<IFaceRecognitionService>(serviceProvider =>
    serviceProvider.GetRequiredService<IFaceRecognitionServiceResolver>().GetDefaultService());
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IAttendanceReportService, AttendanceReportService>();
builder.Services.AddScoped<ITeacherAttendanceService, TeacherAttendanceService>();
builder.Services.AddScoped<ITeacherAttendanceReportService, TeacherAttendanceReportService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();
builder.Services.AddScoped<IScanDeviceLookupService, ScanDeviceLookupService>();
builder.Services.AddScoped<IOnvifCameraService, OnvifCameraService>();
builder.Services.AddSingleton<IFaceRecognitionLoadGate, FaceRecognitionLoadGate>();
builder.Services.AddSingleton<IPeriodAttendancePdfExportService, PeriodAttendancePdfExportService>();
builder.Services.AddSingleton<IQueuedScanVerifyService, QueuedScanVerifyService>();
builder.Services.AddHostedService(sp => (QueuedScanVerifyService)sp.GetRequiredService<IQueuedScanVerifyService>());
builder.Services.AddHostedService<DebugSnapshotRetentionService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStatusCodePages(statusContext =>
{
    var context = statusContext.HttpContext;
    var response = context.Response;
    var requestPath = context.Request.Path;

    if (response.StatusCode == StatusCodes.Status401Unauthorized)
    {
        var returnUrl = Uri.EscapeDataString($"{context.Request.Path}{context.Request.QueryString}");
        response.Redirect($"/Account/Login?returnUrl={returnUrl}");
        return Task.CompletedTask;
    }

    if (response.StatusCode == StatusCodes.Status403Forbidden)
    {
        response.Redirect("/Account/AccessDenied");
        return Task.CompletedTask;
    }

    if (response.StatusCode == StatusCodes.Status404NotFound &&
        !requestPath.StartsWithSegments("/Home/Error"))
    {
        response.Redirect("/Home/Error");
    }

    return Task.CompletedTask;
});
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapHub<ScanLiveHub>("/hubs/scan-live");

await DbInitializer.SeedAsync(app.Services);

app.Run();
