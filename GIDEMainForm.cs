using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace GIDE
{
    /// <summary>
    /// GIDE Main Form - Professional IDE interface for AI coding
    /// Dark theme, chat interface, model selector, project browser
    /// </summary>
    public class GIDEMainForm : Form
    {
        // UI Components
        private SplitContainer mainSplit;
        private SplitContainer leftSplit;
        private Panel chatPanel;
        private Panel inputPanel;
        private Panel projectPanel;
        private Panel statusPanel;
        private Panel progressFillPanel;
        private Panel centerArea;
        private Label percentLabel;
        private RichTextBox chatHistory;
        private TextBox inputBox;
        private Button sendButton;
        private ComboBox modelSelector;
        private Button projectButton;
        private Button newChatButton;
        private TreeView projectTree;
        private Label statusLabel;
        private Label projectPathLabel;

        // Core components
        private GIDEClient _client;
        private string _currentProjectPath = "";
        private bool _isProcessing = false;
        private Thread _downloadThread;
        private bool _isDownloading = false;

        // Available models
        private List<ModelInfo> _availableModels;

        public class ModelInfo
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public string Size { get; set; }
            public long SizeBytes { get; set; }
            public int MinRamGB { get; set; }
            public int MinVramGB { get; set; }
            public string DownloadUrl { get; set; }
            public bool IsDownloaded { get; set; }
            public bool IsRecommended { get; set; }
        }

        public GIDEMainForm()
        {
            InitializeModels();
            InitializeComponent();
            ApplyTheme();
            _client = new GIDEClient();
            UpdateModelSelector();
            UpdateStatus("Ready");
        }

        private void InitializeComponent()
        {
            this.Text = "GIDE";
            this.Size = new Size(1200, 800);
            this.MinimumSize = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.Black;

            // Set icon from GIDE.ico file
            try
            {
                string appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string iconPath = Path.Combine(appDir, "GIDE.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new System.Drawing.Icon(iconPath);
                }
            }
            catch { }

            // Main container - everything centered
            Panel mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Padding = new Padding(0)
            };

            // === TOP BAR (Menu + Model Selector) ===
            Panel topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.Black,
                Padding = new Padding(20, 10, 20, 10)
            };

            // Model selector on the right
            modelSelector = new ComboBox
            {
                Width = 220,
                Height = 30,
                Left = topBar.Width - 340, // Make room for download button
                Top = 10,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            modelSelector.SelectedIndexChanged += ModelSelector_SelectedIndexChanged;
            topBar.Controls.Add(modelSelector);

            // Download button next to model selector
            Button downloadBtn = new Button
            {
                Text = "Download",
                Width = 80,
                Height = 30,
                Left = topBar.Width - 100,
                Top = 10,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Cursor = Cursors.Hand
            };
            downloadBtn.FlatAppearance.BorderSize = 0;
            downloadBtn.Click += DownloadBtn_Click;
            topBar.Controls.Add(downloadBtn);

            // Project button on the left - toggles sidebar
            Button toggleProjectBtn = new Button
            {
                Text = "Folder",
                Width = 80,
                Height = 30,
                Left = 20,
                Top = 10,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Cursor = Cursors.Hand
            };
            toggleProjectBtn.FlatAppearance.BorderSize = 0;
            toggleProjectBtn.Click += (s, e) => {
                projectPanel.Visible = !projectPanel.Visible;
                toggleProjectBtn.Text = projectPanel.Visible ? "Close" : "Folder";
                // Force layout refresh
                this.PerformLayout();
                mainContainer.Invalidate();
            };
            topBar.Controls.Add(toggleProjectBtn);

            // New Chat button
            newChatButton = new Button
            {
                Text = "New Chat",
                Width = 90,
                Height = 30,
                Left = 110,
                Top = 10,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Cursor = Cursors.Hand
            };
            newChatButton.FlatAppearance.BorderSize = 0;
            newChatButton.Click += NewChatButton_Click;
            topBar.Controls.Add(newChatButton);

            // === CENTER CONTENT AREA ===
            centerArea = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };

            // Welcome label
            Label welcomeLabel = new Label
            {
                Text = "How can I help you today?",
                Font = new Font("Segoe UI", 28, FontStyle.Regular),
                ForeColor = Color.White,
                AutoSize = true,
                Top = 150,
                Left = (centerArea.Width - 400) / 2
            };
            welcomeLabel.Anchor = AnchorStyles.Top;
            centerArea.Resize += (s, e) => {
                welcomeLabel.Left = (centerArea.Width - welcomeLabel.Width) / 2;
            };

            // === BIG CENTERED INPUT BOX ===
            Panel inputWrapper = new Panel
            {
                Width = 800,
                Height = 130,
                Top = 250,
                BackColor = Color.FromArgb(40, 40, 40),
                BorderStyle = BorderStyle.None
            };
            inputWrapper.Left = (centerArea.Width - inputWrapper.Width) / 2;
            inputWrapper.Anchor = AnchorStyles.Top;
            centerArea.Resize += (s, e) => {
                inputWrapper.Left = (centerArea.Width - inputWrapper.Width) / 2;
            };

            // Rounded corners effect using padding
            inputWrapper.Padding = new Padding(2);

            // Inner container
            Panel inputInner = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 50, 50),
                Padding = new Padding(15)
            };

            // Input textbox
            inputBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Segoe UI", 14),
                BorderStyle = BorderStyle.None,
                Text = "Type a message...",
                AcceptsReturn = true
            };
            inputBox.GotFocus += (s, e) => {
                if (inputBox.Text == "Type a message...")
                {
                    inputBox.Text = "";
                    inputBox.ForeColor = Color.White;
                }
            };
            inputBox.LostFocus += (s, e) => {
                if (string.IsNullOrWhiteSpace(inputBox.Text))
                {
                    inputBox.Text = "Type a message...";
                    inputBox.ForeColor = Color.FromArgb(180, 180, 180);
                }
            };
            inputBox.KeyDown += InputBox_KeyDown;

            // Bottom row with icons and send button
            Panel bottomRow = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.FromArgb(50, 50, 50),
                Padding = new Padding(0, 5, 0, 0)
            };

            // Send button (right side)
            sendButton = new Button
            {
                Text = "Send",
                Width = 80,
                Height = 32,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0)
            };
            sendButton.FlatAppearance.BorderSize = 0;
            sendButton.Click += SendButton_Click;

            bottomRow.Controls.Add(sendButton);

            inputInner.Controls.Add(inputBox);
            inputInner.Controls.Add(bottomRow);
            inputWrapper.Controls.Add(inputInner);

            // === ACTION BUTTONS ===
            FlowLayoutPanel actionPanel = new FlowLayoutPanel
            {
                Top = 400,
                Height = 40,
                BackColor = Color.Black,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            actionPanel.Left = (centerArea.Width - actionPanel.Width) / 2;
            centerArea.Resize += (s, e) => {
                actionPanel.Left = (centerArea.Width - actionPanel.Width) / 2;
            };

            // Action buttons
            string[] actions = { "Help me write", "Learn about", "Analyze code", "Summarize", "See more" };
            foreach (var action in actions)
            {
                Button btn = new Button
                {
                    Text = action,
                    Width = 110,
                    Height = 32,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(30, 30, 30),
                    ForeColor = Color.FromArgb(180, 180, 180),
                    Font = new Font("Segoe UI", 9),
                    Cursor = Cursors.Hand,
                    Margin = new Padding(5)
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);
                btn.FlatAppearance.BorderSize = 1;
                btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(50, 50, 50);
                btn.MouseLeave += (s, e) => btn.BackColor = Color.FromArgb(30, 30, 30);
                btn.Click += (s, e) => {
                    if (action == "Help me write")
                        inputBox.Text = "Help me write ";
                    else if (action == "Learn about")
                        inputBox.Text = "I want to learn about ";
                    else if (action == "Analyze code")
                        inputBox.Text = "Please analyze this code:\n\n";
                    else if (action == "Summarize")
                        inputBox.Text = "Please summarize ";
                    inputBox.ForeColor = Color.White;
                    inputBox.Focus();
                };
                actionPanel.Controls.Add(btn);
            }

            // === CHAT HISTORY (Centered, shown above input after first message) ===
            chatHistory = new RichTextBox
            {
                Width = 800,
                Height = 200,
                Top = 60,
                BackColor = Color.Black,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 12),
                ReadOnly = true,
                Visible = false
            };
            chatHistory.Left = (centerArea.Width - chatHistory.Width) / 2;
            chatHistory.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            centerArea.Resize += (s, e) => {
                chatHistory.Left = (centerArea.Width - chatHistory.Width) / 2;
                // Resize chatHistory to fill space above inputWrapper
                int availableHeight = inputWrapper.Top - chatHistory.Top - 20;
                if (availableHeight > 50)
                    chatHistory.Height = availableHeight;
            };

            centerArea.Controls.Add(chatHistory);
            centerArea.Controls.Add(actionPanel);
            centerArea.Controls.Add(inputWrapper);
            centerArea.Controls.Add(welcomeLabel);

            // Tag welcome label for later access
            welcomeLabel.Tag = "welcome";

            // === STATUS BAR with fill-up progress ===
            statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = Color.FromArgb(20, 20, 20)
            };

            // Progress fill panel (fills from left to right)
            progressFillPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 0,
                Height = 28,
                BackColor = Color.FromArgb(0, 120, 212) // Blue fill
            };

            // Status text with percentage
            statusLabel = new Label
            {
                Text = "Ready",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Padding = new Padding(10, 5, 10, 0),
                BackColor = Color.Transparent
            };

            // Percentage label on the right
            percentLabel = new Label
            {
                Text = "",
                Dock = DockStyle.Right,
                Width = 60,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Padding = new Padding(0, 5, 10, 0),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                Visible = false
            };

            // Add controls in correct order for proper z-index
            statusPanel.Controls.Add(progressFillPanel);  // Background fill first
            statusPanel.Controls.Add(statusLabel);        // Text on top
            statusPanel.Controls.Add(percentLabel);       // Percentage on top

            // === PROJECT SIDEBAR (Hidden by default) ===
            projectPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 250,
                BackColor = Color.FromArgb(25, 25, 25),
                Visible = false
            };

            // Open Folder button at top of sidebar
            Button openFolderBtn = new Button
            {
                Dock = DockStyle.Top,
                Height = 40,
                Text = "Open Folder",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand,
                Margin = new Padding(10)
            };
            openFolderBtn.FlatAppearance.BorderSize = 0;
            openFolderBtn.Click += ProjectButton_Click;

            projectPathLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Text = "No folder selected",
                ForeColor = Color.Gray,
                BackColor = Color.FromArgb(25, 25, 25),
                Padding = new Padding(10)
            };

            projectTree = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                ShowLines = false,
                ShowPlusMinus = true,
                ImageList = CreateFileIcons()
            };
            projectTree.BeforeExpand += ProjectTree_BeforeExpand;

            projectPanel.Controls.Add(projectTree);
            projectPanel.Controls.Add(projectPathLabel);
            projectPanel.Controls.Add(openFolderBtn);

            // Assemble - control order matters for docking
            // Add in reverse order of docking priority
            mainContainer.Controls.Add(centerArea);
            mainContainer.Controls.Add(chatHistory);
            mainContainer.Controls.Add(topBar);

            // Add controls to form in proper dock order (Fill first, then Left, then Bottom)
            this.Controls.Add(mainContainer);  // Dock Fill - fills remaining space
            this.Controls.Add(projectPanel); // Dock Left - takes left side
            this.Controls.Add(statusPanel);  // Dock Bottom - takes bottom

            // Menu
            CreateMenu();
        }

        private void CreateMenu()
        {
            MenuStrip menuStrip = new MenuStrip();
            menuStrip.BackColor = Color.FromArgb(37, 37, 38);
            menuStrip.ForeColor = Color.LightGray;

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add("Open Folder...", null, ProjectButton_Click);
            fileMenu.DropDownItems.Add("New Chat", null, NewChatButton_Click);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Exit", null, (s, e) => this.Close());

            ToolStripMenuItem toolsMenu = new ToolStripMenuItem("Tools");
            toolsMenu.DropDownItems.Add("Model Manager", null, (s, e) => OpenModelManager());
            toolsMenu.DropDownItems.Add("Clear History", null, NewChatButton_Click);

            ToolStripMenuItem helpMenu = new ToolStripMenuItem("Help");
            helpMenu.DropDownItems.Add("About GIDE", null, (s, e) =>
            {
                MessageBox.Show("GIDE v0.4.0 - Free Local AI Coding Assistant\n\n" +
                    "100% Free • No API Keys • Local Processing\n\n" +
                    "Uses Qwen3 models via llama.cpp",
                    "About GIDE", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(toolsMenu);
            menuStrip.Items.Add(helpMenu);

            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);
        }

        private void ApplyTheme()
        {
            this.BackColor = Color.FromArgb(25, 25, 25);
        }

        private ImageList CreateFileIcons()
        {
            ImageList icons = new ImageList();
            icons.ColorDepth = ColorDepth.Depth32Bit;
            icons.ImageSize = new Size(16, 16);
            // Use system icons
            icons.Images.Add(SystemIcons.WinLogo); // Folder
            icons.Images.Add(SystemIcons.Application); // File
            return icons;
        }

        private void InitializeModels()
        {
            var hardware = HardwareDetector.GetHardwareInfo();
            long ramGB = (long)(hardware.TotalRAM / (1024 * 1024 * 1024));
            long vramGB = hardware.BestGPU != null ? (long)(hardware.BestGPU.DedicatedVRAM / (1024 * 1024 * 1024)) : 0;

            string recommended = HardwareDetector.RecommendModel(hardware).ModelId;

            _availableModels = new List<ModelInfo>
            {
                new ModelInfo
                {
                    Id = "qwen3-30b-awq",
                    DisplayName = "Qwen3 30B AWQ (Best Quality)",
                    Description = "Best quality coding. Requires 32GB RAM or 12GB VRAM.",
                    Size = "~18 GB",
                    SizeBytes = 18L * 1024 * 1024 * 1024,
                    MinRamGB = 32,
                    MinVramGB = 12,
                    DownloadUrl = "https://huggingface.co/Qwen/Qwen3-30B-AWQ-GGUF/resolve/main/qwen3-30b-awq-q4_0.gguf",
                    IsRecommended = recommended == "qwen3-30b-awq"
                },
                new ModelInfo
                {
                    Id = "qwen3-14b",
                    DisplayName = "Qwen3 14B (Balanced)",
                    Description = "Good balance of quality and speed. 16GB RAM or 8GB VRAM.",
                    Size = "~9 GB",
                    SizeBytes = 9L * 1024 * 1024 * 1024,
                    MinRamGB = 16,
                    MinVramGB = 8,
                    DownloadUrl = "https://huggingface.co/Qwen/Qwen3-14B-GGUF/resolve/main/qwen3-14b-q4_k_m.gguf",
                    IsRecommended = recommended == "qwen3-14b"
                },
                new ModelInfo
                {
                    Id = "qwen3-8b",
                    DisplayName = "Qwen3 8B (Fast)",
                    Description = "Fast responses. Good for most coding. 8GB RAM.",
                    Size = "~5 GB",
                    SizeBytes = 5L * 1024 * 1024 * 1024,
                    MinRamGB = 8,
                    MinVramGB = 0,
                    DownloadUrl = "https://huggingface.co/Qwen/Qwen3-8B-GGUF/resolve/main/qwen3-8b-q4_k_m.gguf",
                    IsRecommended = recommended == "qwen3-8b"
                },
                new ModelInfo
                {
                    Id = "qwen3-4b",
                    DisplayName = "Qwen3 4B (Lightweight)",
                    Description = "Lightweight and fast. Works on any system. 4GB RAM.",
                    Size = "~2.5 GB",
                    SizeBytes = 2L * 1024 * 1024 * 1024 + 512L * 1024 * 1024,
                    MinRamGB = 4,
                    MinVramGB = 0,
                    DownloadUrl = "https://huggingface.co/Qwen/Qwen3-4B-GGUF/resolve/main/qwen3-4b-q4_k_m.gguf",
                    IsRecommended = recommended == "qwen3-4b"
                }
            };

            // Check which are downloaded
            foreach (var model in _availableModels)
            {
                string modelPath = ModelManager.GetModelPath(model.Id);
                model.IsDownloaded = File.Exists(modelPath);
            }
        }

        private void UpdateModelSelector()
        {
            modelSelector.Items.Clear();

            foreach (var model in _availableModels)
            {
                string status = model.IsDownloaded ? "[Ready] " : "";
                string rec = model.IsRecommended ? "[RECOMMENDED] " : "";
                modelSelector.Items.Add(rec + status + model.DisplayName);
            }

            // Select the recommended model or first available
            int recommendedIndex = _availableModels.FindIndex(m => m.IsRecommended && m.IsDownloaded);
            if (recommendedIndex < 0)
                recommendedIndex = _availableModels.FindIndex(m => m.IsRecommended);
            if (recommendedIndex < 0)
                recommendedIndex = 0;

            modelSelector.SelectedIndex = recommendedIndex;
        }

        private void DownloadBtn_Click(object sender, EventArgs e)
        {
            if (modelSelector.SelectedIndex < 0) return;

            var model = _availableModels[modelSelector.SelectedIndex];
            
            // Check if model actually exists on disk
            string modelPath = ModelManager.GetModelPath(model.Id);
            bool actuallyExists = File.Exists(modelPath);
            
            // Update the model's downloaded status to match reality
            model.IsDownloaded = actuallyExists;
            
            // Refresh the model selector display
            UpdateModelSelector();
            
            if (actuallyExists)
            {
                MessageBox.Show("Model is already downloaded.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            // Update the model's downloaded status
            model.IsDownloaded = false;

            var result = MessageBox.Show(
                string.Format("Download '{0}' ({1})?", model.DisplayName, model.Size),
                "Download Model",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                DownloadModel(model);
            }
        }

        private void ModelSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (modelSelector.SelectedIndex < 0) return;

            var model = _availableModels[modelSelector.SelectedIndex];
            
            // Check if model actually exists on disk
            string modelPath = ModelManager.GetModelPath(model.Id);
            bool actuallyExists = File.Exists(modelPath);
            
            // Update the model's downloaded status
            model.IsDownloaded = actuallyExists;
            
            if (!actuallyExists)
            {
                var result = MessageBox.Show(
                    string.Format("'{0}' is not downloaded yet ({1}).\n\nDownload now?", model.DisplayName, model.Size),
                    "Download Model",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    DownloadModel(model);
                }
                else
                {
                    // Revert selection to a downloaded model
                    int downloadedIndex = _availableModels.FindIndex(m => m.IsDownloaded);
                    if (downloadedIndex >= 0 && downloadedIndex != modelSelector.SelectedIndex)
                    {
                        modelSelector.SelectedIndex = downloadedIndex;
                    }
                }
            }
            else
            {
                // Switch to this model
                _client.SwitchModel(model.Id);
                UpdateStatus("Using: " + model.DisplayName);
            }
        }

        private void DownloadModel(ModelInfo model)
        {
            if (_isDownloading) return;

            _isDownloading = true;
            UpdateStatus("Downloading " + model.DisplayName + "...");
            percentLabel.Visible = true;
            modelSelector.Enabled = false;
            sendButton.Enabled = false;

            _downloadThread = new Thread(() =>
            {
                try
                {
                    // Ensure TLS 1.2 is enabled for this thread
                    System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                    string modelsDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".gide", "models");

                    if (!Directory.Exists(modelsDir))
                        Directory.CreateDirectory(modelsDir);

                    string modelPath = Path.Combine(modelsDir, model.Id + ".gguf");
                    string tempPath = modelPath + ".tmp";

                    if (File.Exists(tempPath))
                        File.Delete(tempPath);

                    using (var client = new WebClient())
                    {
                        long lastBytes = 0;
                        DateTime lastUpdate = DateTime.Now;

                        client.DownloadProgressChanged += (s, e) =>
                        {
                            int percent = (int)((double)e.BytesReceived / e.TotalBytesToReceive * 100);

                            this.Invoke(new Action(() =>
                            {
                                // Update fill panel width based on percentage
                                int fillWidth = (int)((this.Width - 60) * percent / 100.0);
                                progressFillPanel.Width = fillWidth;
                                percentLabel.Text = percent + "%";
                            }));

                            if ((DateTime.Now - lastUpdate).TotalSeconds >= 2)
                            {
                                long bytesPerSecond = (e.BytesReceived - lastBytes) / 2;
                                double mbps = (double)bytesPerSecond / (1024 * 1024);
                                this.Invoke(new Action(() =>
                                {
                                    UpdateStatus(string.Format("Downloading... {0}% | {1:F1} MB/s", percent, mbps));
                                }));
                                lastBytes = e.BytesReceived;
                                lastUpdate = DateTime.Now;
                            }
                        };

                        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
                        client.DownloadFile(model.DownloadUrl, tempPath);
                    }

                    // Verify and move
                    FileInfo fi = new FileInfo(tempPath);
                    if (fi.Length < 100 * 1024)
                    {
                        File.Delete(tempPath);
                        throw new Exception("Download failed - file too small");
                    }

                    if (File.Exists(modelPath))
                        File.Delete(modelPath);
                    File.Move(tempPath, modelPath);

                    model.IsDownloaded = true;

                    this.Invoke(new Action(() =>
                    {
                        UpdateModelSelector();
                        modelSelector.SelectedIndex = _availableModels.IndexOf(model);
                        UpdateStatus(model.DisplayName + " ready!");
                        _client.SwitchModel(model.Id);
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        UpdateStatus("Download failed: " + ex.Message);
                        MessageBox.Show("Failed to download model:\n" + ex.Message, "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
                finally
                {
                    _isDownloading = false;
                    this.Invoke(new Action(() =>
                    {
                        progressFillPanel.Width = 0;
                        percentLabel.Visible = false;
                        percentLabel.Text = "";
                        modelSelector.Enabled = true;
                        sendButton.Enabled = true;
                    }));
                }
            });

            _downloadThread.IsBackground = true;
            _downloadThread.Start();
        }

        private void ProjectButton_Click(object sender, EventArgs e)
        {
            UpdateStatus("Opening folder dialog...");

            // Run dialog on separate thread to prevent UI freeze
            Thread dialogThread = new Thread(() =>
            {
                try
                {
                    using (var dialog = new OpenFileDialog())
                    {
                        dialog.Title = "Select Project Folder";
                        dialog.CheckFileExists = false;
                        dialog.CheckPathExists = true;
                        dialog.FileName = "[Select Folder]";
                        dialog.Filter = "Folders|*.";
                        dialog.ValidateNames = false;

                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            string folderPath = Path.GetDirectoryName(dialog.FileName);
                            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                            {
                                this.Invoke(new Action(() =>
                                {
                                    UpdateStatus("Loading project: " + folderPath);
                                    LoadProject(folderPath);
                                }));
                            }
                        }
                        else
                        {
                            this.Invoke(new Action(() =>
                            {
                                UpdateStatus("Folder selection cancelled");
                            }));
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show("Error opening folder:\n" + ex.Message, "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        UpdateStatus("Error: " + ex.Message);
                    }));
                }
            });

            dialogThread.SetApartmentState(ApartmentState.STA);
            dialogThread.Start();
        }

        private void LoadProject(string path)
        {
            try
            {
                _currentProjectPath = path;
                projectPathLabel.Text = path;

                if (projectTree.InvokeRequired)
                {
                    projectTree.Invoke(new Action(() => LoadProjectTree(path)));
                }
                else
                {
                    LoadProjectTree(path);
                }

                // Update GIDE's working directory
                try
                {
                    Environment.CurrentDirectory = path;
                }
                catch { }

                UpdateStatus("Project loaded: " + Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading project:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadProjectTree(string path)
        {
            projectTree.Nodes.Clear();
            var rootNode = new TreeNode(Path.GetFileName(path), 0, 0);
            rootNode.Tag = path;
            rootNode.Nodes.Add("Loading...");
            projectTree.Nodes.Add(rootNode);
            rootNode.Expand();
        }

        private void ProjectTree_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            TreeNode node = e.Node;
            string path = node.Tag as string;

            if (path == null) return;

            // Clear dummy node
            if (node.Nodes.Count == 1 && node.Nodes[0].Text == "Loading...")
            {
                node.Nodes.Clear();

                try
                {
                    string[] dirs = Directory.GetDirectories(path);
                    string[] files = Directory.GetFiles(path);

                    foreach (string dir in dirs.OrderBy(d => d))
                    {
                        string name = Path.GetFileName(dir);
                        if (name.StartsWith(".")) continue; // Skip hidden

                        TreeNode dirNode = new TreeNode(name, 0, 0);
                        dirNode.Tag = dir;
                        dirNode.Nodes.Add("Loading...");
                        node.Nodes.Add(dirNode);
                    }

                    foreach (string file in files.OrderBy(f => f))
                    {
                        string name = Path.GetFileName(file);
                        if (name.StartsWith(".")) continue;

                        TreeNode fileNode = new TreeNode(name, 1, 1);
                        fileNode.Tag = file;
                        node.Nodes.Add(fileNode);
                    }
                }
                catch (Exception ex)
                {
                    node.Nodes.Add("Error: " + ex.Message);
                }
            }
        }

        private void ProjectTree_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            string path = e.Node.Tag as string;
            if (path == null) return;

            if (File.Exists(path))
            {
                // For now, just show file info in chat
                try
                {
                    string content = File.ReadAllText(path);
                    if (content.Length > 2000)
                        content = content.Substring(0, 2000) + "\n... [truncated]";

                    inputBox.Text = "Please review this file:\n\n```\n" + content + "\n```";
                    inputBox.Focus();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not read file: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift && !e.Control)
            {
                e.SuppressKeyPress = true;
                SendMessage();
            }
            else if (e.KeyCode == Keys.Enter && e.Shift)
            {
                // Allow Shift+Enter for new line
                e.SuppressKeyPress = false;
            }
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            SendMessage();
        }

        private void SendMessage()
        {
            string message = inputBox.Text.Trim();
            if (string.IsNullOrEmpty(message) || _isProcessing) return;

            if (_client == null)
            {
                AddMessage("Error", "Client not initialized", Color.Red);
                return;
            }

            inputBox.Clear();
            AddMessage("You", message, Color.FromArgb(0, 122, 204));

            _isProcessing = true;
            sendButton.Enabled = false;
            // Don't show progress bar for generation - only for downloads
            UpdateStatus("Processing...");

            bool hadError = false;
            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    if (_client == null)
                    {
                        this.Invoke(new Action(() =>
                        {
                            AddMessage("Error", "Client not initialized", Color.Red);
                            UpdateStatus("Error: Client not initialized");
                        }));
                        return;
                    }

                    this.Invoke(new Action(() =>
                    {
                        UpdateStatus("Initializing...");
                    }));

                    // Get the currently selected model
                    var selectedModel = _availableModels[modelSelector.SelectedIndex];
                    var modelInfo = ModelManager.GetModelInfo(selectedModel.Id);
                    _client.SetModel(modelInfo);
                    
                    bool initResult = _client.InitializeLocalEngine();

                    if (!initResult)
                    {
                        hadError = true;
                        string errorMsg = _client.GetLastError();
                        if (string.IsNullOrEmpty(errorMsg))
                            errorMsg = "Model not downloaded or engine failed";
                        
                        this.Invoke(new Action(() =>
                        {
                            ShowChatInline();
                            AddMessage("Error", "Model engine failed:\n" + errorMsg + "\n\nModel: " + selectedModel.Id + "\nPath: " + ModelManager.GetModelPath(selectedModel.Id), Color.Red);
                            UpdateStatus("Error - see chat for details");
                        }));
                        return;
                    }

                    this.Invoke(new Action(() =>
                    {
                        UpdateStatus("Generating...");
                    }));

                    var messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = "user", Content = message }
                    };

                    string systemPrompt = GetSystemPrompt();

                    this.Invoke(new Action(() =>
                    {
                        UpdateStatus("Calling model...");
                    }));

                    string response = _client.Generate(messages, systemPrompt);

                    this.Invoke(new Action(() =>
                    {
                        ShowChatInline();

                        if (string.IsNullOrEmpty(response))
                        {
                            hadError = true;
                            AddMessage("Error", "Model returned empty response. Check if model file exists and server is running.", Color.Red);
                            UpdateStatus("Empty response - model not working");
                        }
                        else
                        {
                            AddMessage("GIDE", response, Color.FromArgb(100, 200, 120));
                            UpdateStatus("Ready");
                        }
                    }));
                }
                catch (Exception ex)
                {
                    hadError = true;
                    this.Invoke(new Action(() =>
                    {
                        AddMessage("Error", "Exception: " + ex.Message + "\n\nStack: " + ex.StackTrace, Color.Red);
                        UpdateStatus("Error: " + ex.Message);
                    }));
                }
                finally
                {
                    _isProcessing = false;
                    this.Invoke(new Action(() =>
                    {
                        sendButton.Enabled = true;
                        // Only set Ready if there was no error
                        if (!hadError)
                        {
                            UpdateStatus("Ready");
                        }
                    }));
                }
            });
        }

        private string GetSystemPrompt()
        {
            string fileTree = "";
            if (!string.IsNullOrEmpty(_currentProjectPath) && Directory.Exists(_currentProjectPath))
            {
                fileTree = GetProjectFileTree();
            }

            return "You are GIDE, a helpful AI coding assistant. " +
                "You help users write, review, and improve code. " +
                "You can suggest file edits, create new files, and run commands.\n\n" +
                "Current project files:\n" + fileTree + "\n\n" +
                "When suggesting changes, use the <GIDE> XML tags. " +
                "Be concise but thorough.";
        }

        private string GetProjectFileTree()
        {
            if (string.IsNullOrEmpty(_currentProjectPath)) return "(No project loaded)";

            try
            {
                var files = Directory.GetFiles(_currentProjectPath, "*.*", SearchOption.TopDirectoryOnly);
                var result = string.Join("\n", files.Select(f => "- " + Path.GetFileName(f)));
                return result;
            }
            catch { return "(Unable to read project)"; }
        }

        private void ShowChatInline()
        {
            // Hide welcome label and action panel; show chat history above input
            foreach (Control c in centerArea.Controls)
            {
                if (c is Label && (c.Tag as string) == "welcome")
                    c.Visible = false;
                else if (c is FlowLayoutPanel)
                    c.Visible = false;
            }
            chatHistory.Visible = true;
            // Trigger resize layout
            int availableHeight = 0;
            foreach (Control c in centerArea.Controls)
            {
                if (c is Panel && c.Width == 800)
                {
                    availableHeight = c.Top - chatHistory.Top - 20;
                    break;
                }
            }
            if (availableHeight > 50)
                chatHistory.Height = availableHeight;
            chatHistory.Left = (centerArea.Width - chatHistory.Width) / 2;
            chatHistory.BringToFront();
        }

        private void AddMessage(string sender, string message, Color color)
        {
            chatHistory.SelectionStart = chatHistory.TextLength;
            chatHistory.SelectionLength = 0;

            // Sender name
            chatHistory.SelectionColor = color;
            chatHistory.SelectionFont = new Font("Segoe UI", 9, FontStyle.Bold);
            chatHistory.AppendText(sender + "\n");

            // Message
            chatHistory.SelectionColor = Color.LightGray;
            chatHistory.SelectionFont = new Font("Consolas", 10);
            chatHistory.AppendText(message + "\n\n");

            chatHistory.ScrollToCaret();
        }

        private void NewChatButton_Click(object sender, EventArgs e)
        {
            chatHistory.Clear();
            _client = new GIDEClient();
            UpdateStatus("New chat started");
        }

        private void OpenModelManager()
        {
            var form = new GIDESettingsForm();
            form.ShowDialog(this);
            InitializeModels(); // Refresh
            UpdateModelSelector();
        }

        private void UpdateStatus(string text)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(UpdateStatus), text);
                return;
            }
            statusLabel.Text = text;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isProcessing || _isDownloading)
            {
                var result = MessageBox.Show(
                    "Processing in progress. Close anyway?",
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
