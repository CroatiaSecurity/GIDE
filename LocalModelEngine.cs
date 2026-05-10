using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace GIDE
{
    /// <summary>
    /// Self-contained local model inference engine using llama.cpp server.
    /// Downloads and manages llama.cpp binaries and runs inference locally.
    /// </summary>
    public class LocalModelEngine
    {
        private Process _serverProcess;
        private string _modelsDir;
        private string _binDir;
        private int _port = 11500;  // Different from Ollama's 11434
        private bool _isInitialized = false;
        private string _currentModelPath = null;

        public LocalModelEngine()
        {
            _modelsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gide", "models");
            _binDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gide", "bin");
        }

        public string LastError { get; set; }

        /// <summary>
        /// Initialize the engine - download llama.cpp if needed and start server.
        /// </summary>
        public bool Initialize(HardwareDetector.ModelRecommendation model)
        {
            if (_isInitialized)
                return true;

            try
            {
                Console.WriteLine("  Initializing local model engine...");
                LastError = "";

                // Ensure llama.cpp server binary is available
                if (!EnsureLlamaServer())
                {
                    if (string.IsNullOrEmpty(LastError))
                        LastError = "Failed to download llama.cpp server binary. Check internet connection.";
                    Console.WriteLine("  ✗ " + LastError);
                    return false;
                }

                // Ensure model is downloaded
                if (!ModelManager.EnsureModel(model))
                {
                    LastError = "Failed to download model: " + model.ModelId + ". Check internet connection.";
                    Console.WriteLine("  ✗ " + LastError);
                    return false;
                }

                // Start the server with the model
                string modelPath = ModelManager.GetModelPath(model.ModelId);
                Console.WriteLine("  Model path: " + modelPath);
                
                if (!File.Exists(modelPath))
                {
                    LastError = "Model file not found at: " + modelPath;
                    Console.WriteLine("  ✗ " + LastError);
                    return false;
                }

                if (!StartServer(modelPath))
                {
                    if (string.IsNullOrEmpty(LastError))
                        LastError = "Failed to start llama.cpp server.";
                    Console.WriteLine("  ✗ " + LastError);
                    return false;
                }

                _currentModelPath = modelPath;
                _isInitialized = true;
                Console.WriteLine("  ✓ Local model engine ready");
                return true;
            }
            catch (Exception ex)
            {
                LastError = "Exception during initialization: " + ex.Message;
                Console.WriteLine("  ✗ " + LastError);
                return false;
            }
        }

        /// <summary>
        /// Generate text using the loaded model.
        /// </summary>
        public string Generate(List<ChatMessage> messages, string systemPrompt, int contextLength = 32768)
        {
            if (!_isInitialized)
            {
                return "[Engine Error] Not initialized. Call Initialize() first.";
            }

            // Ensure server is still running
            if (_serverProcess == null || _serverProcess.HasExited)
            {
                Console.WriteLine("  [!] Server process stopped, restarting...");
                if (!StartServer(_currentModelPath))
                {
                    return "[Engine Error] Failed to restart server";
                }
            }

            try
            {
                // Build request payload for llama.cpp server
                var requestBody = new Dictionary<string, object>();
                requestBody.Add("messages", BuildMessagesArray(messages, systemPrompt));
                requestBody.Add("temperature", 0.3);
                requestBody.Add("max_tokens", 8192);
                requestBody.Add("stream", false);

                var serializer = new JavaScriptSerializer();
                string payload = serializer.Serialize(requestBody);

                // Send request to local server
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";
                    client.Encoding = Encoding.UTF8;

                    string url = "http://127.0.0.1:" + _port + "/v1/chat/completions";
                    string response = client.UploadString(url, payload);

                    // Parse response
                    return ParseResponse(response);
                }
            }
            catch (Exception ex)
            {
                return "[Engine Error] " + ex.Message;
            }
        }

        /// <summary>
        /// Stop the server and cleanup.
        /// </summary>
        public void Shutdown()
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                try
                {
                    _serverProcess.Kill();
                    _serverProcess.WaitForExit(5000);
                }
                catch { }
            }
            _serverProcess = null;
            _isInitialized = false;
        }

        private bool EnsureLlamaServer()
        {
            string serverExe = Path.Combine(_binDir, "llama-server.exe");

            if (File.Exists(serverExe))
            {
                // Check it's valid (at least 1MB)
                FileInfo fi = new FileInfo(serverExe);
                if (fi.Length > 1024 * 1024)
                    return true;

                Console.WriteLine("  [!] Server binary appears corrupted, re-downloading...");
                File.Delete(serverExe);
            }

            // Download llama.cpp server binary
            // Using a reliable release build
            return DownloadLlamaServer();
        }

        private string GetLatestLlamaCppUrl()
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                // Detect hardware to pick best build
                var hw = HardwareDetector.GetHardwareInfo();
                bool useCuda = hw.HasCudaGPU && hw.GpuVram > 2L * 1024 * 1024 * 1024;

                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "GIDE";
                    string json = client.DownloadString("https://api.github.com/repos/ggml-org/llama.cpp/releases/latest");

                    // Simple JSON parsing - find browser_download_url entries for win zips
                    var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                    serializer.MaxJsonLength = int.MaxValue;
                    var release = serializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(json);

                    if (release == null || !release.ContainsKey("assets")) return null;

                    var assets = release["assets"] as System.Collections.ArrayList;
                    if (assets == null) return null;

                    string cudaUrl = null;
                    string cpuUrl = null;

                    foreach (var assetObj in assets)
                    {
                        var asset = assetObj as System.Collections.Generic.Dictionary<string, object>;
                        if (asset == null) continue;
                        if (!asset.ContainsKey("name") || !asset.ContainsKey("browser_download_url")) continue;

                        string name = asset["name"].ToString();
                        string url = asset["browser_download_url"].ToString();

                        // Skip cudart packages
                        if (name.StartsWith("cudart")) continue;
                        // Want llama-bXXXX-bin-win-{cpu|cuda-12.4}-x64.zip
                        if (!name.Contains("win") || !name.EndsWith(".zip") || !name.Contains("x64")) continue;
                        if (name.Contains("arm64")) continue;

                        if (name.Contains("cuda-12") && cudaUrl == null)
                            cudaUrl = url;
                        else if (name.Contains("cpu") && cpuUrl == null)
                            cpuUrl = url;
                    }

                    if (useCuda && cudaUrl != null) return cudaUrl;
                    return cpuUrl ?? cudaUrl;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  [!] Failed to fetch latest release: " + ex.Message);
                return null;
            }
        }

        private bool DownloadLlamaServer()
        {
            Console.WriteLine("  Downloading llama.cpp server (one-time setup)...");

            if (!Directory.Exists(_binDir))
                Directory.CreateDirectory(_binDir);

            string serverExe = Path.Combine(_binDir, "llama-server.exe");
            string tempPath = serverExe + ".tmp";

            // Auto-fetch latest llama.cpp release from GitHub API
            string downloadUrl = GetLatestLlamaCppUrl();
            if (string.IsNullOrEmpty(downloadUrl))
            {
                // Fallback to known good version
                downloadUrl = "https://github.com/ggml-org/llama.cpp/releases/download/b9093/llama-b9093-bin-win-cpu-x64.zip";
                Console.WriteLine("  [!] Could not fetch latest, using fallback: " + downloadUrl);
            }
            else
            {
                Console.WriteLine("  Latest llama.cpp: " + downloadUrl);
            }

            try
            {
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                string zipPath = Path.Combine(_binDir, "llama.zip");

                using (var client = new WebClient())
                {
                    Console.WriteLine("  Downloading llama.cpp binaries from: " + downloadUrl);
                    try
                    {
                        client.DownloadFile(downloadUrl, zipPath);
                    }
                    catch (Exception dlEx)
                    {
                        LastError = "Download failed: " + dlEx.Message + " | URL: " + downloadUrl;
                        Console.WriteLine("  ✗ " + LastError);
                        return false;
                    }

                    // Verify zip was downloaded
                    if (!File.Exists(zipPath))
                    {
                        LastError = "Zip file not created after download";
                        return false;
                    }

                    FileInfo zipInfo = new FileInfo(zipPath);
                    if (zipInfo.Length < 100 * 1024)
                    {
                        LastError = "Downloaded zip is too small (" + zipInfo.Length + " bytes), likely an error page";
                        File.Delete(zipPath);
                        return false;
                    }

                    // Extract using PowerShell (available on Windows 7+)
                    Console.WriteLine("  Extracting binaries...");
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = string.Format("-NoProfile -Command \"Expand-Archive -Path '{0}' -DestinationPath '{1}' -Force\"", zipPath, _binDir),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (var process = Process.Start(psi))
                    {
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                        {
                            string error = process.StandardError.ReadToEnd();
                            LastError = "Extraction failed: " + error;
                            Console.WriteLine("  ✗ " + LastError);
                            return false;
                        }
                    }

                    // Clean up zip
                    try { File.Delete(zipPath); } catch { }
                }

                // Look for server binary - could be llama-server.exe or server.exe
                if (!File.Exists(serverExe))
                {
                    // Try to find it in subdirectories or with alternate names
                    string[] possibleNames = { "llama-server.exe", "server.exe" };
                    string found = null;
                    foreach (var name in possibleNames)
                    {
                        var matches = Directory.GetFiles(_binDir, name, SearchOption.AllDirectories);
                        if (matches.Length > 0)
                        {
                            found = matches[0];
                            break;
                        }
                    }

                    if (found != null)
                    {
                        // Move found binary to expected location
                        if (found != serverExe)
                        {
                            // Copy all files from the found directory to _binDir
                            string foundDir = Path.GetDirectoryName(found);
                            if (foundDir != _binDir)
                            {
                                foreach (var file in Directory.GetFiles(foundDir))
                                {
                                    string dest = Path.Combine(_binDir, Path.GetFileName(file));
                                    if (!File.Exists(dest))
                                        File.Copy(file, dest);
                                }
                            }
                            // Rename if needed
                            string actualServerExe = Path.Combine(_binDir, Path.GetFileName(found));
                            if (actualServerExe != serverExe && File.Exists(actualServerExe))
                                File.Copy(actualServerExe, serverExe, true);
                        }
                    }
                    else
                    {
                        // List what we got for debugging
                        var allFiles = Directory.GetFiles(_binDir, "*.exe", SearchOption.AllDirectories);
                        LastError = "Server binary not found. Files extracted: " + string.Join(", ", allFiles);
                        Console.WriteLine("  ✗ " + LastError);
                        return false;
                    }
                }

                Console.WriteLine("  ✓ llama.cpp server ready");
                return true;
            }
            catch (Exception ex)
            {
                LastError = "Exception downloading llama.cpp: " + ex.Message;
                Console.WriteLine("  ✗ " + LastError);
                return false;
            }
        }

        private bool IsPortAvailable(int port)
        {
            try
            {
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private int FindFreePort()
        {
            for (int p = 11500; p < 11600; p++)
            {
                if (IsPortAvailable(p))
                    return p;
            }
            return 0;
        }

        private bool StartServer(string modelPath)
        {
            // Kill any existing server on our port
            KillExistingServer();

            // Find a free port
            if (!IsPortAvailable(_port))
            {
                int freePort = FindFreePort();
                if (freePort == 0)
                {
                    LastError = "No free ports available in range 11500-11599";
                    Console.WriteLine("  ✗ " + LastError);
                    return false;
                }
                Console.WriteLine("  Port " + _port + " in use, using port " + freePort);
                _port = freePort;
            }

            string serverExe = Path.Combine(_binDir, "llama-server.exe");
            if (!File.Exists(serverExe))
            {
                Console.WriteLine("  ✗ Server binary not found: " + serverExe);
                return false;
            }

            try
            {
                // Get hardware info for optimal settings
                var hw = HardwareDetector.GetHardwareInfo();
                long ramGB = (long)(hw.TotalRAM / (1024 * 1024 * 1024));

                // Build arguments
                var args = new StringBuilder();
                args.Append("-m \"" + modelPath + "\" ");
                args.Append("--port " + _port + " ");
                args.Append("--host 127.0.0.1 ");
                args.Append("-c 8192 ");  // Context length (smaller for compatibility)

                // CPU threads - use all available minus 1
                int threads = Math.Max(1, hw.CpuCores - 1);
                args.Append("-t " + threads + " ");

                // GPU layers - use GPU if available
                if (hw.HasCudaGPU && hw.GpuVram > 4L * 1024 * 1024 * 1024)  // 4GB+ VRAM
                {
                    // Use as many GPU layers as possible
                    int gpuLayers = 999;
                    args.Append("-ngl " + gpuLayers + " ");
                    Console.WriteLine("  Using GPU acceleration");
                }

                // Batch size for faster processing
                args.Append("-b 512 ");

                Console.WriteLine("  Starting inference server...");

                _serverProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = serverExe,
                        Arguments = args.ToString(),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = _binDir
                    }
                };

                // Capture output for debugging
                var errorBuffer = new StringBuilder();
                _serverProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Console.WriteLine("  [Server] " + e.Data);
                        if (errorBuffer.Length < 2000)
                            errorBuffer.AppendLine(e.Data);
                    }
                };

                _serverProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Console.WriteLine("  [Server Error] " + e.Data);
                        if (errorBuffer.Length < 2000)
                            errorBuffer.AppendLine(e.Data);
                    }
                };

                _serverProcess.Start();
                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();

                // Wait for server to be ready
                int attempts = 0;
                while (attempts < 30)  // 30 seconds max
                {
                    Thread.Sleep(1000);

                    if (_serverProcess.HasExited)
                    {
                        // Wait briefly for any final output
                        Thread.Sleep(200);
                        string output = errorBuffer.ToString().Trim();
                        if (string.IsNullOrEmpty(output)) output = "(no output)";
                        LastError = "Server exited (code " + _serverProcess.ExitCode + "). Output:\n" + output + "\n\nArgs: " + args.ToString();
                        Console.WriteLine("  ✗ " + LastError);
                        return false;
                    }

                    // Try to connect
                    try
                    {
                        using (var client = new WebClient())
                        {
                            client.DownloadString("http://127.0.0.1:" + _port + "/health");
                            return true;  // Server is ready
                        }
                    }
                    catch { }

                    attempts++;
                    if (attempts % 5 == 0)
                        Console.WriteLine("  Waiting for server... (" + attempts + "s)");
                }

                LastError = "Server failed to start within 30 seconds (port " + _port + ")";
                Console.WriteLine("  ✗ " + LastError);
                return false;
            }
            catch (Exception ex)
            {
                LastError = "Failed to start server: " + ex.Message;
                Console.WriteLine("  ✗ " + LastError);
                return false;
            }
        }

        private void KillExistingServer()
        {
            try
            {
                // Find and kill any llama-server processes
                foreach (var proc in Process.GetProcessesByName("llama-server"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(2000);
                    }
                    catch { }
                }
            }
            catch { }

            // Also check if something is using our port
            try
            {
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        // This is a simplified check - in practice we'd use netstat
                        if (proc.ProcessName.ToLower().Contains("llama"))
                        {
                            proc.Kill();
                            proc.WaitForExit(1000);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private object[] BuildMessagesArray(List<ChatMessage> messages, string systemPrompt)
        {
            var messageList = new List<object>();

            // Add system message
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                var systemMsg = new Dictionary<string, object>();
                systemMsg.Add("role", "system");
                systemMsg.Add("content", systemPrompt);
                messageList.Add(systemMsg);
            }

            // Add conversation history
            foreach (ChatMessage msg in messages)
            {
                var chatMsg = new Dictionary<string, object>();
                chatMsg.Add("role", msg.Role);
                chatMsg.Add("content", msg.Content);
                messageList.Add(chatMsg);
            }

            return messageList.ToArray();
        }

        private string ParseResponse(string json)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                var result = serializer.Deserialize<Dictionary<string, object>>(json);

                if (result != null && result.ContainsKey("choices"))
                {
                    // Could be object[] or ArrayList depending on deserializer
                    var choicesRaw = result["choices"];
                    System.Collections.IList choices = choicesRaw as System.Collections.IList;
                    if (choices != null && choices.Count > 0)
                    {
                        var choice = choices[0] as Dictionary<string, object>;
                        if (choice != null && choice.ContainsKey("message"))
                        {
                            var message = choice["message"] as Dictionary<string, object>;
                            if (message != null && message.ContainsKey("content") && message["content"] != null)
                            {
                                string content = message["content"].ToString();
                                return StripThinkTags(content);
                            }
                        }
                        // Some servers return content directly on choice
                        if (choice != null && choice.ContainsKey("text") && choice["text"] != null)
                        {
                            return StripThinkTags(choice["text"].ToString());
                        }
                    }
                }

                // Fallback: check for direct content / error fields
                if (result != null && result.ContainsKey("content") && result["content"] != null)
                    return StripThinkTags(result["content"].ToString());
                if (result != null && result.ContainsKey("error"))
                    return "[Server Error] " + serializer.Serialize(result["error"]);

                return "[Parse Error] Unexpected response format: " + (json.Length > 500 ? json.Substring(0, 500) + "..." : json);
            }
            catch (Exception ex)
            {
                return "[Parse Error] " + ex.Message + "\n\nRaw: " + (json.Length > 500 ? json.Substring(0, 500) : json);
            }
        }

        private string StripThinkTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Remove <think>...</think> blocks (Qwen3 thinking mode output)
            int start = text.IndexOf("<think>");
            while (start >= 0)
            {
                int end = text.IndexOf("</think>", start);
                if (end < 0) break;
                text = text.Substring(0, start) + text.Substring(end + 8);
                start = text.IndexOf("<think>");
            }

            return text.TrimStart('\n', '\r', ' ');
        }
    }
}
