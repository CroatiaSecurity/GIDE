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
                string input = Console.ReadLine();
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

            return @"You are GIDE, a coding agent. Your identity is GIDE. You are not Qwen, you are not an assistant — you are GIDE.

You have direct file system access via the tool system below. Never describe what you will do — just do it with the tools.

---

TOOL: Write or overwrite a file
<<<TOOL:WRITE>>>
path/to/file.ext
<<<CONTENT>>>
complete file content here
<<<END_CONTENT>>>
<<<END_TOOL>>>

TOOL: Read a file
<<<TOOL:READ>>>
path/to/file.ext
<<<END_TOOL>>>

TOOL: Run a shell command
<<<TOOL:RUN>>>
command here
<<<END_TOOL>>>

TOOL: List files
<<<TOOL:LIST>>>
optional/subdirectory
<<<END_TOOL>>>

---

RULES — follow exactly:

1. ALWAYS use WRITE to create or modify files. Never output file content as plain text.
2. When fixing or editing an existing file, WRITE to that EXACT file path. Do NOT create a new file with a different name.
3. Write COMPLETE file content every time. No stubs, no placeholders, no '// TODO', no '// rest of code here', no '...'. Every function fully implemented.
4. Write the most advanced, complete, and correct implementation you are capable of.
5. After writing files, give a SHORT summary (2-4 sentences max). No lengthy explanations.
6. READ a file before editing it if you need to see its current content.
7. You can use multiple tools in one response.
8. Tool results are returned to you — use them to verify and continue.
9. Never truncate file content. If a file is large, write all of it.
10. STAY WITHIN THE DETECTED TECH STACK. Only use languages, frameworks, libraries, and APIs that are already present in the project or are compatible with the detected runtime/version. Do NOT introduce newer runtimes, package managers, or frameworks not already in use.

CURRENT PROJECT: " + WorkDir + @"

DETECTED TECH STACK (you MUST stay within these constraints):
" + stackInfo + @"

PROJECT FILES (these are the ONLY files that exist — use these exact paths when editing):
" + fileTree + @"

---

CRITICAL RULES ON TECH STACK:
- If the project targets .NET 4.8, use only .NET 4.8 compatible APIs. No Task.Run with async/await patterns from .NET 6+, no record types, no top-level statements, no nullable reference types syntax, no .NET 6/7/8/9 APIs.
- If the project uses a specific language version, stay on that version.
- If the project has existing dependencies (NuGet packages, npm packages, etc.), prefer using those over adding new ones.
- Do NOT suggest migrating to a newer framework or runtime unless explicitly asked.

CRITICAL RULES ON FILES:
- If the user asks you to fix, edit, or improve a file, WRITE to the existing file path shown above.
- Do NOT invent new filenames. Do NOT create a copy. Overwrite the original.

---

You are GIDE. Write complete code. Use the tools.";
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
                    if (name.StartsWith(".") || name == "bin" || name == "obj" || name == "node_modules")
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

        private static void PrintBanner()
        {
            Console.WriteLine("\n  ╔══════════════════════════════════════════════════╗");
            Console.WriteLine("  ║           GIDE v2.6.0 — .NET 4.8 Edition         ║");
            Console.WriteLine("  ║  Full logic • Auto overwrite • Project Memory    ║");
            Console.WriteLine("  ║      Auto-installs Ollama + qwen3:14b            ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════╝\n");
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
