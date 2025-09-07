namespace RemoteRunner.Utilities;

public static class StringUtil
{
    public static string? Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);
}
