using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using ParentalControl.WebService.Data;
using ParentalControl.WebService.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Parental Control Web Service");

    builder.Host.UseSerilog();

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
        throw new InvalidOperationException("Database connection string is not configured");

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString)
            .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
            .EnableDetailedErrors(builder.Environment.IsDevelopment()));

    builder.Services.AddScoped<ITimeCalculationService, TimeCalculationService>();
    builder.Services.AddScoped<IDatabaseInitializationService, DatabaseInitializationService>();
    builder.Services.AddScoped<IUsageReportService, UsageReportService>();
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddHttpClient();
    builder.Services.AddScoped(sp => 
    {
        var navigationManager = sp.GetRequiredService<NavigationManager>();
        return new HttpClient { BaseAddress = new Uri(navigationManager.BaseUri) };
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Parental Control API", Version = "v1" });
    });

    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();
    
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromHours(8);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "database");

    var app = builder.Build();

    // Apply migrations on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbInit = scope.ServiceProvider.GetRequiredService<IDatabaseInitializationService>();
        try
        {
            var canConnect = await db.Database.CanConnectAsync();
            if (!canConnect)
            {
                Log.Warning("Cannot connect to database. Connection string: {ConnectionString}", 
                    connectionString.Replace(builder.Configuration["DbPassword"] ?? "", "***"));
            }
            else
            {
                var isInitialized = await dbInit.IsDatabaseInitializedAsync();
                if (!isInitialized)
                {
                    Log.Warning("Database not initialized. Visit /setup to initialize.");
                }
                else
                {
                    await db.Database.MigrateAsync();
                    Log.Information("Database migrations applied successfully");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Database check failed. Connection string: {ConnectionString}", 
                connectionString.Replace(builder.Configuration["DbPassword"] ?? "", "***"));
        }
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseSession();

    app.MapControllers();
    app.MapBlazorHub();
    app.MapFallbackToPage("/_Host");

    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
