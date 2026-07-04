// ServerManager.cs
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

        public static async Task StartServerAsync(TextBlock statusTextBlock)
        {
            if (IsPortInUse(Port))
            {
                UpdateStatus(statusTextBlock, "API Server: Already Running ✅", Colors.LimeGreen);
                return;
            }

            UpdateStatus(statusTextBlock, "API Server: Starting...", Colors.Yellow);

            try
            {
                string? workingDir = RepositoryPaths.TryGetBackendDirectory();
                if (workingDir == null || !File.Exists(Path.Combine(workingDir, "api.py")))
                {
                    UpdateStatus(statusTextBlock, "API Server: backend/api.py not found ❌", Colors.Red);
                    return;
                }

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

                _pythonProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine($"Uvicorn: {e.Data}");
                        if (e.Data.Contains("Uvicorn running") || e.Data.Contains("Application startup complete"))
                        {
                            App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
                                UpdateStatus(statusTextBlock, "API Server: Running ✅", Colors.LimeGreen));
                        }
                    }
                };

                _pythonProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        Debug.WriteLine($"Uvicorn Error: {e.Data}");
                };

                _pythonProcess.Start();
                _pythonProcess.BeginOutputReadLine();
                _pythonProcess.BeginErrorReadLine();

                await Task.Delay(5000); // 少し長めに待機

                if (!_pythonProcess.HasExited && IsPortInUse(Port))
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
            if (textBlock == null) return;
            textBlock.Text = message;
            textBlock.Foreground = new SolidColorBrush(color);
        }

        public static void StopServer()
        {
            if (_pythonProcess != null && !_pythonProcess.HasExited)
            {
                try { _pythonProcess.Kill(true); _pythonProcess.Dispose(); } catch { }
            }
        }
    }
}