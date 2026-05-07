using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace GIDE
{
    public class HistoryManager
    {
        private string _historyPath;
        private List<ChatMessage> _messages = new List<ChatMessage>();

        public HistoryManager(string workDir)
        {
            _historyPath = Path.Combine(workDir, ".gide_history.json");
            Load();
        }

        public void AddUserMessage(string content)
        {
            ChatMessage msg = new ChatMessage();
            msg.Role = "user";
            msg.Content = content;
            _messages.Add(msg);
            Trim();
            Save();
        }

        public void AddAssistantMessage(string content)
        {
            ChatMessage msg = new ChatMessage();
            msg.Role = "assistant";
            msg.Content = content;
            _messages.Add(msg);
            Trim();
            Save();
        }

        private void Trim()
        {
            if (_messages.Count > 200)
                _messages.RemoveRange(0, _messages.Count - 200);
        }

        private void Load()
        {
            if (File.Exists(_historyPath))
            {
                try
                {
                    string json = File.ReadAllText(_historyPath);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    _messages = serializer.Deserialize<List<ChatMessage>>(json) ?? new List<ChatMessage>();
                }
                catch
                {
                    _messages = new List<ChatMessage>();
                }
            }
        }

        private void Save()
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(_messages);
                File.WriteAllText(_historyPath, json);
            }
            catch { }
        }

        public List<ChatMessage> GetMessages()
        {
            return _messages;
        }

        public void Clear()
        {
            _messages.Clear();
            Save();
        }
    }
}