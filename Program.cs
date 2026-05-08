using System;
using System.IO;
using System.Text;

namespace GIDE
{
    class Program
    {
        private static string WorkDir = ".";

        static void Main(string[] args)
        {
            // Force TLS 1.2 for HTTPS requests (required for modern servers)
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            // Handle context menu / --dir argument
            if (args.Length > 0)
            {
                string arg = args[0];
                if (arg.StartsWith("--dir="))
                    arg = arg.Substring(6);
                else if (arg == "--dir" && args.Length > 1)
                    arg = args[1];

                WorkDir = Path.GetFullPath(arg);
            }
            else
            {
                WorkDir = Directory.GetCurrentDirectory();
            }

            Directory.CreateDirectory(WorkDir);

            Console.WriteLine("  Checking environment...");

            if (!Installer.EnsureOllamaInstalled())
            {
                Console.WriteLine("\n  Press any key to exit...");
                Console.ReadKey();
                return;
            }

            if (!Installer.EnsureOllamaRunning())
                Console.WriteLine("  Warning: Ollama service may not be running");

            if (!Installer.EnsureModelInstalled("qwen3:14b"))
                Console.WriteLine("\n  Warning: Model not available, some features may not work");

            Console.WriteLine();

            HistoryManager history = new HistoryManager(WorkDir);
            ToolExecutor executor = new ToolExecutor(WorkDir);
            GIDEClient client = new GIDEClient();

            PrintBanner();
            Console.WriteLine("  Project: " + WorkDir);
            Console.WriteLine();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("  You: ");
                Console.ResetColor();
                string input = ReadMultiLineInput();
                if (string.IsNullOrEmpty(input)) continue;

                if (input.StartsWith("/"))
                {
                    HandleCommand(input, history, client, executor);
                    continue;
                }

                history.AddUserMessage(input);

                int maxIterations = 50;

                for (int i = 0; i < maxIterations; i++)
                {
                    // Rebuild system prompt each turn so file list stays current
                    string systemPrompt = BuildSystemPrompt();

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  [Thinking... (turn " + (i + 1) + "/" + maxIterations + ")]");
                    Console.ResetColor();

                    string response = client.Generate(history.GetMessages(), systemPrompt);
                    var tools = ToolParser.Parse(response);
                    string display = ToolParser.StripTools(response);

                    if (!string.IsNullOrWhiteSpace(display))
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine(display);
                        Console.ResetColor();
                    }

                    if (tools.Count == 0)
                    {
                        if (string.IsNullOrWhiteSpace(display))
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("  [No file operations performed. Use explicit tool format.]");
                            Console.ResetColor();
                        }
                        history.AddAssistantMessage(response);
                        break;
                    }

                    string toolFeedback = "";

                    foreach (ToolCall tool in tools)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("\n  [Executing: " + tool.Type + (tool.Path != null ? " → " + tool.Path : "") + "]");
                        Console.ResetColor();

                        string result = executor.Execute(tool);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(result);
                        Console.ResetColor();

                        toolFeedback += "[" + tool.Type + "] " + tool.Path + tool.Command + ": " + result + "\n";
                    }

                    history.AddAssistantMessage(response);
                    history.AddUserMessage("Tool execution results:\n" + toolFeedback + "\nContinue with the task based on these results.");
                }
            }
        }

        private static string BuildSystemPrompt()
        {
            string fileTree = GetProjectFileTree();
            string stackInfo = DetectTechStack();
            string fileContents = ScanAllProjectFiles();

            return @"You are GIDE, a coding agent. You write files using tools, not markdown.

=== FILE WRITING FORMAT (MANDATORY) ===

To write ANY code, you MUST use this exact structure:

<<<TOOL:WRITE>>>
path/to/file.ext
<<<CONTENT>>>
complete file content here
<<<END_CONTENT>>>
<<<END_TOOL>>>

EXAMPLE — writing a C# file:

<<<TOOL:WRITE>>>
src/Example.cs
<<<CONTENT>>>
using System;

namespace Example
{
    class Program
    {
        static void Main() { Console.WriteLine(""Hello""); }
    }
}
<<<END_CONTENT>>>
<<<END_TOOL>>>

=== OTHER TOOLS ===

<<<TOOL:READ>>>
path/to/file.ext
<<<END_TOOL>>>

<<<TOOL:RUN>>>
command here
<<<END_TOOL>>>

<<<TOOL:LIST>>>
directory
<<<END_TOOL>>>

=== RULES ===

1. Use <<<TOOL:WRITE>>> with <<<CONTENT>>> markers for ALL code. Never output raw code.
2. Write COMPLETE files. No placeholders, no '// TODO', no '// ...', no truncation.
3. Use EXACT file paths from the project. Do not rename files.
4. READ before editing if you need current content.
5. Brief summary after writing (2-4 sentences).
6. Stay within detected tech stack constraints.

=== PROJECT INFO ===

CURRENT PROJECT: " + WorkDir + @"

TECH STACK (stay within these constraints):
" + stackInfo + @"

PROJECT FILES (use these exact paths):
" + fileTree + @"

=== IMPORTANT ===

- If targeting .NET 4.8: No async Main, no record types, no top-level statements, no .NET 6+ APIs.
- When editing a file, use the EXACT path from PROJECT FILES above.
- NEVER use markdown. ALWAYS use <<<TOOL:WRITE>>> for any code output.

=== ALL FILE CONTENTS (PRE-SCANNED) ===

" + fileContents + @"

=== END OF FILE CONTENTS ===";
        }

        private static string DetectTechStack()
        {
            var sb = new StringBuilder();

            try
            {
                // --- .csproj / .vbproj / .fsproj ---
                string[] projFiles = Directory.GetFiles(WorkDir, "*.*proj", SearchOption.AllDirectories);
                foreach (string proj in projFiles)
                {
                    string name = Path.GetFileName(proj);
                    string skip = Path.GetFileName(Path.GetDirectoryName(proj));
                    if (skip == "bin" || skip == "obj") continue;

                    string content = File.ReadAllText(proj);
                    sb.AppendLine("Project file: " + name);

                    // Target framework
                    string tf = ExtractXmlValue(content, "TargetFramework");
                    if (string.IsNullOrEmpty(tf))
                        tf = ExtractXmlValue(content, "TargetFrameworkVersion");
                    if (!string.IsNullOrEmpty(tf))
                        sb.AppendLine("  Target framework: " + tf);

                    // Language version
                    string lv = ExtractXmlValue(content, "LangVersion");
                    if (!string.IsNullOrEmpty(lv))
                        sb.AppendLine("  Language version: " + lv);

                    // Output type
                    string ot = ExtractXmlValue(content, "OutputType");
                    if (!string.IsNullOrEmpty(ot))
                        sb.AppendLine("  Output type: " + ot);

                    // NuGet package references
                    var pkgMatches = System.Text.RegularExpressions.Regex.Matches(content,
                        @"<PackageReference\s+Include=""([^""]+)""\s+Version=""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    foreach (System.Text.RegularExpressions.Match m in pkgMatches)
                        sb.AppendLine("  NuGet: " + m.Groups[1].Value + " " + m.Groups[2].Value);

                    // Legacy packages.config references
                    string pkgConfig = Path.Combine(Path.GetDirectoryName(proj), "packages.config");
                    if (File.Exists(pkgConfig))
                    {
                        string pkgContent = File.ReadAllText(pkgConfig);
                        var legacyMatches = System.Text.RegularExpressions.Regex.Matches(pkgContent,
                            @"<package\s+id=""([^""]+)""\s+version=""([^""]+)""",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        foreach (System.Text.RegularExpressions.Match m in legacyMatches)
                            sb.AppendLine("  NuGet (packages.config): " + m.Groups[1].Value + " " + m.Groups[2].Value);
                    }
                }

                // --- package.json (Node/JS/TS) ---
                string[] pkgJsonFiles = Directory.GetFiles(WorkDir, "package.json", SearchOption.AllDirectories);
                foreach (string pkgJson in pkgJsonFiles)
                {
                    string dir = Path.GetFileName(Path.GetDirectoryName(pkgJson));
                    if (dir == "node_modules") continue;

                    string content = File.ReadAllText(pkgJson);
                    sb.AppendLine("Node.js project (package.json): " + pkgJson.Substring(WorkDir.Length).TrimStart('\\', '/'));

                    string nodeVer = ExtractJsonValue(content, "engines", "node");
                    if (!string.IsNullOrEmpty(nodeVer))
                        sb.AppendLine("  Node version: " + nodeVer);

                    // Detect TypeScript
                    if (content.Contains("\"typescript\""))
                        sb.AppendLine("  Language: TypeScript");
                    else
                        sb.AppendLine("  Language: JavaScript");

                    // Key frameworks
                    foreach (string fw in new[] { "react", "vue", "angular", "express", "next", "nuxt", "svelte", "electron" })
                        if (content.Contains("\"" + fw + "\""))
                            sb.AppendLine("  Framework: " + fw);
                }

                // --- requirements.txt / pyproject.toml (Python) ---
                if (File.Exists(Path.Combine(WorkDir, "requirements.txt")) ||
                    File.Exists(Path.Combine(WorkDir, "pyproject.toml")) ||
                    Directory.GetFiles(WorkDir, "*.py", SearchOption.TopDirectoryOnly).Length > 0)
                {
                    sb.AppendLine("Language: Python");
                    string pyproj = Path.Combine(WorkDir, "pyproject.toml");
                    if (File.Exists(pyproj))
                    {
                        string content = File.ReadAllText(pyproj);
                        string pyver = ExtractTomlValue(content, "python");
                        if (!string.IsNullOrEmpty(pyver))
                            sb.AppendLine("  Python version: " + pyver);
                    }
                }

                // --- Cargo.toml (Rust) ---
                string cargoPath = Path.Combine(WorkDir, "Cargo.toml");
                if (File.Exists(cargoPath))
                {
                    sb.AppendLine("Language: Rust");
                    string content = File.ReadAllText(cargoPath);
                    string edition = ExtractTomlValue(content, "edition");
                    if (!string.IsNullOrEmpty(edition))
                        sb.AppendLine("  Rust edition: " + edition);
                }

                // --- go.mod (Go) ---
                string goMod = Path.Combine(WorkDir, "go.mod");
                if (File.Exists(goMod))
                {
                    sb.AppendLine("Language: Go");
                    string content = File.ReadAllText(goMod);
                    var goVer = System.Text.RegularExpressions.Regex.Match(content, @"^go\s+([\d.]+)", System.Text.RegularExpressions.RegexOptions.Multiline);
                    if (goVer.Success)
                        sb.AppendLine("  Go version: " + goVer.Groups[1].Value);
                }

                // --- pom.xml (Java/Maven) ---
                if (File.Exists(Path.Combine(WorkDir, "pom.xml")))
                {
                    sb.AppendLine("Language: Java (Maven)");
                    string content = File.ReadAllText(Path.Combine(WorkDir, "pom.xml"));
                    string javaVer = ExtractXmlValue(content, "java.version");
                    if (string.IsNullOrEmpty(javaVer)) javaVer = ExtractXmlValue(content, "maven.compiler.source");
                    if (!string.IsNullOrEmpty(javaVer))
                        sb.AppendLine("  Java version: " + javaVer);
                }

                // --- build.gradle (Java/Kotlin/Gradle) ---
                string[] gradleFiles = Directory.GetFiles(WorkDir, "build.gradle*", SearchOption.TopDirectoryOnly);
                if (gradleFiles.Length > 0)
                {
                    sb.AppendLine("Build system: Gradle");
                    string content = File.ReadAllText(gradleFiles[0]);
                    if (content.Contains("kotlin")) sb.AppendLine("  Language: Kotlin");
                    else sb.AppendLine("  Language: Java");
                }
            }
            catch { /* non-fatal */ }

            string result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result)
                ? "(no project files detected — infer stack from existing source files)"
                : result;
        }

        // Extracts the text content of the first matching XML element
        private static string ExtractXmlValue(string xml, string tag)
        {
            var m = System.Text.RegularExpressions.Regex.Match(xml,
                @"<" + tag + @"[^>]*>\s*([^<]+)\s*</" + tag + @">",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        // Extracts a value from a simple JSON object (one level deep)
        private static string ExtractJsonValue(string json, string section, string key)
        {
            // Find section block first, then key within it
            var secMatch = System.Text.RegularExpressions.Regex.Match(json,
                "\"" + section + "\"\\s*:\\s*\\{([^}]+)\\}");
            string block = secMatch.Success ? secMatch.Groups[1].Value : json;
            var m = System.Text.RegularExpressions.Regex.Match(block,
                "\"" + key + "\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        // Extracts a value from a TOML file (key = "value")
        private static string ExtractTomlValue(string toml, string key)
        {
            var m = System.Text.RegularExpressions.Regex.Match(toml,
                @"^" + key + @"\s*=\s*""([^""]+)""",
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }

        private static string GetProjectFileTree()
        {
            try
            {
                var sb = new StringBuilder();
                string[] entries = Directory.GetFileSystemEntries(WorkDir, "*", SearchOption.AllDirectories);
                Array.Sort(entries);

                foreach (string entry in entries)
                {
                    // Skip hidden/system dirs
                    string name = Path.GetFileName(entry);
                    string parentDir = Path.GetFileName(Path.GetDirectoryName(entry));
                    if (name.StartsWith(".") || name == "bin" || name == "obj" || name == "node_modules" ||
                        parentDir == "bin" || parentDir == "obj" || parentDir == "node_modules")
                        continue;

                    string rel = entry.Substring(WorkDir.Length).TrimStart(Path.DirectorySeparatorChar);
                    bool isDir = Directory.Exists(entry);
                    sb.AppendLine(isDir ? "[dir]  " + rel : "       " + rel);
                }

                string result = sb.ToString().Trim();
                return string.IsNullOrEmpty(result) ? "(empty project)" : result;
            }
            catch
            {
                return "(could not read project files)";
            }
        }

        private static string ScanAllProjectFiles()
        {
            var sb = new StringBuilder();
            string[] codeExtensions = new[] { ".cs", ".vb", ".fs", ".py", ".js", ".ts", ".jsx", ".tsx", ".java", ".kt", ".go", ".rs", ".c", ".cpp", ".h", ".hpp", ".rb", ".php", ".swift", ".m", ".mm", ".lua", ".pl", ".sh", ".bat", ".ps1", ".sql", ".html", ".htm", ".css", ".scss", ".sass", ".less", ".json", ".xml", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf", ".md", ".txt", ".dockerfile", ".csproj", ".vbproj", ".fsproj", ".sln" };

            try
            {
                string[] files = Directory.GetFiles(WorkDir, "*", SearchOption.AllDirectories);
                Array.Sort(files);

                foreach (string file in files)
                {
                    string name = Path.GetFileName(file);
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    string parentDir = Path.GetFileName(Path.GetDirectoryName(file));

                    // Skip hidden, binary, and system directories
                    if (name.StartsWith(".") || parentDir == "bin" || parentDir == "obj" || 
                        parentDir == "node_modules" || parentDir == ".git" || parentDir == ".vs" ||
                        parentDir == "packages" || parentDir == "debug" || parentDir == "release")
                        continue;

                    // Only include recognized code/text file extensions
                    bool isCodeFile = false;
                    foreach (string codeExt in codeExtensions)
                    {
                        if (ext == codeExt)
                        {
                            isCodeFile = true;
                            break;
                        }
                    }
                    if (!isCodeFile) continue;

                    // Skip very large files (>100KB)
                    FileInfo fi = new FileInfo(file);
                    if (fi.Length > 100 * 1024) continue;

                    string rel = file.Substring(WorkDir.Length).TrimStart(Path.DirectorySeparatorChar);

                    try
                    {
                        string content = File.ReadAllText(file);
                        sb.AppendLine("=== FILE: " + rel + " ===");
                        sb.AppendLine(content);
                        sb.AppendLine();
                    }
                    catch { /* skip unreadable files */ }
                }
            }
            catch { /* non-fatal */ }

            string result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "(no readable code files found)" : result;
        }

        private static string ReadMultiLineInput()
        {
            var sb = new StringBuilder();
            bool firstLine = true;
            int emptyLineCount = 0;

            while (true)
            {
                string line = Console.ReadLine();

                // Single line input: if first line is non-empty and user just presses enter, return it
                if (firstLine && !string.IsNullOrEmpty(line))
                {
                    sb.AppendLine(line);
                    firstLine = false;
                    emptyLineCount = 0;
                    continue;
                }

                if (firstLine && string.IsNullOrEmpty(line))
                {
                    // Empty first line, just return empty
                    return "";
                }

                // For multi-line: two consecutive empty lines ends input
                if (string.IsNullOrEmpty(line))
                {
                    emptyLineCount++;
                    if (emptyLineCount >= 2)
                    {
                        // End of input
                        break;
                    }
                    sb.AppendLine(); // preserve single empty line
                }
                else
                {
                    emptyLineCount = 0;
                    sb.AppendLine(line);
                }

                // Show continuation prompt
                if (!firstLine)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("  ...  ");
                    Console.ResetColor();
                }
            }

            return sb.ToString().TrimEnd('\r', '\n', ' ');
        }

        private static void PrintBanner()
        {
            Console.WriteLine("\n  ╔══════════════════════════════════════════════════╗");
            Console.WriteLine("  ║           GIDE v2.7.0 — .NET 4.8 Edition         ║");
            Console.WriteLine("  ║  Full logic • Auto overwrite • Project Memory    ║");
            Console.WriteLine("  ║  Auto file scan • Multi-line paste support       ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════╝");
            Console.WriteLine("  Tip: Paste multiple lines, then press Enter twice to submit.\n");
        }

        private static void HandleCommand(string cmd, HistoryManager history, GIDEClient client, ToolExecutor executor)
        {
            if (cmd == "/clear")
            {
                history.Clear();
                Console.WriteLine("  [!] History cleared.");
            }
            else if (cmd.StartsWith("/cloud"))
            {
                client.SwitchToCloud();
            }
            else if (cmd.StartsWith("/local"))
            {
                client.SwitchToLocal("qwen3:14b");
            }
            else if (cmd == "/install")
            {
                Console.WriteLine("  Re-checking Ollama installation...");
                Installer.EnsureOllamaInstalled();
                Installer.EnsureOllamaRunning();
                Installer.EnsureModelInstalled("qwen3:14b");
            }
            else if (cmd == "/files")
            {
                Console.WriteLine(GetProjectFileTree());
            }
            else if (cmd == "/help")
            {
                Console.WriteLine("  /local   - Use local Ollama");
                Console.WriteLine("  /cloud   - Use OpenRouter (DeepSeek R1)");
                Console.WriteLine("  /clear   - Clear conversation history");
                Console.WriteLine("  /files   - List project files");
                Console.WriteLine("  /install - Reinstall/check Ollama");
                Console.WriteLine("  /help    - Show this help");
            }
            else
            {
                Console.WriteLine("  Unknown command. Type /help for options.");
            }
        }
    }
}
