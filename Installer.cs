using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GIDE
{
    /// <summary>
    /// Legacy installer - kept for backwards compatibility.
    /// GIDE v0.4.0+ uses LocalModelEngine which is self-contained.
    /// </summary>
    public static class Installer
    {
        /// <summary>
        /// Legacy backend check - now just returns true as we no longer use this.
        /// </summary>
        public static bool EnsureBackendInstalled()
        {
            // No longer needed - GIDE uses self-contained LocalModelEngine
            return true;
        }

        /// <summary>
        /// Legacy service check - no longer needed.
        /// </summary>
        public static bool EnsureBackendRunning()
        {
            // No longer needed - LocalModelEngine manages its own server process
            return true;
        }

        /// <summary>
        /// Legacy model check - redirects to ModelManager.
        /// </summary>
        public static bool EnsureModelInstalled(string modelName)
        {
            // Normalize model names to expected format if needed
            string modelId = modelName.Replace(":", "-");

            var modelInfo = ModelManager.GetModelInfo(modelId);
            return ModelManager.EnsureModel(modelInfo);
        }
    }
}