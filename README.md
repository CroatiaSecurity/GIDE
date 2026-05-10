# GIDE v0.4.0
**Gorstak's IDE** — a free, fully local AI coding assistant for Windows, built on .NET 4.8.

GIDE runs entirely on your machine using [llama.cpp](https://github.com/ggml-org/llama.cpp) and Qwen3 models. No cloud, no API keys, no usage limits. Point it at a project folder, describe what you want, and it assists with code directly.

---

## Features

- **100% local** — no API keys, no internet required after setup, no data leaves your machine.
- **Auto-downloads llama.cpp** — always fetches the latest release from GitHub automatically.
- **Qwen3 model support** — choose from 4B, 8B, 14B, or 30B AWQ models depending on your hardware.
- **Hardware-aware** — detects RAM, CPU cores, and CUDA GPU to pick the optimal model and build.
- **Auto port selection** — finds a free port automatically, no manual configuration needed.
- **ChatGPT-style UI** — centered input, inline chat history, quick action buttons.
- **Project browser** — open a folder and GIDE will scan and provide context about your codebase.
- **Context menu integration** — right-click any folder in Explorer and choose "Run GIDE here".

---

## Requirements

- Windows 7 SP1 or later (64-bit)
- [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) (pre-installed on Windows 10/11)
- Free disk space for models:
  - Qwen3 4B: ~2.5 GB
  - Qwen3 8B: ~5 GB
  - Qwen3 14B: ~9 GB *(recommended)*
  - Qwen3 30B AWQ: ~18 GB
- RAM: 4 GB minimum, 16 GB recommended for 14B model
- Internet connection on first launch (to download llama.cpp and chosen model)

---

## Installation

### Option A — Installer (recommended)
Download `GIDE-Setup-0.4.0.exe` from the [releases page](https://github.com/CroatiaSecurity/GIDE/releases) and run it.

The installer:
- Installs GIDE to `Program Files\GIDE`
- Adds "Run GIDE here" to the folder right-click context menu
- Creates Start Menu and optional desktop shortcuts

### Option B — Build from source
```
git clone https://github.com/CroatiaSecurity/GIDE
cd GIDE
build.cmd
```
The compiled binary lands at `dist\GIDE.exe`.

Requires the .NET Framework 4.8 SDK / `csc.exe` (included with Visual Studio or the [.NET 4.8 Developer Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)).

---

## First Launch

On first launch GIDE will automatically:
1. Query GitHub for the latest llama.cpp release and download it (~30 MB)
2. Prompt you to select and download a Qwen3 model
3. Start the local inference server and begin responding

This is a one-time setup. After that, GIDE starts instantly with no internet needed.

---

## Model Selection

Use the dropdown in the top-right corner to select a model. The recommended model for your hardware is pre-selected.

| Model | Size | RAM Required |
|---|---|---|
| Qwen3 4B | ~2.5 GB | 4 GB |
| Qwen3 8B | ~5 GB | 8 GB |
| Qwen3 14B *(recommended)* | ~9 GB | 16 GB |
| Qwen3 30B AWQ | ~18 GB | 32 GB / 12 GB VRAM |

Click **Download** to download a model. The status bar shows download progress.

---

## How it works

1. GIDE starts a local `llama-server` process (llama.cpp) with your selected model.
2. Your messages are sent to the local server via the OpenAI-compatible `/v1/chat/completions` API.
3. The model generates a response locally and streams it back to the UI.
4. Qwen3 `<think>` blocks are automatically stripped from the output.

---

## Data & Privacy

- All inference runs locally on your hardware.
- No telemetry, no analytics, no remote logging.
- Models are stored in `%USERPROFILE%\.gide\models\`.
- llama.cpp binaries are stored in `%USERPROFILE%\.gide\bin\`.

---

## Building the installer

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) installed to `C:\Program Files (x86)\Inno Setup 6\`.

```
build.cmd
```

Output: `releases\0.4.0\GIDE-Setup-0.4.0.exe`

---

## License

See [LICENSE](LICENSE).
