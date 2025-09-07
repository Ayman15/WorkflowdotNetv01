namespace RemoteRunner;

public class RunnerOptions
{
    /// Required shared secret; request must send header X-API-KEY
    public string ApiKey { get; set; } = "";

    /// Allowlist: name -> absolute path to exe on THIS server
    public Dictionary<string, string> AllowedExecutables { get; set; } = new();

    /// Default timeout (seconds) if request doesn’t specify one
    public int DefaultTimeoutSeconds { get; set; } = 60;

    /// Max characters of stdout/stderr to include in response
    public int MaxStdCaptureChars { get; set; } = 4000;
}
