using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Windows.UI;

namespace App2
{
    public class ServerManager
    {
        private static Process? _pythonProcess;
        private const string Host = "127.0.0.1";
        private const int Port = 8000;
        private const string StartupCompleteMarker = "Application startup complete";

        public static async Task StartServerAsync(TextBlock statusTextBlock)
        {
            if (IsPortInUse(Port))
            {
                UpdateStatus(statusTextBlock, "API Server: Already Running ✅", Colors.LimeGreen);
                return;
            }

            UpdateStatus(statusTextBlock, "API Server: Starting…", Colors.Yellow);

            try
            {
                string? workingDir = RepositoryPaths.TryGetBackendDirectory();
                if (workingDir == null || !File.Exists(Path.Combine(workingDir, "api.py")))
                {
                    UpdateStatus(statusTextBlock, "API Server: backend/api.py not found ❌", Colors.Red);
                    return;
                }

                var startupComplete = new TaskCompletionSource<bool>();

                _pythonProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "uvicorn",
                        Arguments = $"api:app --host {Host} --port {Port}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = workingDir
                    }
                };

                void OnUvicornLog(string? line)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        return;
                    }

                    Debug.WriteLine($"Uvicorn: {line}");
                    if (!line.Contains(StartupCompleteMarker) && !line.Contains("Uvicorn running"))
                    {
                        return;
                    }

                    startupComplete.TrySetResult(true);
                    App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (App.IsShuttingDown)
                        {
                            return;
                        }

                        UpdateStatus(statusTextBlock, "API Server: Running ✅", Colors.LimeGreen);
                    });
                }

                _pythonProcess.OutputDataReceived += (_, e) => OnUvicornLog(e.Data);
                _pythonProcess.ErrorDataReceived += (_, e) => OnUvicornLog(e.Data);

                _pythonProcess.Start();
                _pythonProcess.BeginOutputReadLine();
                _pythonProcess.BeginErrorReadLine();

                await Task.WhenAny(startupComplete.Task, Task.Delay(TimeSpan.FromSeconds(30)));

                if (startupComplete.Task.IsCompleted)
                {
                    return;
                }

                if (IsPortInUse(Port))
                {
                    UpdateStatus(statusTextBlock, "API Server: Running ✅", Colors.LimeGreen);
                }
                else
                {
                    UpdateStatus(statusTextBlock, "API Server: Failed to Start ❌", Colors.Red);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus(statusTextBlock, $"API Error: {ex.Message}", Colors.Red);
                Debug.WriteLine($"Server start failed: {ex}");
            }
        }

        private static bool IsPortInUse(int port)
        {
            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect(Host, port, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                if (success) client.EndConnect(result);
                return success;
            }
            catch { return false; }
        }

        private static void UpdateStatus(TextBlock? textBlock, string message, Color color)
        {
            if (App.IsShuttingDown || textBlock == null)
            {
                return;
            }

            textBlock.Text = message;
            textBlock.Foreground = new SolidColorBrush(color);
        }

        public static void StopServer()
        {
            if (_pythonProcess == null)
            {
                return;
            }

            try
            {
                if (!_pythonProcess.HasExited)
                {
                    _pythonProcess.Kill(true);
                }
            }
            catch { }
            finally
            {
                _pythonProcess.Dispose();
                _pythonProcess = null;
            }
        }
    }
}