using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace assemblySimulator
{
    public static class FilePicker
    {
        public static string? ChooseFile(string title = "Select a File", bool allowAll = false)
        {
            string? filePath = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                filePath = PickFileWindows(allowAll);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                filePath = PickFileLinux(title);
            }
            if (string.IsNullOrWhiteSpace(filePath))
            {
                Console.Error.WriteLine("Failed to pick a file.");
                Console.WriteLine("type '!' to fail gracefully, or enter a file path manually.");
                while (string.IsNullOrWhiteSpace(filePath))
                {
                    Console.WriteLine("File path: ");
                    filePath = Console.ReadLine();
                }
                return String.Equals("!", filePath) ? null : filePath;
            }
            return filePath;
        }

        private static string? PickFileWindows(bool allowAll = false)
        {
            // powershell
            var psScript = "[System.Reflection.Assembly]::LoadWithPartialName('System.windows.forms') | Out-Null; " +
                           "$dialog = New-Object System.Windows.Forms.OpenFileDialog; " +
                           "$dialog.Filter = " + (allowAll ? "'All Files (*.*)|*.*'; " : "'Binary Files (*.bin)|*.bin'; ") +
                           "if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { Write-Output $dialog.FileName }";

            return RunCommand("powershell", $"-NoProfile -Command \"{psScript}\"");
        }

        private static string? PickFileLinux(string title)
        {
            // gnome zenity
            string? result = RunCommand("zenity", $"--file-selection --title=\"{title}\"");
            // kde kdialog
            if (string.IsNullOrWhiteSpace(result))
            {
                result = RunCommand("kdialog", $"--getopenfilename . --title \"{title}\"");
            }

            return result;
        }

        private static string? RunCommand(string command, string args)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                return string.IsNullOrEmpty(output) ? null : output;
            }
            catch
            {
                // Command tool was not found on the system
                return null;
            }
        }
    }
}
