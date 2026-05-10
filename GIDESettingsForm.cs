using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace GIDE
{
    /// <summary>
    /// GIDE Settings and Model Management Form
    /// A GUI for downloading, selecting, and managing free local AI models
    /// </summary>
    public class GIDESettingsForm : Form
    {
        private ListView modelListView;
        private Label hardwareInfoLabel;
        private ProgressBar downloadProgressBar;
        private Label statusLabel;
        private Button downloadButton;
        private Button selectButton;
        private Button refreshButton;
        private Button closeButton;
        private GroupBox hardwareGroup;
        private GroupBox modelsGroup;

        private HardwareDetector.HardwareInfo _hardware;
        private List<FreeModelInfo> _availableModels;
        private Thread _downloadThread;
        private bool _isDownloading = false;

        /// <summary>
        /// Information about available free models
        /// </summary>
        public class FreeModelInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string Size { get; set; }
            public long SizeBytes { get; set; }
            public string MinRequirements { get; set; }
            public int MinRamGB { get; set; }
            public int MinVramGB { get; set; }
            public string DownloadUrl { get; set; }
            public string Filename { get; set; }
            public bool IsDownloaded { get; set; }
            public bool IsRecommended { get; set; }
        }

        public GIDESettingsForm()
        {
            _hardware = HardwareDetector.GetHardwareInfo();
            InitializeAvailableModels();
            InitializeComponent();
            RefreshModelList();
        }

        private void InitializeComponent()
        {
            this.Text = "GIDE Model Manager - Free Local AI Models";
            this.Size = new Size(900, 650);
            this.MinimumSize = new Size(800, 550);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = SystemIcons.Application;

            // Hardware Info Group
            hardwareGroup = new GroupBox
            {
                Text = "Your Hardware (Auto-Detected)",
                Location = new Point(12, 12),
                Size = new Size(860, 120),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            hardwareInfoLabel = new Label
            {
                Location = new Point(15, 25),
                Size = new Size(830, 85),
                Font = new Font("Consolas", 10),
                Text = GetHardwareDisplayText(),
                AutoSize = false
            };
            hardwareGroup.Controls.Add(hardwareInfoLabel);

            // Models Group
            modelsGroup = new GroupBox
            {
                Text = "Available Free Models (100% Offline, No API Keys)",
                Location = new Point(12, 145),
                Size = new Size(860, 380),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Model ListView
            modelListView = new ListView
            {
                Location = new Point(15, 25),
                Size = new Size(830, 300),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                MultiSelect = false
            };

            modelListView.Columns.Add("Status", 70);
            modelListView.Columns.Add("Model", 180);
            modelListView.Columns.Add("Size", 80);
            modelListView.Columns.Add("Requirements", 150);
            modelListView.Columns.Add("Description", 320);

            modelListView.SelectedIndexChanged += ModelListView_SelectedIndexChanged;
            modelListView.DoubleClick += ModelListView_DoubleClick;

            // Status label
            statusLabel = new Label
            {
                Location = new Point(15, 330),
                Size = new Size(500, 20),
                Text = "Ready",
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            // Progress bar
            downloadProgressBar = new ProgressBar
            {
                Location = new Point(520, 330),
                Size = new Size(325, 20),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Visible = false
            };

            modelsGroup.Controls.Add(modelListView);
            modelsGroup.Controls.Add(statusLabel);
            modelsGroup.Controls.Add(downloadProgressBar);

            // Buttons
            downloadButton = new Button
            {
                Text = "Download Selected Model",
                Location = new Point(12, 535),
                Size = new Size(180, 35),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Enabled = false
            };
            downloadButton.Click += DownloadButton_Click;

            selectButton = new Button
            {
                Text = "Use Selected Model",
                Location = new Point(205, 535),
                Size = new Size(150, 35),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Enabled = false
            };
            selectButton.Click += SelectButton_Click;

            refreshButton = new Button
            {
                Text = "Refresh List",
                Location = new Point(368, 535),
                Size = new Size(120, 35),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            refreshButton.Click += RefreshButton_Click;

            closeButton = new Button
            {
                Text = "Close",
                Location = new Point(752, 535),
                Size = new Size(120, 35),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            closeButton.Click += (s, e) => this.Close();

            // Help label
            Label helpLabel = new Label
            {
                Location = new Point(12, 580),
                Size = new Size(860, 25),
                Text = "All models are 100% free and run locally. No internet connection required after download. No API keys needed.",
                ForeColor = Color.DarkGreen,
                Font = new Font(this.Font, FontStyle.Italic),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            this.Controls.Add(hardwareGroup);
            this.Controls.Add(modelsGroup);
            this.Controls.Add(downloadButton);
            this.Controls.Add(selectButton);
            this.Controls.Add(refreshButton);
            this.Controls.Add(closeButton);
            this.Controls.Add(helpLabel);
        }

        private void InitializeAvailableModels()
        {
            _availableModels = new List<FreeModelInfo>
            {
                new FreeModelInfo
                {
                    Id = "qwen3-30b-awq",
                    Name = "Qwen3 30B AWQ",
                    Description = "Best quality coding. Large context window. Best for complex projects.",
                    Size = "~18 GB",
                    SizeBytes = 18L * 1024 * 1024 * 1024,
                    MinRamGB = 32,
                    MinVramGB = 12,
                    MinRequirements = "32GB RAM or 12GB VRAM",
                    Filename = "qwen3-30b-awq-q4_0.gguf",
                    DownloadUrl = "https://huggingface.co/Qwen/Qwen3-30B-AWQ-GGUF/resolve/main/qwen3-30b-awq-q4_0.gguf"
                },
                new FreeModelInfo
                {
                    Id = "qwen3-14b",
                    Name = "Qwen3 14B Q4_K_M",
                    Description = "Great balance of quality and speed. Good for most coding tasks.",
                    Size = "~9 GB",
                    SizeBytes = 9L * 1024 * 1024 * 1024,
                    MinRamGB = 16,
                    MinVramGB = 8,
                    MinRequirements = "16GB RAM or 8GB VRAM",
                    Filename = "qwen3-14b-q4_k_m.gguf",
                    DownloadUrl = "https://huggingface.co/Qwen/Qwen3-14B-GGUF/resolve/main/qwen3-14b-q4_k_m.gguf"
                },
                new FreeModelInfo
                {
                    Id = "qwen3-8b",
                    Name = "Qwen3 8B Q4_K_M",
                    Description = "Fast responses. Good quality. Ideal for mid-range systems.",
                    Size = "~5 GB",
                    SizeBytes = 5L * 1024 * 1024 * 1024,
                    MinRamGB = 8,
                    MinVramGB = 0,
                    MinRequirements = "8GB RAM",
                    Filename = "qwen3-8b-q4_k_m.gguf",
                    DownloadUrl = "https://huggingface.co/Qwen/Qwen3-8B-GGUF/resolve/main/qwen3-8b-q4_k_m.gguf"
                },
                new FreeModelInfo
                {
                    Id = "qwen3-4b",
                    Name = "Qwen3 4B Q4_K_M",
                    Description = "Lightweight and fast. Works on almost any system.",
                    Size = "~2.5 GB",
                    SizeBytes = 2L * 1024 * 1024 * 1024 + 512L * 1024 * 1024,
                    MinRamGB = 4,
                    MinVramGB = 0,
                    MinRequirements = "4GB RAM",
                    Filename = "qwen3-4b-q4_k_m.gguf",
                    DownloadUrl = "https://huggingface.co/Qwen/Qwen3-4B-GGUF/resolve/main/qwen3-4b-q4_k_m.gguf"
                }
            };
        }

        private string GetHardwareDisplayText()
        {
            long ramGB = (long)(_hardware.TotalRAM / (1024 * 1024 * 1024));
            long vramGB = _hardware.BestGPU != null ? (long)(_hardware.BestGPU.DedicatedVRAM / (1024 * 1024 * 1024)) : 0;

            string gpuText = _hardware.BestGPU != null
                ? string.Format("{0}\n  VRAM: {1} GB ({2})",
                    _hardware.BestGPU.Name,
                    vramGB,
                    _hardware.BestGPU.IsDiscrete ? "Discrete" : "Integrated")
                : "No GPU detected";

            return string.Format(
                "RAM: {0} GB\n" +
                "CPU Cores: {1}\n" +
                "GPU: {2}",
                ramGB,
                _hardware.CpuCores,
                gpuText
            );
        }

        private void RefreshModelList()
        {
            modelListView.Items.Clear();
            long ramGB = (long)(_hardware.TotalRAM / (1024 * 1024 * 1024));
            long vramGB = _hardware.BestGPU != null ? (long)(_hardware.BestGPU.DedicatedVRAM / (1024 * 1024 * 1024)) : 0;

            foreach (var model in _availableModels)
            {
                // Check if downloaded
                string modelPath = ModelManager.GetModelPath(model.Id);
                model.IsDownloaded = File.Exists(modelPath);

                // Check if recommended
                bool canRun = ramGB >= model.MinRamGB || vramGB >= model.MinVramGB;
                model.IsRecommended = canRun && (model.Id == GetRecommendedModelId());

                string status = model.IsDownloaded ? "[Downloaded]" : (canRun ? "[Available]" : "[Need RAM]");
                if (model.IsRecommended)
                    status = "[RECOMMENDED]";

                var item = new ListViewItem(new[]
                {
                    status,
                    model.Name,
                    model.Size,
                    model.MinRequirements,
                    model.Description
                });

                if (model.IsRecommended)
                {
                    item.BackColor = Color.LightGreen;
                    item.Font = new Font(modelListView.Font, FontStyle.Bold);
                }
                else if (model.IsDownloaded)
                {
                    item.BackColor = Color.LightYellow;
                }
                else if (!canRun)
                {
                    item.ForeColor = Color.Gray;
                }

                item.Tag = model;
                modelListView.Items.Add(item);
            }

            UpdateButtonStates();
        }

        private string GetRecommendedModelId()
        {
            var rec = HardwareDetector.RecommendModel(_hardware);
            return rec.ModelId;
        }

        private void ModelListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtonStates();
        }

        private void ModelListView_DoubleClick(object sender, EventArgs e)
        {
            if (modelListView.SelectedItems.Count > 0)
            {
                var model = (FreeModelInfo)modelListView.SelectedItems[0].Tag;
                if (model.IsDownloaded)
                {
                    SelectModel(model);
                }
                else
                {
                    StartDownload(model);
                }
            }
        }

        private void UpdateButtonStates()
        {
            if (modelListView.SelectedItems.Count == 0)
            {
                downloadButton.Enabled = false;
                selectButton.Enabled = false;
                return;
            }

            var model = (FreeModelInfo)modelListView.SelectedItems[0].Tag;
            downloadButton.Enabled = !model.IsDownloaded && !_isDownloading;
            selectButton.Enabled = model.IsDownloaded;

            downloadButton.Text = model.IsDownloaded ? "Already Downloaded" : "Download Selected Model";
        }

        private void DownloadButton_Click(object sender, EventArgs e)
        {
            if (modelListView.SelectedItems.Count == 0) return;

            var model = (FreeModelInfo)modelListView.SelectedItems[0].Tag;
            StartDownload(model);
        }

        private void SelectButton_Click(object sender, EventArgs e)
        {
            if (modelListView.SelectedItems.Count == 0) return;

            var model = (FreeModelInfo)modelListView.SelectedItems[0].Tag;
            SelectModel(model);
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            RefreshModelList();
            statusLabel.Text = "List refreshed at " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void StartDownload(FreeModelInfo model)
        {
            if (_isDownloading) return;

            _isDownloading = true;
            downloadButton.Enabled = false;
            selectButton.Enabled = false;
            refreshButton.Enabled = false;
            downloadProgressBar.Visible = true;
            downloadProgressBar.Value = 0;

            _downloadThread = new Thread(() => DownloadModelWorker(model));
            _downloadThread.IsBackground = true;
            _downloadThread.Start();
        }

        private void DownloadModelWorker(FreeModelInfo model)
        {
            try
            {
                // Ensure TLS 1.2 is enabled for this thread
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                SetStatus("Preparing download...");

                string modelsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".gide", "models");

                if (!Directory.Exists(modelsDir))
                    Directory.CreateDirectory(modelsDir);

                string modelPath = Path.Combine(modelsDir, model.Id + ".gguf");
                string tempPath = modelPath + ".tmp";

                // Clean up temp file if exists
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                SetStatus("Downloading " + model.Name + "...");

                using (var client = new WebClient())
                {
                    long lastBytes = 0;
                    DateTime lastUpdate = DateTime.Now;

                    client.DownloadProgressChanged += (s, e) =>
                    {
                        int percent = (int)((double)e.BytesReceived / e.TotalBytesToReceive * 100);
                        SetProgress(percent);

                        // Calculate speed every 2 seconds
                        if ((DateTime.Now - lastUpdate).TotalSeconds >= 2)
                        {
                            long bytesPerSecond = (e.BytesReceived - lastBytes) / 2;
                            double mbps = (double)bytesPerSecond / (1024 * 1024);
                            SetStatus(string.Format("Downloading... {0}% | {1:F1} MB/s", percent, mbps));
                            lastBytes = e.BytesReceived;
                            lastUpdate = DateTime.Now;
                        }
                    };

                    client.DownloadFileAsync(new Uri(model.DownloadUrl), tempPath);

                    while (client.IsBusy)
                    {
                        Thread.Sleep(100);
                    }
                }

                // Verify download
                FileInfo fi = new FileInfo(tempPath);
                if (fi.Length < 100 * 1024) // Less than 100KB is error
                {
                    string content = File.ReadAllText(tempPath);
                    File.Delete(tempPath);
                    SetStatus("Download failed: " + content.Substring(0, Math.Min(100, content.Length)));
                    return;
                }

                // Move to final location
                if (File.Exists(modelPath))
                    File.Delete(modelPath);
                File.Move(tempPath, modelPath);

                SetStatus("Download complete! " + model.Name + " is ready to use.");
                model.IsDownloaded = true;

                // Refresh UI on main thread
                this.Invoke(new Action(() =>
                {
                    RefreshModelList();
                    downloadProgressBar.Visible = false;
                }));
            }
            catch (Exception ex)
            {
                SetStatus("Download failed: " + ex.Message);
            }
            finally
            {
                _isDownloading = false;
                this.Invoke(new Action(() =>
                {
                    downloadButton.Enabled = true;
                    refreshButton.Enabled = true;
                    downloadProgressBar.Visible = false;
                    UpdateButtonStates();
                }));
            }
        }

        private void SelectModel(FreeModelInfo model)
        {
            // Save current model preference to config
            string configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".gide", "config.json");

            try
            {
                var config = new System.Collections.Generic.Dictionary<string, object>();
                config.Add("selected_model", model.Id);
                config.Add("model_name", model.Name);

                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                string json = serializer.Serialize(config);

                string dir = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(configPath, json);

                statusLabel.Text = "Selected: " + model.Name + " - Restart GIDE to use this model.";
                statusLabel.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save model selection: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetStatus(string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(SetStatus), text);
                return;
            }
            statusLabel.Text = text;
            statusLabel.ForeColor = Color.Black;
        }

        private void SetProgress(int percent)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<int>(SetProgress), percent);
                return;
            }
            downloadProgressBar.Value = Math.Min(100, Math.Max(0, percent));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isDownloading)
            {
                var result = MessageBox.Show(
                    "Download in progress. Close anyway?",
                    "Confirm",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            base.OnFormClosing(e);
        }
    }
}
