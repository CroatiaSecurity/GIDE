using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GIDE
{
    public class ToolCall
    {
        public string Type;
        public string Path;
        public string Content;
        public string Command;
    }

    public static class ToolParser
    {
        public static List<ToolCall> Parse(string response)
        {
            var tools = new List<ToolCall>();

            MatchCollection matches = Regex.Matches(response,
                @"<<<TOOL:(.+?)>>>(.*?)<<<END_TOOL>>>",
                RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var tool = new ToolCall();
                tool.Type = match.Groups[1].Value.Trim().ToUpper();
                string body = match.Groups[2].Value;

                switch (tool.Type)
                {
                    case "WRITE":
                    {
                        // First line is the file path
                        string trimmed = body.TrimStart('\r', '\n', ' ');
                        int newline = trimmed.IndexOfAny(new char[] { '\n', '\r' });
                        if (newline < 0)
                        {
                            tool.Path = trimmed.Trim();
                            tool.Content = "";
                        }
                        else
                        {
                            tool.Path = trimmed.Substring(0, newline).Trim();
                            string rest = trimmed.Substring(newline + 1);

                            int contentStart = rest.IndexOf("<<<CONTENT>>>");
                            int contentEnd   = rest.IndexOf("<<<END_CONTENT>>>");

                            if (contentStart >= 0 && contentEnd > contentStart)
                            {
                                // Extract content between markers, preserving internal whitespace
                                // but stripping exactly one leading newline after the marker
                                string raw = rest.Substring(contentStart + 13, contentEnd - contentStart - 13);
                                if (raw.StartsWith("\r\n")) raw = raw.Substring(2);
                                else if (raw.StartsWith("\n"))  raw = raw.Substring(1);
                                // Strip only trailing newline added by the model after <<<END_CONTENT>>>
                                tool.Content = raw.TrimEnd('\r', '\n');
                            }
                            else
                            {
                                // Fallback: Try to extract from markdown code blocks (```lang ... ```)
                                // This handles cases where the model uses markdown instead of <<<CONTENT>>>
                                Match mdMatch = Regex.Match(rest, @"```[a-zA-Z]*\r?\n(.*?)```", RegexOptions.Singleline);
                                if (mdMatch.Success)
                                {
                                    tool.Content = mdMatch.Groups[1].Value.TrimEnd('\r', '\n');
                                }
                                else
                                {
                                    // No content markers and no markdown — use everything after the path line
                                    tool.Content = rest.TrimEnd('\r', '\n');
                                }
                            }
                        }
                        break;
                    }

                    case "READ":
                    case "LIST":
                        tool.Path = body.Trim();
                        break;

                    case "RUN":
                        tool.Command = body.Trim();
                        break;
                }

                tools.Add(tool);
            }

            return tools;
        }

        public static string StripTools(string response)
        {
            // Remove all tool blocks
            string result = Regex.Replace(response,
                @"<<<TOOL:(.+?)>>>(.*?)<<<END_TOOL>>>",
                "", RegexOptions.Singleline);

            return result.Trim();
        }
    }
}
