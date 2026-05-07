<<<<<<< HEAD
# GIDE v2.6.0
**Gorstak's IDE** — a local AI coding agent for Windows, built on .NET 4.8.

GIDE runs entirely on your machine using [Ollama](https://ollama.com) and `qwen3:14b`. No cloud required by default. Point it at a project folder, describe what you want, and it reads, writes, and edits files directly on disk.

---

## Features

- **Full file writes** — no stubs, no placeholders, no `// TODO`. Every function is fully implemented.
- **Overwrites existing files** — edits the actual file in place, not a copy with a random name.
- **Project-aware** — scans your project files on every turn so it always knows what exists and uses the correct filenames.
- **Stack-aware** — detects your target framework, language version, and dependencies from `.csproj`, `package.json`, `Cargo.toml`, etc. Stays within that stack. Won't suggest .NET 6 APIs on a .NET 4.8 project.
- **Agentic loop** — executes tools (read, write, run), gets the results back, and keeps going until the task is done. Up to 50 turns per request.
- **Local by default** — uses Ollama + `qwen3:14b` locally. Switch to DeepSeek R1 via OpenRouter with `/cloud`.
- **Auto-installs** — downloads and sets up Ollama and the model on first run if they aren't present.
- **Context menu integration** — right-click any folder in Explorer and choose "Run GIDE here".

---

## Requirements

- Windows 7 SP1 or later (64-bit)
- [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) (pre-installed on Windows 10/11)
- ~10 GB free disk space for the model
- 8 GB RAM minimum, 16 GB recommended

Ollama and `qwen3:14b` are downloaded automatically on first run if not already installed.

---

## Installation

### Option A — Installer (recommended)
Download `GIDE-Setup-2.6.0.exe` from the [releases page](https://github.com/tandrlemandrle/GIDE/releases) and run it.

The installer:
- Installs GIDE to `Program Files\GIDE`
- Adds "Run GIDE here" to the folder right-click context menu
- Optionally adds GIDE to your system PATH
- Creates Start Menu and optional desktop shortcuts

### Option B — Build from source
```
git clone https://github.com/tandrlemandrle/GIDE
cd GIDE
build.cmd
```
The compiled binary lands at `dist\GIDE.exe`.

Requires the .NET Framework 4.8 SDK / `csc.exe` (included with Visual Studio or the .NET 4.8 Developer Pack).

---

## Usage

**From Explorer** — right-click a project folder → "Run GIDE here"

**From the command line:**
```
GIDE.exe [project folder]
GIDE.exe --dir "C:\Projects\MyApp"
```
If no folder is given, GIDE uses the current working directory.

On first launch GIDE checks for Ollama and the model, downloading them if needed. This is a one-time setup (~8–10 GB download for the model).

---

## Commands

| Command    | Description                                      |
|------------|--------------------------------------------------|
| `/local`   | Switch to local Ollama (`qwen3:14b`)             |
| `/cloud`   | Switch to OpenRouter (DeepSeek R1)               |
| `/clear`   | Clear conversation history                       |
| `/files`   | Print the current project file tree              |
| `/install` | Re-check and reinstall Ollama / model if needed  |
| `/help`    | Show command list                                |

---

## Cloud mode (OpenRouter)

Run `/cloud` and GIDE will prompt you for an [OpenRouter](https://openrouter.ai/keys) API key the first time. The key is saved to `~\.gide\config.json`. Cloud mode uses **DeepSeek R1** (`deepseek/deepseek-r1-0528`).

Switch back to local at any time with `/local`.

---

## How it works

1. You type a request.
2. GIDE builds a system prompt that includes the detected tech stack and a live file tree of your project.
3. The model responds with tool calls (`WRITE`, `READ`, `RUN`, `LIST`).
4. GIDE executes the tools, feeds the results back to the model, and loops until the task is complete or 50 turns are reached.
5. Files are written atomically (write to temp → delete original → move temp) with retry logic to handle antivirus/indexer locks.

---

## Building the installer

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) installed to `C:\Program Files (x86)\Inno Setup 6\`.

```
build.cmd
```

The installer is output to `bin\GIDE-Setup-2.6.0.exe`.

---

## License

See [LICENSE](LICENSE).
=======
# GIDE v2.6.0
**Gorstak's IDE** — a local AI coding agent for Windows, built on .NET 4.8.

GIDE runs entirely on your machine using [Ollama](https://ollama.com) and `qwen3:14b`. No cloud required by default. Point it at a project folder, describe what you want, and it reads, writes, and edits files directly on disk.

---

## Features

- **Full file writes** — no stubs, no placeholders, no `// TODO`. Every function is fully implemented.
- **Overwrites existing files** — edits the actual file in place, not a copy with a random name.
- **Project-aware** — scans your project files on every turn so it always knows what exists and uses the correct filenames.
- **Stack-aware** — detects your target framework, language version, and dependencies from `.csproj`, `package.json`, `Cargo.toml`, etc. Stays within that stack. Won't suggest .NET 6 APIs on a .NET 4.8 project.
- **Agentic loop** — executes tools (read, write, run), gets the results back, and keeps going until the task is done. Up to 50 turns per request.
- **Local by default** — uses Ollama + `qwen3:14b` locally. Switch to DeepSeek R1 via OpenRouter with `/cloud`.
- **Auto-installs** — downloads and sets up Ollama and the model on first run if they aren't present.
- **Context menu integration** — right-click any folder in Explorer and choose "Run GIDE here".

---

## Requirements

- Windows 7 SP1 or later (64-bit)
- [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) (pre-installed on Windows 10/11)
- ~10 GB free disk space for the model
- 8 GB RAM minimum, 16 GB recommended

Ollama and `qwen3:14b` are downloaded automatically on first run if not already installed.

---

## Installation

### Option A — Installer (recommended)
Download `GIDE-Setup-2.6.0.exe` from the [releases page](https://github.com/tandrlemandrle/GIDE/releases) and run it.

The installer:
- Installs GIDE to `Program Files\GIDE`
- Adds "Run GIDE here" to the folder right-click context menu
- Optionally adds GIDE to your system PATH
- Creates Start Menu and optional desktop shortcuts

### Option B — Build from source
```
git clone https://github.com/tandrlemandrle/GIDE
cd GIDE
build.cmd
```
The compiled binary lands at `dist\GIDE.exe`.

Requires the .NET Framework 4.8 SDK / `csc.exe` (included with Visual Studio or the .NET 4.8 Developer Pack).

---

## Usage

**From Explorer** — right-click a project folder → "Run GIDE here"

**From the command line:**
```
GIDE.exe [project folder]
GIDE.exe --dir "C:\Projects\MyApp"
```
If no folder is given, GIDE uses the current working directory.

On first launch GIDE checks for Ollama and the model, downloading them if needed. This is a one-time setup (~8–10 GB download for the model).

---

## Commands

| Command    | Description                                      |
|------------|--------------------------------------------------|
| `/local`   | Switch to local Ollama (`qwen3:14b`)             |
| `/cloud`   | Switch to OpenRouter (DeepSeek R1)               |
| `/clear`   | Clear conversation history                       |
| `/files`   | Print the current project file tree              |
| `/install` | Re-check and reinstall Ollama / model if needed  |
| `/help`    | Show command list                                |

---

## Cloud mode (OpenRouter)

Run `/cloud` and GIDE will prompt you for an [OpenRouter](https://openrouter.ai/keys) API key the first time. The key is saved to `~\.gide\config.json`. Cloud mode uses **DeepSeek R1** (`deepseek/deepseek-r1-0528`).

Switch back to local at any time with `/local`.

---

## How it works

1. You type a request.
2. GIDE builds a system prompt that includes the detected tech stack and a live file tree of your project.
3. The model responds with tool calls (`WRITE`, `READ`, `RUN`, `LIST`).
4. GIDE executes the tools, feeds the results back to the model, and loops until the task is complete or 50 turns are reached.
5. Files are written atomically (write to temp → delete original → move temp) with retry logic to handle antivirus/indexer locks.

---

## Building the installer

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) installed to `C:\Program Files (x86)\Inno Setup 6\`.

```
build.cmd
```

The installer is output to `bin\GIDE-Setup-2.6.0.exe`.

---

## License

See [LICENSE](LICENSE).
>>>>>>> 1d808e3d96d06e719300d7d1d43650cadce6e62e
