using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GIDE
{
    /// <summary>
    /// Legacy installer - kept for backwards compatibility.
    /// GIDE v0.3.0+ uses LocalModelEngine which is self-contained.
    /// </summary>
    public static class Installer
    {
        /// <summary>
        /// Legacy Ollama check - now just returns true as we no longer use Ollama.
        /// </summary>
        public static bool EnsureOllamaInstalled()
        {
            // No longer needed - GIDE uses self-contained LocalModelEngine
            return true;
        }

        /// <summary>
        /// Legacy Ollama service check - no longer needed.
        /// </summary>
        public static bool EnsureOllamaRunning()
        {
            // No longer needed - LocalModelEngine manages its own server process
            return true;
        }

        /// <summary>
        /// Legacy model check - redirects to ModelManager.
        /// </summary>
        public static bool EnsureModelInstalled(string modelName)
        {
            // Convert old Ollama model names to new format if needed
            string modelId = modelName.Replace(":", "-");

            var modelInfo = ModelManager.GetModelInfo(modelId);
            return ModelManager.EnsureModel(modelInfo);
        }
    }
}