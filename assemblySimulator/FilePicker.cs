using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace assemblySimulator
{
    public static class FilePicker
    {
        public static string? ChooseFile(string title = "Select a File", bool allowAll = false, bool getDir = false)
        {
            if (getDir && allowAll)
                throw new ArgumentException("Cannot have both getDir and allowAll set to true.");
            string? filePath = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                filePath = PickFileWindows(allowAll, getDir);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                filePath = PickFileLinux(title, allowAll, getDir);
            }
            if (string.IsNullOrWhiteSpace(filePath))
            {
                Console.Error.WriteLine("Failed to pick a file with GUI");
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

        private static string? PickFileWindows(bool allowAll = false, bool getDir = false)
        {
            // powershell
            var psScript = "[System.Reflection.Assembly]::LoadWithPartialName('System.windows.forms') | Out-Null; ";
            if (getDir)
            {
                psScript += "$dialog = New-Object System.Windows.Forms.FolderBrowserDialog; " +
                            "if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { Write-Output $dialog.SelectedPath }";
            }
            else
            {
                psScript += "$dialog = New-Object System.Windows.Forms.OpenFileDialog; " +
                            "$dialog.Filter = " + (allowAll ? "'All Files (*.*)|*.*'; " : "'Binary Files (*.bin)|*.bin'; ") +
                            "if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { Write-Output $dialog.FileName }";
            }

            return RunCommand("powershell", $"-NoProfile -Command \"{psScript}\"");
        }

        private static string? PickFileLinux(string title, bool allowAll = false, bool getDir = false)
        {
            string? result;

            { // gnome zenity
                string zenityArgs = $"--file-selection --title=\"{title}\"";
                if (getDir)
                {
                    zenityArgs += " --directory";
                }
                if (!allowAll && !getDir)
                {
                    zenityArgs += " --file-filter=\"*.bin\"";
                }
                result = RunCommand("zenity", zenityArgs);
            }

            if (string.IsNullOrWhiteSpace(result))
            {// kde kdialog
                string kdialogArgs = $"--title=\"{title}\"";
                if (getDir)
                {
                    kdialogArgs += " --getexistingdirectory .";
                }
                if (!getDir)
                {
                    string filter = allowAll ? "*|All files" : "*.bin|Binary files (*.bin)";
                    kdialogArgs += $" --getopenfilename . \"{filter}\"";
                }
                result = RunCommand("kdialog", kdialogArgs);
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
