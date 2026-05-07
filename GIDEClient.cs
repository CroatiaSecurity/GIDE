using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Web.Script.Serialization;

namespace GIDE
{
    public class GIDEClient
    {
        public string CurrentModel = "qwen3:14b";
        public string Backend = "local";

        private HttpClient _http = new HttpClient();
        private string _apiKey;

        public GIDEClient()
        {
            _apiKey = Config.LoadApiKey();
            // Increase timeout for large responses
            _http.Timeout = TimeSpan.FromMinutes(5);
        }

        public string Generate(List<ChatMessage> messages, string systemPrompt)
        {
            if (Backend == "local")
                return GenerateOllama(messages, systemPrompt);
            else
                return GenerateOpenRouter(messages, systemPrompt);
        }

        private string GenerateOllama(List<ChatMessage> messages, string systemPrompt)
        {
            try
            {
                // Ensure Ollama is running
                if (!EnsureOllamaRunning())
                {
                    return "[Ollama Error] Ollama service is not running.\n\n" +
                           "Please start Ollama by:\n" +
                           "1. Open Command Prompt as Administrator\n" +
                           "2. Run: ollama serve\n" +
                           "3. Keep the window open\n" +
                           "4. Restart GIDE\n\n" +
                           "Or use '/cloud' to switch to cloud mode.";
                }

                string payload = "{\"model\":\"" + CurrentModel + "\",\"messages\":" + 
                    SimpleSerialize(messages, systemPrompt) + ",\"stream\":false,\"options\":{\"temperature\":0.6}}";

                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = _http.PostAsync("http://localhost:11434/api/chat", content).Result;

                if (response.IsSuccessStatusCode)
                {
                    string json = response.Content.ReadAsStringAsync().Result;
                    
                    // Try to parse using JSON serializer for better reliability
                    try
                    {
                        var serializer = new JavaScriptSerializer();
                        var result = serializer.Deserialize<Dictionary<string, object>>(json);
                        if (result.ContainsKey("message"))
                        {
                            var message = result["message"] as Dictionary<string, object>;
                            if (message != null && message.ContainsKey("content"))
                                return StripThinkTags(message["content"].ToString());
                        }
                    }
                    catch { }
                    
                    // Fallback to string parsing
                    int start = json.IndexOf("\"content\":\"") + 11;
                    if (start > 11)
                    {
                        int end = json.IndexOf("\"", start);
                        if (end > start)
                            return StripThinkTags(json.Substring(start, end - start).Replace("\\n", "\n").Replace("\\\"", "\""));
                    }
                    
                    return "[Ollama Error] Could not parse response";
                }
                
                return "[Ollama Error] HTTP " + response.StatusCode + " - Make sure Ollama is running";
            }
            catch (Exception ex)
            {
                return "[Ollama Failed] " + ex.Message;
            }
        }

        private string GenerateOpenRouter(List<ChatMessage> messages, string systemPrompt)
        {
            // Check for API key
            if (string.IsNullOrEmpty(_apiKey))
            {
                _apiKey = Config.SetupApiKey();
                if (string.IsNullOrEmpty(_apiKey))
                    return "[OpenRouter Error] No API key provided. Please set up your API key.";
            }

            try
            {
                // Build the request payload
                var requestBody = new Dictionary<string, object>();
                requestBody.Add("model", "deepseek/deepseek-r1-0528");
                requestBody.Add("messages", BuildMessagesArray(messages, systemPrompt));
                requestBody.Add("temperature", 0.6);
                requestBody.Add("max_tokens", 8192);

                var serializer = new JavaScriptSerializer();
                string payload = serializer.Serialize(requestBody);
                
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("Authorization", "Bearer " + _apiKey);
                _http.DefaultRequestHeaders.Add("User-Agent", "GIDE/2.6.0");

                var response = _http.PostAsync("https://openrouter.ai/api/v1/chat/completions", content).Result;
                string json = response.Content.ReadAsStringAsync().Result;

                if (!response.IsSuccessStatusCode)
                {
                    return "[OpenRouter Error] HTTP " + response.StatusCode + "\n" + 
                           "Response: " + json + "\n\n" +
                           "Check your API key at: https://openrouter.ai/keys";
                }

                // Parse the response
                try
                {
                    var result = serializer.Deserialize<Dictionary<string, object>>(json);
                    if (result.ContainsKey("choices"))
                    {
                        var choices = result["choices"] as object[];
                        if (choices != null && choices.Length > 0)
                        {
                            var choice = choices[0] as Dictionary<string, object>;
                            if (choice != null && choice.ContainsKey("message"))
                            {
                                var message = choice["message"] as Dictionary<string, object>;
                                if (message != null && message.ContainsKey("content"))
                                {
                                    string content_text = message["content"].ToString();
                                    return content_text.Replace("\\n", "\n").Replace("\\\"", "\"");
                                }
                            }
                        }
                    }
                    
                    return "[OpenRouter Error] Could not parse response: " + json.Substring(0, Math.Min(200, json.Length));
                }
                catch (Exception parseEx)
                {
                    return "[OpenRouter Error] Parse failed: " + parseEx.Message;
                }
            }
            catch (Exception ex)
            {
                return "[OpenRouter Error] " + ex.Message;
            }
        }

        private object[] BuildMessagesArray(List<ChatMessage> messages, string systemPrompt)
        {
            var messageList = new List<object>();
            
            // Add system message
            var systemMsg = new Dictionary<string, object>();
            systemMsg.Add("role", "system");
            systemMsg.Add("content", systemPrompt);
            messageList.Add(systemMsg);
            
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

        private string SimpleSerialize(List<ChatMessage> messages, string systemPrompt)
        {
            string result = "[{\"role\":\"system\",\"content\":\"" + EscapeJson(systemPrompt) + "\"}";
            foreach (ChatMessage msg in messages)
            {
                result += ",{\"role\":\"" + msg.Role + "\",\"content\":\"" + EscapeJson(msg.Content) + "\"}";
            }
            result += "]";
            return result;
        }

        private string StripThinkTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // Remove <think>...</think> blocks (qwen3 thinking mode output)
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

        private string EscapeJson(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            
            return text.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\n", "\\n")
                      .Replace("\r", "\\r")
                      .Replace("\t", "\\t");
        }

        private bool EnsureOllamaRunning()
        {
            try
            {
                // Quick check if Ollama is responding
                var checkResponse = _http.GetAsync("http://localhost:11434/api/tags").Result;
                if (checkResponse.IsSuccessStatusCode)
                    return true;
                    
                // Try to start Ollama
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "ollama",
                        Arguments = "serve",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                    System.Threading.Thread.Sleep(3000);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public void SwitchToCloud()
        {
            Backend = "openrouter";
            Console.WriteLine("  → Switched to Cloud (OpenRouter DeepSeek R1)");
            Console.WriteLine("  Make sure you have an API key from https://openrouter.ai/keys");
        }

        public void SwitchToLocal(string model)
        {
            Backend = "local";
            if (!string.IsNullOrEmpty(model))
                CurrentModel = model;
            Console.WriteLine("  → Switched to Local Ollama: " + CurrentModel);
        }
    }

    public class ChatMessage
    {
        public string Role;
        public string Content;
    }
}