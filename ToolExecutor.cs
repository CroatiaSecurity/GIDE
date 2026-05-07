using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GIDE
{
    public class ToolExecutor
    {
        private string _workDir;

        public ToolExecutor(string workDir)
        {
            _workDir = Path.GetFullPath(workDir);
        }

        public string Execute(ToolCall tool)
        {
            try
            {
                if (tool.Type.ToUpper() == "WRITE")
                    return WriteFile(tool.Path, tool.Content);
                else if (tool.Type.ToUpper() == "READ")
                    return ReadFile(tool.Path);
                else if (tool.Type.ToUpper() == "RUN")
                    return RunCommand(tool.Command);
                else if (tool.Type.ToUpper() == "LIST")
                    return ListFiles(tool.Path);
                else
                    return "[ERROR] Unknown tool: " + tool.Type;
            }
            catch (Exception ex)
            {
                return "[ERROR] " + tool.Type + ": " + ex.Message;
            }
        }

        private string WriteFile(string relativePath, string content)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return "[WRITE ERROR] No file path provided";

            if (content == null)
                content = "";

            // Resolve full path safely
            relativePath = relativePath.Trim().Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(_workDir, relativePath));

            // Safety: don't write outside the work directory
            if (!fullPath.StartsWith(_workDir, StringComparison.OrdinalIgnoreCase))
                return "[WRITE ERROR] Path escapes work directory: " + relativePath;

            // Ensure parent directory exists
            string dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Clear read-only attribute if set — common cause of silent write failures
            if (File.Exists(fullPath))
            {
                try
                {
                    FileAttributes attrs = File.GetAttributes(fullPath);
                    if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        File.SetAttributes(fullPath, attrs & ~FileAttributes.ReadOnly);
                }
                catch { /* non-fatal, attempt write anyway */ }
            }

            // Write with retry — handles transient locks (antivirus, indexers, etc.)
            Exception lastEx = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    // Write to a temp file first, then atomic-replace
                    string tempPath = fullPath + ".gide_tmp";
                    File.WriteAllText(tempPath, content, new UTF8Encoding(false));

                    // Delete destination and move temp into place
                    if (File.Exists(fullPath))
                        File.Delete(fullPath);

                    File.Move(tempPath, fullPath);

                    long written = new FileInfo(fullPath).Length;
                    return string.Format("[WRITE OK] {0} ({1} bytes)", relativePath, written);
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    if (attempt < 3)
                        System.Threading.Thread.Sleep(300 * attempt);
                }
            }

            return "[WRITE FAILED] " + relativePath + " — " + (lastEx != null ? lastEx.Message : "unknown error");
        }

        private string ReadFile(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return "[READ ERROR] No file path provided";

            relativePath = relativePath.Trim().Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(_workDir, relativePath));

            if (!File.Exists(fullPath))
                return "[READ ERROR] File not found: " + relativePath;

            try
            {
                string text = File.ReadAllText(fullPath, Encoding.UTF8);
                return "[READ] " + relativePath + " (" + text.Length + " chars)\n" + text;
            }
            catch (Exception ex)
            {
                return "[READ ERROR] " + relativePath + " — " + ex.Message;
            }
        }

        private string RunCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return "[RUN ERROR] No command provided";

            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + command);
                psi.WorkingDirectory = _workDir;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                using (var process = Process.Start(psi))
                {
                    // Read both streams concurrently to avoid deadlock on large output
                    string stdout = "";
                    string stderr = "";
                    var outTask = System.Threading.Tasks.Task.Run(() => stdout = process.StandardOutput.ReadToEnd());
                    var errTask = System.Threading.Tasks.Task.Run(() => stderr = process.StandardError.ReadToEnd());

                    process.WaitForExit(30000); // 30s timeout
                    System.Threading.Tasks.Task.WaitAll(outTask, errTask);

                    string combined = stdout;
                    if (!string.IsNullOrEmpty(stderr))
                        combined += (combined.Length > 0 ? "\n" : "") + "[stderr] " + stderr;

                    int exitCode = process.ExitCode;
                    string status = exitCode == 0 ? "[RUN OK]" : "[RUN EXIT " + exitCode + "]";
                    return status + " " + command + "\n" + combined.Trim();
                }
            }
            catch (Exception ex)
            {
                return "[RUN ERROR] " + command + " — " + ex.Message;
            }
        }

        private string ListFiles(string path)
        {
            try
            {
                string targetPath = string.IsNullOrWhiteSpace(path)
                    ? _workDir
                    : Path.GetFullPath(Path.Combine(_workDir, path.Trim()));

                if (!Directory.Exists(targetPath))
                    return "[LIST ERROR] Directory not found: " + path;

                var sb = new StringBuilder();
                sb.AppendLine("[LIST] " + targetPath);

                foreach (string entry in Directory.GetFileSystemEntries(targetPath, "*", SearchOption.AllDirectories))
                {
                    string rel = entry.Substring(_workDir.Length).TrimStart(Path.DirectorySeparatorChar);
                    bool isDir = Directory.Exists(entry);
                    sb.AppendLine((isDir ? "[DIR]  " : "[FILE] ") + rel);
                }

                return sb.ToString().Trim();
            }
            catch (Exception ex)
            {
                return "[LIST ERROR] " + ex.Message;
            }
        }
    }
}
