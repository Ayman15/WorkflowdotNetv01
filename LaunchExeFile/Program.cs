using System;
using System.Diagnostics;
using System.IO;

namespace ExcelLauncherTest
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // If no file is passed, just launch Excel itself
                string fileToOpen = args.Length > 0 ? args[0] : "";

                var psi = new ProcessStartInfo
                {
                    FileName = "excel.exe",   // Excel must be installed on the machine
                    Arguments = string.IsNullOrWhiteSpace(fileToOpen) ? "" : $"\"{fileToOpen}\"",
                    UseShellExecute = true    // Let Windows figure out how to open Excel
                };

                Console.WriteLine($"[ExcelLauncherTest] Starting: {psi.FileName} {psi.Arguments}");
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ExcelLauncherTest] ERROR: " + ex.Message);
            }
        }
    }
}
