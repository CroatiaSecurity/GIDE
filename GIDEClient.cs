using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace GIDE
{
    /// <summary>
    /// GIDE Client - Completely local, free AI coding assistant.
    /// No cloud services, no API keys, no usage charges.
    /// Uses self-hosted llama.cpp with free Qwen3 models.
    /// </summary>
    public class GIDEClient
    {
        public string CurrentModel = "qwen3-14b";
        public string CurrentModelDisplayName = "Qwen3 14B";

        private LocalModelEngine _localEngine = null;
        private HardwareDetector.ModelRecommendation _modelRecommendation = null;

        public GIDEClient()
        {
            // Detect hardware and select appropriate model
            InitializeLocalModel();
        }

        private void InitializeLocalModel()
        {
            // Detect hardware and get recommendation
            var hardware = HardwareDetector.GetHardwareInfo();
            _modelRecommendation = HardwareDetector.RecommendModel(hardware);

            // Check if user has a saved preference
            string savedModel = LoadSelectedModelFromConfig();
            if (!string.IsNullOrEmpty(savedModel))
            {
                // Verify the saved model can run on this hardware
                var savedModelInfo = ModelManager.GetModelInfo(savedModel);
                long ramGB = (long)(hardware.TotalRAM / (1024 * 1024 * 1024));
                long vramGB = hardware.BestGPU != null ? (long)(hardware.BestGPU.DedicatedVRAM / (1024 * 1024 * 1024)) : 0;

                bool canRunSaved = ramGB >= savedModelInfo.MinRamGB || vramGB >= savedModelInfo.MinVramGB;

                if (canRunSaved)
                {
                    CurrentModel = savedModel;
                    _modelRecommendation = savedModelInfo;
                    CurrentModelDisplayName = _modelRecommendation.DisplayName;
                    return;
                }
            }

            // Use hardware recommendation
            CurrentModel = _modelRecommendation.ModelId;
            CurrentModelDisplayName = _modelRecommendation.DisplayName;
        }

        private string LoadSelectedModelFromConfig()
        {
            try
            {
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".gide", "config.json");

                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                    var config = serializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(json);

                    if (config != null && config.ContainsKey("selected_model"))
                        return config["selected_model"].ToString();
                }
            }
            catch { }

            return null;
        }

        public void SetModel(HardwareDetector.ModelRecommendation model)
        {
            _modelRecommendation = model;
        }

        public bool InitializeLocalEngine()
        {
            // Always create a new engine to ensure correct model is loaded
            _localEngine = new LocalModelEngine();
            bool result = _localEngine.Initialize(_modelRecommendation);
            if (!result)
            {
                _lastError = _localEngine.LastError;
            }
            return result;
        }

        private string _lastError = "";

        public string GetLastError()
        {
            return _lastError;
        }

        public string Generate(List<ChatMessage> messages, string systemPrompt)
        {
            // Ensure local engine is initialized
            if (_localEngine == null)
            {
                if (!InitializeLocalEngine())
                {
                    return "[Local Engine Error] Failed to initialize local model engine.\n\n" +
                           "This may be due to:\n" +
                           "- Insufficient RAM (need at least 4GB free)\n" +
                           "- Model download failed - check internet connection\n" +
                           "- llama.cpp server failed to start\n\n" +
                           "Try restarting GIDE or run '/install' to re-download components.";
                }
            }

            return _localEngine.Generate(messages, systemPrompt, _modelRecommendation.ContextLength);
        }

        public void SwitchModel(string model)
        {
            if (!string.IsNullOrEmpty(model))
            {
                CurrentModel = model;
                _modelRecommendation = ModelManager.GetModelInfo(model);
                CurrentModelDisplayName = _modelRecommendation.DisplayName;
            }

            // Re-initialize with new model
            if (_localEngine != null)
                _localEngine.Shutdown();
            _localEngine = null;

            Console.WriteLine("  → Switched to: " + CurrentModelDisplayName);

            // Auto-initialize new model
            if (!InitializeLocalEngine())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Warning: Failed to load new model. May need to download first.");
                Console.ResetColor();
            }
        }

        public void PrintModelInfo()
        {
            HardwareDetector.PrintHardwareInfo();
        }

        public void PrintStatus()
        {
            Console.WriteLine("  Current model: " + CurrentModelDisplayName);
            Console.WriteLine("  Model ID: " + CurrentModel);
            Console.WriteLine("  Context length: " + _modelRecommendation.ContextLength + " tokens");
            Console.WriteLine("  Engine: " + (_localEngine != null ? "Running" : "Not initialized"));
        }

        public void Shutdown()
        {
            if (_localEngine != null)
            {
                _localEngine.Shutdown();
                _localEngine = null;
            }
        }
    }

    public class ChatMessage
    {
        public string Role;
        public string Content;
    }
}
