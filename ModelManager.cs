using System;
using System.IO;
using System.Net;
using System.Threading;

namespace GIDE
{
    public static class ModelManager
    {
        private static string ModelsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".gide", "models");

        public static string GetModelPath(string modelId)
        {
            return Path.Combine(ModelsDir, modelId + ".gguf");
        }

        public static bool IsModelDownloaded(string modelId)
        {
            string path = GetModelPath(modelId);
            return File.Exists(path);
        }

        public static bool EnsureModel(HardwareDetector.ModelRecommendation model)
        {
            string modelPath = GetModelPath(model.ModelId);

            if (File.Exists(modelPath))
            {
                // Verify file size is reasonable
                FileInfo fi = new FileInfo(modelPath);
                if (fi.Length > 100 * 1024 * 1024) // At least 100MB
                {
                    Console.WriteLine("  ✓ Model already downloaded: " + model.DisplayName);
                    return true;
                }
                else
                {
                    Console.WriteLine("  [!] Model file appears corrupted, re-downloading...");
                    try { File.Delete(modelPath); } catch { }
                }
            }

            return DownloadModel(model);
        }

        private static bool DownloadModel(HardwareDetector.ModelRecommendation model)
        {
            // Ensure models directory exists
            if (!Directory.Exists(ModelsDir))
                Directory.CreateDirectory(ModelsDir);

            string modelPath = GetModelPath(model.ModelId);
            string tempPath = modelPath + ".tmp";

            Console.WriteLine();
            Console.WriteLine("  === Downloading Model ===");
            Console.WriteLine("  Model: " + model.DisplayName);
            Console.WriteLine("  Size: ~" + (model.ModelSizeBytes / (1024 * 1024 * 1024)) + " GB");
            Console.WriteLine("  This is a one-time download.");
            Console.WriteLine();

            try
            {
                // Enable TLS 1.2
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                using (var client = new WebClient())
                {
                    // Progress reporting
                    long lastBytes = 0;
                    DateTime lastUpdate = DateTime.Now;

                    client.DownloadProgressChanged += (sender, e) =>
                    {
                        // Update every 2 seconds
                        if ((DateTime.Now - lastUpdate).TotalSeconds >= 2)
                        {
                            long bytesPerSecond = (e.BytesReceived - lastBytes) / 2;
                            double percent = (double)e.BytesReceived / e.TotalBytesToReceive * 100;
                            double mbps = (double)bytesPerSecond / (1024 * 1024);

                            Console.Write("  Progress: " + percent.ToString("F1") + "% | " +
                                         mbps.ToString("F1") + " MB/s    \r");

                            lastBytes = e.BytesReceived;
                            lastUpdate = DateTime.Now;
                        }
                    };

                    // Download to temp file first
                    client.DownloadFileAsync(new Uri(model.HuggingFaceUrl), tempPath);

                    // Wait for completion
                    while (client.IsBusy)
                    {
                        Thread.Sleep(100);
                    }

                    Console.WriteLine(); // New line after progress

                    // Check if download succeeded
                    if (!File.Exists(tempPath))
                    {
                        Console.WriteLine("  ✗ Download failed - no file created");
                        return false;
                    }

                    FileInfo fi = new FileInfo(tempPath);
                    if (fi.Length < 100 * 1024) // Less than 100KB is probably an error
                    {
                        string content = File.ReadAllText(tempPath);
                        Console.WriteLine("  ✗ Download failed: " + content.Substring(0, Math.Min(200, content.Length)));
                        File.Delete(tempPath);
                        return false;
                    }

                    // Move temp to final location
                    if (File.Exists(modelPath))
                        File.Delete(modelPath);
                    File.Move(tempPath, modelPath);

                    Console.WriteLine("  ✓ Model downloaded successfully!");
                    Console.WriteLine("  Location: " + modelPath);
                    Console.WriteLine();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  ✗ Download failed: " + ex.Message);
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch { }
                return false;
            }
        }

        public static string GetAvailableModelOrDefault()
        {
            // Check for any downloaded model, return the best one
            var hardware = HardwareDetector.GetHardwareInfo();
            var recommendation = HardwareDetector.RecommendModel(hardware);

            // Check recommended first
            if (IsModelDownloaded(recommendation.ModelId))
                return recommendation.ModelId;

            // Check all models from best to worst
            string[] modelIds = { "qwen3-30b-awq", "qwen3-14b", "qwen3-8b", "qwen3-4b" };
            foreach (var id in modelIds)
            {
                if (IsModelDownloaded(id))
                    return id;
            }

            // Return recommendation even if not downloaded (will be downloaded)
            return recommendation.ModelId;
        }

        public static HardwareDetector.ModelRecommendation GetModelInfo(string modelId)
        {
            // Return model info for known models
            switch (modelId)
            {
                case "qwen3-30b-awq":
                    return new HardwareDetector.ModelRecommendation
                    {
                        ModelId = "qwen3-30b-awq",
                        ModelName = "Qwen3-30B-AWQ",
                        DisplayName = "Qwen3 30B (High Quality)",
                        HuggingFaceUrl = "https://huggingface.co/Qwen/Qwen3-30B-AWQ-GGUF/resolve/main/qwen3-30b-awq-q4_0.gguf",
                        ModelSizeBytes = 18L * 1024 * 1024 * 1024,
                        ContextLength = 32768,
                        Description = "Best quality responses, requires high-end hardware",
                        NeedsGpu = true
                    };
                case "qwen3-14b":
                    return new HardwareDetector.ModelRecommendation
                    {
                        ModelId = "qwen3-14b",
                        ModelName = "Qwen3-14B-Q4_K_M",
                        DisplayName = "Qwen3 14B (Balanced)",
                        HuggingFaceUrl = "https://huggingface.co/Qwen/Qwen3-14B-GGUF/resolve/main/qwen3-14b-q4_k_m.gguf",
                        ModelSizeBytes = 9L * 1024 * 1024 * 1024,
                        ContextLength = 32768,
                        Description = "Good balance of quality and speed",
                        NeedsGpu = false
                    };
                case "qwen3-8b":
                    return new HardwareDetector.ModelRecommendation
                    {
                        ModelId = "qwen3-8b",
                        ModelName = "Qwen3-8B-Q4_K_M",
                        DisplayName = "Qwen3 8B (Fast)",
                        HuggingFaceUrl = "https://huggingface.co/Qwen/Qwen3-8B-GGUF/resolve/main/qwen3-8b-q4_k_m.gguf",
                        ModelSizeBytes = 5L * 1024 * 1024 * 1024,
                        ContextLength = 32768,
                        Description = "Fast responses, good for most coding tasks",
                        NeedsGpu = false
                    };
                case "qwen3-4b":
                    return new HardwareDetector.ModelRecommendation
                    {
                        ModelId = "qwen3-4b",
                        ModelName = "Qwen3-4B-Q4_K_M",
                        DisplayName = "Qwen3 4B (Lightweight)",
                        HuggingFaceUrl = "https://huggingface.co/Qwen/Qwen3-4B-GGUF/resolve/main/qwen3-4b-q4_k_m.gguf",
                        ModelSizeBytes = 2L * 1024 * 1024 * 1024,
                        ContextLength = 32768,
                        Description = "Lightweight, works on most systems",
                        NeedsGpu = false
                    };
                default:
                    return HardwareDetector.RecommendModel();
            }
        }
    }
}
