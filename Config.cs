using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace GIDE
{
    /// <summary>
    /// Configuration manager - kept for future settings.
    /// GIDE v0.4.0+ is local-only, no API keys needed.
    /// </summary>
    public static class Config
    {
        private static string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".gide", "config.json");

        /// <summary>
        /// Legacy API key loader - kept for backwards compatibility.
        /// Always returns null as GIDE no longer uses cloud services.
        /// </summary>
        public static string LoadApiKey()
        {
            // No longer needed - GIDE is local-only
            return null;
        }

        /// <summary>
        /// Legacy API key setup - no longer functional.
        /// </summary>
        [Obsolete("GIDE is now local-only. No API keys needed.")]
        public static string SetupApiKey()
        {
            Console.WriteLine("GIDE no longer requires API keys. All processing is local.");
            return null;
        }

        /// <summary>
        /// Legacy config save - kept for backwards compatibility.
        /// </summary>
        [Obsolete("GIDE is now local-only.")]
        public static void SaveConfig(string key)
        {
            // No-op - local-only mode
        }
    }
}