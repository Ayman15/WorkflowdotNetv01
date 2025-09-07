using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using RemoteRunner;
using RemoteRunner.Contracts;
using RemoteRunner.Utilities;
// If (and only if) you keep the line builder.Host.UseWindowsService():
// using Microsoft.Extensions.Hosting.WindowsServices;

var builder = WebApplication.CreateBuilder(args);

// ===== Logging =====
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ===== Bind options =====
builder.Services
    .AddOptions<RunnerOptions>()
    .Bind(builder.Configuration.GetSection("Runner"))
    .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "Runner:ApiKey is required.")
    .Validate(o => o.AllowedExecutables?.Count > 0, "Runner:AllowedExecutables must include at least one entry.")
    .ValidateOnStart();

// ===== Controllers / JSON =====
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ===== Swagger (dev only) =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Remote Runner API", Version = "v1" });
});

// Optional: only if you plan to install as Windows Service (otherwise remove this line)
// builder.Host.UseWindowsService();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Remote Runner API v1");
        c.RoutePrefix = "";
    });
}

app.MapGet("/health", () => Results.Ok(new { ok = true, env = app.Environment.EnvironmentName }))
   .ExcludeFromDescription();

// ====== Endpoint: POST /run ======
app.MapPost("/run", async (
    [FromBody] RunRequest req,
    HttpRequest http,
    IOptions<RunnerOptions> opts,
    ILoggerFactory lf) =>
{
    var log = lf.CreateLogger("RemoteRunner");
    var cfg = opts.Value;

    // --- API key check ---
    if (!http.Headers.TryGetValue("X-API-KEY", out var key) || key != cfg.ApiKey)
        return Results.Unauthorized();

    // --- Resolve executable path from allowlist ---
    string resolvedPath;
    if (!string.IsNullOrWhiteSpace(req.exeName))
    {
        if (!cfg.AllowedExecutables.TryGetValue(req.exeName, out resolvedPath!))
            return Results.BadRequest($"Unknown exeName '{req.exeName}'.");
    }
    else if (!string.IsNullOrWhiteSpace(req.exePath))
    {
        var allowed = cfg.AllowedExecutables.Values.Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = Path.GetFullPath(req.exePath);
        if (!allowed.Contains(candidate))
            return Results.BadRequest("exePath is not in allowlist.");
        resolvedPath = candidate;
    }
    else
    {
        return Results.BadRequest("Provide either exeName or exePath.");
    }

    if (!File.Exists(resolvedPath))
        return Results.BadRequest($"Executable not found on server: {resolvedPath}");

    var wd = string.IsNullOrWhiteSpace(req.workingDirectory)
        ? (Path.GetDirectoryName(resolvedPath) ?? Environment.CurrentDirectory)
        : req.workingDirectory;

    var timeout = TimeSpan.FromSeconds(Math.Max(1, req.timeoutSeconds ?? cfg.DefaultTimeoutSeconds));

    var psi = new ProcessStartInfo(resolvedPath, req.args ?? "")
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = wd,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };

    log.LogInformation("Starting EXE: {Path} {Args} (pid={Pid})", resolvedPath, req.args, req.processId);

    var sw = Stopwatch.StartNew();
    try
    {
        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.Start();

        var waitTask = proc.WaitForExitAsync();
        var completed = await Task.WhenAny(waitTask, Task.Delay(timeout)) == waitTask;

        if (!completed)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            log.LogError("Timeout {Timeout}s for {Path}", timeout.TotalSeconds, resolvedPath);
            return Results.Ok(new RunResponse(
                started: true,
                exitCode: null,
                durationMs: sw.ElapsedMilliseconds,
                stdout: null,
                stderr: $"Timeout after {timeout.TotalSeconds} seconds.",
                resolvedExePath: resolvedPath
            ));
        }

        var exit = proc.ExitCode;
        string? stdout = null, stderr = null;

        try { stdout = StringUtil.Truncate(await proc.StandardOutput.ReadToEndAsync(), cfg.MaxStdCaptureChars); } catch { }
        try { stderr = StringUtil.Truncate(await proc.StandardError.ReadToEndAsync(), cfg.MaxStdCaptureChars); } catch { }

        sw.Stop();

        log.LogInformation("Completed exit={Exit} in {Ms}ms: {Path}", exit, sw.ElapsedMilliseconds, resolvedPath);

        return Results.Ok(new RunResponse(
            started: true,
            exitCode: exit,
            durationMs: sw.ElapsedMilliseconds,
            stdout: stdout,
            stderr: stderr,
            resolvedExePath: resolvedPath
        ));
    }
    catch (Exception ex)
    {
        sw.Stop();
        log.LogError(ex, "Exception running {Path}", resolvedPath);
        return Results.Problem($"Exception: {ex.Message}");
    }
})
.WithName("Run")
.Produces<RunResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized);

app.Run();
