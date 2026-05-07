using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace GIDE
{
    public static class Config
    {
        private static string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
            ".gide", "config.json");

        public static string LoadApiKey()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    var serializer = new JavaScriptSerializer();
                    var config = serializer.Deserialize<Dictionary<string, object>>(json);
                    
                    if (config != null && config.ContainsKey("api_key"))
                        return config["api_key"].ToString();
                }
                catch { }
            }
            return null;
        }

        public static string SetupApiKey()
        {
            string dir = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            Console.WriteLine("\n=== GIDE OpenRouter Setup ===");
            Console.WriteLine("1. Go to: https://openrouter.ai/keys");
            Console.WriteLine("2. Create a free API key");
            Console.WriteLine("3. Copy the key (starts with 'sk-or-v1-')");
            Console.WriteLine();
            Console.Write("Paste your API Key: ");
            string key = Console.ReadLine().Trim();

            if (string.IsNullOrEmpty(key))
            {
                Console.WriteLine("No key provided. Cloud mode will not work.");
                return null;
            }

            var config = new Dictionary<string, object>();
            config.Add("api_key", key);
            config.Add("default_backend", "openrouter");
            
            var serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(config);
            File.WriteAllText(ConfigPath, json);

            Console.WriteLine("✓ API Key saved successfully!\n");
            return key;
        }
        
        public static void SaveConfig(string key)
        {
            string dir = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
                
            var config = new Dictionary<string, object>();
            config.Add("api_key", key);
            
            var serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(config);
            File.WriteAllText(ConfigPath, json);
        }
    }
}