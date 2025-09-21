using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OptimaJet.Workflow.Core.Runtime;

public static class ApprovalService
{
    public static async Task<Guid> StartAsync(
        string approverEmails, string subject, string body,
        string wellsJson, string approvalToken,
        string identity)
    {
        var processId = Guid.NewGuid();

        // ✅ Correct argument order: (schemeCode, processId)
        await WorkflowInit.Runtime.CreateInstanceAsync("SimpleWF", processId);

        // ✅ Persist initial parameters
        await SetParamAsync(processId, "ApproverEmails", approverEmails);
        await SetParamAsync(processId, "Subject", subject);
        await SetParamAsync(processId, "Body", body);
        await SetParamAsync(processId, "WellsJson", wellsJson);
        await SetParamAsync(processId, "ApprovalToken", approvalToken);

        // Your XML transitions Initial -> WaitingForDecision automatically, so nothing else to do here.
        // If you switch your first transition to a Start command, call:
        // await ExecuteCommandAsync(processId, "Start", identity);

        return processId;
    }

    public static async Task ApproveAndRunAsync(
        Guid processId,
        string approverIdentity,
        string exePath,
        string arguments = "",
        TimeSpan? killAfter = null,
        string workingDirectory = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(exePath))
            throw new FileNotFoundException($"EXE not found at '{exePath}'", exePath);

        await SetParamAsync(processId, "Decision", "Approve");

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments ?? "",
            WorkingDirectory =
                string.IsNullOrWhiteSpace(workingDirectory)
                    ? Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory
                    : workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.Start();

        string stdout = await proc.StandardOutput.ReadToEndAsync();
        string stderr = await proc.StandardError.ReadToEndAsync();

        var timeout = killAfter ?? TimeSpan.FromMinutes(10);
        var finished = await WaitForExitAsync(proc, timeout, ct);
        if (!finished)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            await SetParamAsync(processId, "Decision", $"Approve/EXE Timeout after {timeout}");
        }

        int exitCode = proc.HasExited ? proc.ExitCode : -9999;
        await SetParamAsync(processId, "ExeExitCode", exitCode.ToString());
        if (!string.IsNullOrWhiteSpace(stdout)) await SetParamAsync(processId, "ExeStdOut", Trunc(stdout, 4000));
        if (!string.IsNullOrWhiteSpace(stderr)) await SetParamAsync(processId, "ExeStdErr", Trunc(stderr, 4000));

        // ✅ Build command and execute with identity parameters
        await ExecuteCommandAsync(processId, "Approve", approverIdentity);
        // Your XML will go Approved -> Final (Auto).
    }

    public static async Task RejectAsync(Guid processId, string approverIdentity, string reason = null)
    {
        await SetParamAsync(processId, "Decision", string.IsNullOrWhiteSpace(reason) ? "Reject" : reason);
        await ExecuteCommandAsync(processId, "Reject", approverIdentity);
        // Your XML will go Rejected -> Final (Auto).
    }

    // ----------------- helpers -----------------

    private static async Task ExecuteCommandAsync(Guid processId, string commandName, string identityId)
    {
        var cmd = new WorkflowCommand
        {
            ProcessId = processId,
            CommandName = commandName
        };

        // Option A: single identity (no impersonation)
        await WorkflowInit.Runtime.ExecuteCommandAsync(cmd, identityId, impersonatedIdentityId: null, default, false);

        // Option B (alternative overload):
        // await WorkflowInit.Runtime.ExecuteCommandAsync(processId, identityId, impersonatedIdentityId: null, cmd);
    }

    private static Task SetParamAsync(Guid processId, string name, object value)
        => WorkflowInit.Runtime.SetPersistentProcessParameterAsync(processId, name, value);

    private static async Task<bool> WaitForExitAsync(Process p, TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            if (p.HasExited) return true;
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(object s, EventArgs e) => tcs.TrySetResult(true);
            p.Exited += Handler;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var delay = Task.Delay(timeout, cts.Token);
            var completed = await Task.WhenAny(tcs.Task, delay).ConfigureAwait(false);

            p.Exited -= Handler;
            if (completed == tcs.Task) { cts.Cancel(); return true; }
            return false;
        }
        catch { return p.HasExited; }
    }

    private static string Trunc(string s, int max) => s?.Length > max ? s.Substring(0, max) : s;
}
