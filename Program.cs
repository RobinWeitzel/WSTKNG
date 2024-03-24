using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore;
using Hangfire;
using Hangfire.SqlServer;
using WSTKNG.Models;
using Hangfire.Storage.SQLite;
using WSTKNG.Services;
using Hangfire.Console;
using Hangfire.Console.Extensions;
using WSTKNG.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSignalR();

builder.Services.AddDbContext<ApplicationContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("WSTKNGContextSQLite")));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddHangfire(configuration =>
    configuration
       .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
       .UseSimpleAssemblyNameTypeSerializer()
       .UseRecommendedSerializerSettings()
       .UseSQLiteStorage(builder.Configuration.GetConnectionString("HangfireContextSQLite"))
        .UseConsole());

builder.Services.AddHangfireConsoleExtensions();

builder.Services.AddHangfireServer();

builder.Services.AddTransient<IEmailService, EmailService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var context = services.GetRequiredService<ApplicationContext>();
    context.Database.EnsureCreated();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new MyAuthorizationFilter() }
});

app.UseAuthorization();

app.MapRazorPages();
app.MapHub<NotificationHub>("/notificationHub");

RecurringJob.AddOrUpdate<Crawler>("crawlTOC", c => c.CheckTOC(null, null), "0 */4 * * *");
RecurringJob.AddOrUpdate<Crawler>("crawlChapters", c => c.ScheduledCrawl(null), "15 */4 * * *");
RecurringJob.AddOrUpdate<Crawler>("sendEmails", c => c.ScheduledEmail(null), "30 */4 * * *");

app.Run();