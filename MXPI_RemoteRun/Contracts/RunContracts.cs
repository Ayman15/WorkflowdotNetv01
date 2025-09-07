namespace RemoteRunner.Contracts;

public record RunRequest(
    string? exeName,            // preferred: key in AllowedExecutables
    string? exePath,            // optional: will be validated against allowlist
    string? args,               // e.g. "--processId 123"
    string? processId,          // optional: informational
    int? timeoutSeconds,        // optional override
    string? workingDirectory    // optional
);

public record RunResponse(
    bool started,
    int? exitCode,
    long durationMs,
    string? stdout,
    string? stderr,
    string resolvedExePath
);
