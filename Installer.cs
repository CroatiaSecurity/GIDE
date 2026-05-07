using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace GIDE
{
    public static class Installer
    {
public static bool EnsureOllamaInstalled()
{
    // Enable TLS 1.2 before downloading
    System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
    
    // Check if ollama is already installed
    if (IsOllamaInstalled())
    {
        Console.WriteLine("  ✓ Ollama is already installed");
        return true;
    }

    Console.WriteLine("\n  [!] Ollama not found. Installing Ollama...");
    Console.WriteLine("  This will download Ollama for Windows...");

    try
    {
        // Download Ollama installer - using more reliable method
        string installerPath = Path.Combine(Path.GetTempPath(), "OllamaSetup.exe");
        
        // Use WebClient instead of HttpClient for better compatibility
        using (var client = new System.Net.WebClient())
        {
            Console.WriteLine("  Downloading from https://ollama.com/download/OllamaSetup.exe");
            Console.WriteLine("  This may take a minute...");
            client.DownloadFile("https://ollama.com/download/OllamaSetup.exe", installerPath);
        }

        // Run installer
        Console.WriteLine("  Installing Ollama (this may take a moment)...");
        var installProcess = Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SILENT",
            UseShellExecute = true,
            Verb = "runas"
        });
        
        installProcess.WaitForExit();
        System.Threading.Thread.Sleep(3000);
        
        // Clean up
        try { File.Delete(installerPath); } catch { }

        if (IsOllamaInstalled())
        {
            Console.WriteLine("  ✓ Ollama installed successfully");
            return true;
        }
        else
        {
            Console.WriteLine("  ✗ Ollama installation failed.");
            return false;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("  ✗ Failed to install Ollama: " + ex.Message);
        return false;
    }
}

        public static bool EnsureOllamaRunning()
        {
            // Check if ollama service is running
            try
            {
                var checkProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
                
                if (checkProcess != null)
                {
                    checkProcess.WaitForExit(2000);
                    if (checkProcess.ExitCode == 0)
                        return true;
                }
            }
            catch { }

            // Start ollama service
            try
            {
                Console.WriteLine("  Starting Ollama service...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "serve",
                    UseShellExecute = true,
                    CreateNoWindow = true
                });
                Thread.Sleep(4000); // Wait for service to start
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool EnsureModelInstalled(string modelName)
        {
            // Check if model exists
            try
            {
                var checkProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "list",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
                
                if (checkProcess != null)
                {
                    string output = checkProcess.StandardOutput.ReadToEnd();
                    checkProcess.WaitForExit();
                    
                    if (output.Contains(modelName))
                    {
                        Console.WriteLine("  ✓ Model " + modelName + " is already installed");
                        return true;
                    }
                }
            }
            catch { }

            // Pull the model
            Console.WriteLine("  Downloading model " + modelName + " (this may take several minutes)...");
            Console.WriteLine("  This is a one-time download (~8-10GB)");
            
            try
            {
                var pullProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "pull " + modelName,
                    UseShellExecute = true,
                    CreateNoWindow = false
                });
                
                pullProcess.WaitForExit();
                
                if (pullProcess.ExitCode == 0)
                {
                    Console.WriteLine("  ✓ Model " + modelName + " installed successfully");
                    return true;
                }
                else
                {
                    Console.WriteLine("  ✗ Failed to install " + modelName);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  ✗ Error installing model: " + ex.Message);
                return false;
            }
        }

        private static bool IsOllamaInstalled()
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
                
                if (process != null)
                {
                    process.WaitForExit(2000);
                    return process.ExitCode == 0;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}