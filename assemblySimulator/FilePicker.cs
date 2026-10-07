using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace assemblySimulator
{
    public static class FilePicker
    {
        public static string? ChooseFile(string title = "Select a File")
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return PickFileWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return PickFileLinux(title);
            }

            throw new PlatformNotSupportedException("Unsupported operating system.");
        }

        private static string? PickFileWindows()
        {
            // Using PowerShell's built-in OpenFileDialog to avoid WinForms dependencies
            var psScript = "[System.Reflection.Assembly]::LoadWithPartialName('System.windows.forms') | Out-Null; " +
                           "$dialog = New-Object System.Windows.Forms.OpenFileDialog; " +
                           "$dialog.Filter = 'All Files (*.*)|*.*'; " +
                           "if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { Write-Output $dialog.FileName }";

            return RunCommand("powershell", $"-NoProfile -Command \"{psScript}\"");
        }

        private static string? PickFileLinux(string title)
        {
            // Try zenity (GNOME/GTK default)
            string? result = RunCommand("zenity", $"--file-selection --title=\"{title}\"");

            // Fallback to kdialog (KDE default) if zenity isn't available
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
