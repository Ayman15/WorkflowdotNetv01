using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection; // for CreateScope()
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;                // for OpenApiInfo
using Quartz;
using Quartz.Impl;
using OptimaJet.Workflow.Core.Runtime;
using System.Reflection.Metadata;
// using WorkflowLib; // <-- UNCOMMENT if your WorkflowInit lives there

var builder = WebApplication.CreateBuilder(args);

// ------------------------ Logging ------------------------
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

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

// JSON + enum-as-string
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SimpleWF Approval API", Version = "v1" });
});

// HttpClient for remote runner
builder.Services.AddHttpClient();

// simple JSON-lines audit logger to a file
builder.Services.AddSingleton<IAuditLogger>(sp =>
{
    var cfg = sp.GetRequiredService<IOptions<ApprovalsOptions>>().Value;
    var baseDir = AppContext.BaseDirectory;
    var path = string.IsNullOrWhiteSpace(cfg.LogFilePath)
        ? Path.Combine(baseDir, "approvals_audit.log")
        : (Path.IsPathRooted(cfg.LogFilePath) ? cfg.LogFilePath : Path.Combine(baseDir, cfg.LogFilePath));
    return new FileAuditLogger(path, sp.GetRequiredService<ILogger<FileAuditLogger>>());
});

var app = builder.Build();

// Swagger UI (Development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SimpleWF Approval API v1");
        c.RoutePrefix = ""; // open at root (http://localhost:XXXX/)
    });
}

app.MapControllers();

// Root “health” endpoint (hidden from Swagger)
app.MapGet("/", () => "SimpleWF Approval API running.").ExcludeFromDescription();

app.Logger.LogInformation("Environment: {env}", app.Environment.EnvironmentName);

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
    public string BaseUrl { get; set; } = "";           // e.g., http://localhost:5062/approvals
    public string ApproverEmails { get; set; } = "";
    public string? ExePath { get; set; }                // local exe path, e.g., C:\Ops\RunWellsIntegrator.exe
    public string? LogFilePath { get; set; }            // NDJSON log file (relative or absolute)

    // Remote runner settings
    public string? RemoteRunnerUrl { get; set; }        // e.g., http://server:5005  (remote service base)
    public string? RemoteRunnerApiKey { get; set; }     // optional shared-secret header X-API-KEY
    public int RemoteRunnerTimeoutSeconds { get; set; } = 60;
    public string? RemoteExePath { get; set; }          // optional: path/name known to remote runner; otherwise it can ignore
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

// ======================== Simple Audit Logger ========================
public interface IAuditLogger
{
    Task Info(string evt, object? data = null);
    Task Error(string evt, Exception ex, object? data = null);
}

public class FileAuditLogger : IAuditLogger
{
    private readonly string _path;
    private readonly ILogger<FileAuditLogger> _logger;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public FileAuditLogger(string path, ILogger<FileAuditLogger> logger)
    {
        _path = path;
        _logger = logger;
    }

    public Task Info(string evt, object? data = null) => Write("INFO", evt, data, null);
    public Task Error(string evt, Exception ex, object? data = null) => Write("ERROR", evt, data, ex);

    private Task Write(string level, string evt, object? data, Exception? ex)
    {
        try
        {
            var line = JsonSerializer.Serialize(new
            {
                ts = DateTimeOffset.Now.ToString("O"),
                level,
                evt,
                data,
                exception = ex?.ToString()
            }, _json);

            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            File.AppendAllText(_path, line + Environment.NewLine);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to write audit log line for event {Event}", evt);
        }
        return Task.CompletedTask;
    }
}

// ======================== Quartz job that sends email & starts workflow ========================
public class DailyJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var sp = (IServiceProvider)context.MergedJobDataMap["ServiceProvider"];
        using var scope = sp.CreateScope();
        var wellsProvider = scope.ServiceProvider.GetRequiredService<IWellsProvider>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var opt = scope.ServiceProvider.GetRequiredService<IOptions<ApprovalsOptions>>().Value;
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
        var log = scope.ServiceProvider.GetRequiredService<ILogger<DailyJob>>();

        try
        {
            var wells = await wellsProvider.GetNewWellsAsync();
            var processId = Guid.NewGuid();

            // Build email HTML
            string HtmlTable(IEnumerable<Well> list)
            {
                var rows = string.Join("", list.Select(w =>
                    $"<tr><td>{WebUtility.HtmlEncode(w.Name)}</td><td>{WebUtility.HtmlEncode(w.Type)}</td><td>{WebUtility.HtmlEncode(w.Description)}</td></tr>"));
                return $"<table border='1' cellpadding='6' cellspacing='0'><thead><tr><th>Name</th><th>Type</th><th>Description</th></tr></thead><tbody>{rows}</tbody></table>";
            }

            // Approval links
            var approveUrl = $"{opt.BaseUrl}/Decide_By_Email?pid={processId}&decision=approve";
            var rejectUrl = $"{opt.BaseUrl}/Decide_By_Email?pid={processId}&decision=reject";

            var htmlBody =
                "<h2>New Wells Pending Approval</h2>" +
                HtmlTable(wells) +
                "<div style='margin-top:16px'>" +
                $"<a href='{approveUrl}'>Approve</a> | <a href='{rejectUrl}'>Reject</a>" +
                "</div>";

            var recipients = (opt.ApproverEmails ?? "ops1@company.com;ops2@company.com")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            await email.SendAsync(recipients, $"New Wells Approval – {DateTime.Now:yyyy-MM-dd}", htmlBody);
            log.LogInformation("Email sent for process {ProcessId} to {Recipients}", processId, recipients);
            await audit.Info("email_sent", new { processId, recipients, wellsCount = wells.Count });

            // Create and start workflow "SimpleWF"
            WorkflowInit.Runtime.CreateInstance(new CreateInstanceParams("SimpleWF", processId));
            var startCmd = WorkflowInit.Runtime
                .GetAvailableCommands(processId, string.Empty)
                .First(c => c.CommandName.Equals("Start", StringComparison.OrdinalIgnoreCase));
            WorkflowInit.Runtime.ExecuteCommand(startCmd, string.Empty, string.Empty);

            log.LogInformation("Workflow started: scheme={Scheme} pid={ProcessId}", "SimpleWF", processId);
            await audit.Info("workflow_started", new { scheme = "SimpleWF", processId, wells });
        }
        catch (Exception ex)
        {
            log.LogError(ex, "DailyJob failed.");
            await audit.Error("daily_job_failed", ex);
            throw;
        }
    }
}

// ======================== REQUEST TYPES ========================
public enum ApprovalDecision { Approve, Reject }
public record ApprovalDecisionRequest(Guid pid, ApprovalDecision decision);

// ======================== CONTROLLER (Approvals APIs) ========================
[ApiController]
[Route("approvals")]
public class ApprovalsController : ControllerBase
{
    private readonly ApprovalsOptions _opt;
    private readonly IAuditLogger _audit;
    private readonly ILogger<ApprovalsController> _log;
    private readonly IHostEnvironment _env;
    private readonly IHttpClientFactory _http;

    public ApprovalsController(
        IOptions<ApprovalsOptions> opt,
        IAuditLogger audit,
        ILogger<ApprovalsController> log,
        IHostEnvironment env,
        IHttpClientFactory http)
    {
        _opt = opt.Value;
        _audit = audit;
        _log = log;
        _env = env;
        _http = http;
    }

    [HttpGet("Available_Commands")]
    public IActionResult Commands([FromQuery] Guid pid)
    {
        if (pid == Guid.Empty) return BadRequest("Missing or invalid pid.");
        var cmds = WorkflowInit.Runtime
            .GetAvailableCommands(pid, string.Empty)
            .Select(c => c.CommandName)
            .ToArray();

        _log.LogInformation("Available commands for pid={Pid}: {Commands}", pid, cmds);
        _ = _audit.Info("commands_listed", new { pid, cmds });
        return Ok(cmds);
    }

    [HttpGet("Decide_By_Email")]
    public Task<IActionResult> DecideByEmail([FromQuery] Guid pid, [FromQuery] string decision)
        => ExecuteDecisionAsync(pid, decision);

    [HttpPost("Decide_By_Manual_Entry")]
    public Task<IActionResult> DecidePost([FromBody] ApprovalDecisionRequest dto)
        => ExecuteDecisionAsync(dto.pid, dto.decision.ToString());

    [HttpPost("Approve_Button")]
    public Task<IActionResult> Approve([FromQuery] Guid pid)
        => ExecuteDecisionAsync(pid, "approve");

    [HttpPost("Reject_Button")]
    public Task<IActionResult> Reject([FromQuery] Guid pid)
        => ExecuteDecisionAsync(pid, "reject");

    // ---------- Approve triggers BOTH: local EXE and remote web service ----------
    private async Task<IActionResult> ExecuteDecisionAsync(Guid pid, string? decision)
    {
        if (pid == Guid.Empty)
            return BadRequest("Missing or invalid pid.");

        var wanted = decision?.Equals("approve", StringComparison.OrdinalIgnoreCase) == true
            ? "Approve"
            : "Reject";

        try
        {
            _log.LogInformation("Decision request: pid={Pid}, decision={Decision}, env={Env}", pid, wanted, _env.EnvironmentName);
            _ = _audit.Info("decision_request", new { pid, decision = wanted, env = _env.EnvironmentName });

            var cmd = WorkflowInit.Runtime
                .GetAvailableCommands(pid, string.Empty)
                .FirstOrDefault(c => c.CommandName.Equals(wanted, StringComparison.OrdinalIgnoreCase));

            if (cmd == null)
            {
                _log.LogWarning("Command not available: pid={Pid}, wanted={Wanted}", pid, wanted);
                _ = _audit.Info("command_unavailable", new { pid, wanted });
                return BadRequest($"Command '{wanted}' not available for processId {pid}.");
            }

            WorkflowInit.Runtime.ExecuteCommand(cmd, string.Empty, string.Empty);

            _log.LogInformation("Command executed: pid={Pid}, command={Command}", pid, wanted);
            _ = _audit.Info("command_executed", new { pid, command = wanted });

            if (wanted == "Approve")
            {
                // Run both in parallel (fire both tasks and await)
                var tasks = new List<Task>
                {
                    RunLocalExeAsync(pid),
                    InvokeRemoteRunnerAsync(pid)
                };
                await Task.WhenAll(tasks);
            }

            return Ok($"Decision recorded as {wanted.ToUpperInvariant()}.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ExecuteDecision failed: pid={Pid}, decision={Decision}", pid, wanted);
            _ = _audit.Error("decision_failed", ex, new { pid, wanted });
            return StatusCode(500, "Internal error while executing decision.");
        }
    }

    // ---------- Local EXE ----------
    private Task RunLocalExeAsync(Guid pid)
    {
        var exe = _opt.ExePath;
        if (string.IsNullOrWhiteSpace(exe) || !System.IO.File.Exists(exe))
        {
            _log.LogWarning("Local EXE not found or path not set. ExePath={ExePath}", exe ?? "(null)");
            _ = _audit.Info("exe_skipped_local_missing", new { pid, exePath = exe });
            return Task.CompletedTask;
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(exe, $"--processId {pid}")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            System.Diagnostics.Process.Start(psi);

            _log.LogInformation("Local EXE started: {Exe} --processId {Pid}", exe, pid);
            _ = _audit.Info("exe_started_local", new { pid, exe });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Local EXE start failed: {Exe}", exe);
            _ = _audit.Error("exe_local_exception", ex, new { pid, exe });
        }

        return Task.CompletedTask;
    }

    // ---------- Remote runner via web service ----------
    private async Task InvokeRemoteRunnerAsync(Guid pid)
    {
        if (string.IsNullOrWhiteSpace(_opt.RemoteRunnerUrl))
        {
            _log.LogWarning("Remote runner URL not configured. Skipping remote run.");
            _ = _audit.Info("remote_skipped_unconfigured", new { pid });
            return;
        }

        var http = _http.CreateClient();
        var url = _opt.RemoteRunnerUrl!.TrimEnd('/') + "/run";

        var payload = new
        {
            exePath = _opt.RemoteExePath,       // optional; remote service may ignore and use its own mapping
            args = $"--processId {pid}",
            processId = pid.ToString()
        };

        var json = JsonSerializer.Serialize(payload);
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(_opt.RemoteRunnerApiKey))
            req.Headers.Add("X-API-KEY", _opt.RemoteRunnerApiKey);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, _opt.RemoteRunnerTimeoutSeconds)));

        try
        {
            var res = await http.SendAsync(req, cts.Token);
            var body = await res.Content.ReadAsStringAsync(cts.Token);

            if (res.IsSuccessStatusCode)
            {
                _log.LogInformation("Remote run OK. Url={Url} Response={Body}", url, Truncate(body, 1000));
                _ = _audit.Info("exe_started_remote", new { pid, url, response = Truncate(body, 2000) });
            }
            else
            {
                _log.LogError("Remote run FAILED ({Status}). Url={Url} Body={Body}", (int)res.StatusCode, url, Truncate(body, 1000));
                _ = _audit.Error("exe_remote_failed",
                    new Exception($"HTTP {(int)res.StatusCode}"),
                    new { pid, url, response = Truncate(body, 2000) });
            }
        }
        catch (TaskCanceledException)
        {
            _log.LogError("Remote run TIMEOUT after {s}s. Url={Url}", _opt.RemoteRunnerTimeoutSeconds, url);
            _ = _audit.Error("exe_remote_timeout", new TimeoutException(), new { pid, url });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Remote run EXCEPTION. Url={Url}", url);
            _ = _audit.Error("exe_remote_exception", ex, new { pid, url });
        }
    }

    private static string? Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
}

