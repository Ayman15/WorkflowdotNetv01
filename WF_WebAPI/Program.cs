using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Quartz;
using Quartz.Impl;
using OptimaJet.Workflow.Core.Runtime;
//using WorkflowLib; // <-- UNCOMMENTED: your WorkflowInit lives here

var builder = WebApplication.CreateBuilder(args);

// ------------------------ WorkflowInit like console app ------------------------
var conn = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(conn))
{
    Console.WriteLine("ERROR: ConnectionStrings:DefaultConnection is missing in appsettings.json");
    Environment.Exit(1);
}
WorkflowInit.ConnectionString = conn;
// If your WorkflowInit has explicit init/start methods, call them here:
// WorkflowInit.Init();
// WorkflowInit.Runtime.Start();

// ------------------------ Services & Swagger ------------------------
builder.Services.Configure<ApprovalsOptions>(builder.Configuration.GetSection("Approvals"));

builder.Services.AddSingleton<IWellsProvider, MyWellsProvider>();
builder.Services.AddSingleton<IEmailSender, ConsoleEmailSender>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger UI (your launch profile opens /swagger)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/", () => "SimpleWF Approval API running.");

// ------------------------ Quartz Scheduler ------------------------
ISchedulerFactory schedFactory = new StdSchedulerFactory();
var scheduler = await schedFactory.GetScheduler();
await scheduler.Start();

var job = JobBuilder.Create<DailyJob>()
    .WithIdentity("DailySimpleWFJob", "Approvals")
    .UsingJobData(new JobDataMap { { "ServiceProvider", app.Services } })
    .Build();

TimeZoneInfo tz;
try { tz = TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
catch { tz = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }

// For quick testing, run every 60s in DEBUG; otherwise 07:00 daily
ITrigger trigger;
#if DEBUG
trigger = TriggerBuilder.Create()
    .WithIdentity("TestTrigger", "Approvals")
    .WithSimpleSchedule(x => x.WithIntervalInSeconds(60).RepeatForever())
    .StartNow()
    .Build();
#else
trigger = TriggerBuilder.Create()
    .WithIdentity("DailySimpleWFTrigger", "Approvals")
    .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(7, 0).InTimeZone(tz))
    .StartNow()
    .Build();
#endif

await scheduler.ScheduleJob(job, trigger);

app.Run();


// ======================== SUPPORTING CLASSES ========================

public class ApprovalsOptions
{
    public string BaseUrl { get; set; } = "";       // e.g., http://localhost:5062/approvals
    public string ApproverEmails { get; set; } = "";
}

public class Well
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
}

public interface IWellsProvider
{
    Task<List<Well>> GetNewWellsAsync();
}

public class MyWellsProvider : IWellsProvider
{
    public Task<List<Well>> GetNewWellsAsync() => Task.FromResult(new List<Well>
    {
        new() { Name = "Meliha-01", Type = "SRP", Description = "Sandface recompletion" },
        new() { Name = "Meliha-02", Type = "ESP", Description = "Workover complete" }
    });
}

public interface IEmailSender
{
    Task SendAsync(string[] to, string subject, string htmlBody);
}

// For testing: prints the email to console instead of sending
public class ConsoleEmailSender : IEmailSender
{
    public Task SendAsync(string[] to, string subject, string htmlBody)
    {
        Console.WriteLine("---- EMAIL ----");
        Console.WriteLine("To: " + string.Join(",", to));
        Console.WriteLine("Subject: " + subject);
        Console.WriteLine(htmlBody);
        Console.WriteLine("---------------");
        return Task.CompletedTask;
    }
}

// Quartz job that runs daily (or every 60s in DEBUG)
public class DailyJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var sp = (IServiceProvider)context.MergedJobDataMap["ServiceProvider"];
        using var scope = sp.CreateScope();
        var wellsProvider = scope.ServiceProvider.GetRequiredService<IWellsProvider>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var opt = scope.ServiceProvider.GetRequiredService<IOptions<ApprovalsOptions>>().Value;

        var wells = await wellsProvider.GetNewWellsAsync();
        var processId = Guid.NewGuid();

        // Build email HTML
        string HtmlTable(IEnumerable<Well> list)
        {
            var rows = string.Join("", list.Select(w =>
                $"<tr><td>{WebUtility.HtmlEncode(w.Name)}</td><td>{WebUtility.HtmlEncode(w.Type)}</td><td>{WebUtility.HtmlEncode(w.Description)}</td></tr>"));
            return $"<table border='1' cellpadding='6' cellspacing='0'><thead><tr><th>Name</th><th>Type</th><th>Description</th></tr></thead><tbody>{rows}</tbody></table>";
        }

        // Use BaseUrl from appsettings (must match your launch profile port, e.g., http://localhost:5062/approvals)
        var approveUrl = $"{opt.BaseUrl}/decide?pid={processId}&decision=approve";
        var rejectUrl = $"{opt.BaseUrl}/decide?pid={processId}&decision=reject";

        var htmlBody =
            "<h2>New Wells Pending Approval</h2>" +
            HtmlTable(wells) +
            "<div style='margin-top:16px'>" +
            $"<a href='{approveUrl}'>Approve</a> | <a href='{rejectUrl}'>Reject</a>" +
            "</div>";

        var recipients = (opt.ApproverEmails ?? "ops1@company.com;ops2@company.com")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        await email.SendAsync(recipients, $"New Wells Approval – {DateTime.Now:yyyy-MM-dd}", htmlBody);

        // Create and start workflow "SimpleWF"
        WorkflowInit.Runtime.CreateInstance(new CreateInstanceParams("SimpleWF", processId));
        var startCmd = WorkflowInit.Runtime
            .GetAvailableCommands(processId, string.Empty)
            .First(c => c.CommandName.Equals("Start", StringComparison.OrdinalIgnoreCase));
        WorkflowInit.Runtime.ExecuteCommand(startCmd, string.Empty, string.Empty);
    }
}

// Controller for Approve/Reject
[ApiController]
[Route("approvals")]
public class ApprovalsController : ControllerBase
{
    [HttpGet("decide")]
    public IActionResult Decide([FromQuery] Guid pid, [FromQuery] string decision)
    {
        var wanted = decision?.Equals("approve", StringComparison.OrdinalIgnoreCase) == true ? "Approve" : "Reject";

        var cmd = WorkflowInit.Runtime
            .GetAvailableCommands(pid, string.Empty)
            .FirstOrDefault(c => c.CommandName.Equals(wanted, StringComparison.OrdinalIgnoreCase));

        if (cmd == null)
            return BadRequest($"Command '{wanted}' not available.");

        WorkflowInit.Runtime.ExecuteCommand(cmd, string.Empty, string.Empty);

        if (wanted == "Approve")
        {
            // Run your remote app here (optional)
            var exe = @"C:\Ops\RunWellsIntegrator.exe";
            if (System.IO.File.Exists(exe))
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe, $"--processId {pid}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(psi);
            }
        }

        return Content($"Decision recorded as {wanted.ToUpperInvariant()}.");
    }
}
