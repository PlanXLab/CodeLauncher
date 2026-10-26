using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using VSCodePortableCommon;

namespace VSCodePortableLauncher
{
    partial class UpgradeForm : Form
    {
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int ReleaseCapture();
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;
    private const int WS_BORDER = 0x00800000;

        // Constants
        private const int MAX_ACTIVITY_LOG_LINES = 20;

        // VSCode extensions (must match installer)
        public static readonly string[] VSCODE_EXTENSIONS = new string[]
        {
            "teabyii.ayu",
            "zhuangtongfa.material-theme",
            "ms-python.python",
            "ms-python.vscode-pylance",
            "ms-python.vscode-python-envs",
            "KevinRose.vsc-python-indent",
            "usernamehw.errorlens"
        };

        // Win32 API for extracting icon from exe
        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        private string pvsDir;
        private string pvsInfo;

        private Dictionary<string, string> versions;
        private bool upgradeCompleted;
        private bool upgradeStarted;
        private int totalComponents;
        private int completedComponents;
        private Stopwatch upgradeTimer;
        private DateTime upgradeStartTime;
        private List<string> pendingUpgrades;
        private System.Windows.Forms.Timer elapsedTimer;
        private StringBuilder logFileBuffer;
        private object logFileLock;
        private Queue<string> activityDisplayEntries;
        private string overallStatusOverride;

        public UpgradeForm(string installDir, List<string> upgrades)
        {
            pvsDir = installDir;
            pvsInfo = Path.Combine(pvsDir, CommonHelper.RuntimeInfoFileName);

            upgradeCompleted = false;
            upgradeStarted = false;
            totalComponents = 0;
            completedComponents = 0;
            versions = new Dictionary<string, string>();
            pendingUpgrades = upgrades;
            logFileBuffer = new StringBuilder();
            logFileLock = new object();
            activityDisplayEntries = new Queue<string>();
            overallStatusOverride = "Ready to upgrade";

            // Optimize network performance
            System.Net.ServicePointManager.DefaultConnectionLimit = 100;
            System.Net.ServicePointManager.Expect100Continue = false;

            InitializeComponent();

            // Set form icon (extract from launcher.exe's win32 icon resource)
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                IntPtr hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);
                if (hIcon != IntPtr.Zero)
                {
                    Icon = Icon.FromHandle(hIcon);
                }
            }
            catch { /* Ignore icon errors */ }

            headerPanel.MouseDown += HeaderPanel_MouseDown;
            headerTitleLabel.MouseDown += HeaderPanel_MouseDown;
            headerSubLabel.MouseDown += HeaderPanel_MouseDown;
            
            // Initialize table with pending upgrades
            InitializePendingUpgradesTable();
            UpdateCurrentTask("Ready to upgrade");
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= WS_BORDER;
                return cp;
            }
        }

        private void HeaderPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
            }
        }

        private void InitializePendingUpgradesTable()
        {
            var componentMap = new Dictionary<string, string>
            {
                { "upgrade_pwsh7", "PowerShell 7" },
                { "upgrade_ohmyposh", "Oh My Posh" },
                { "upgrade_term_icons", "Terminal-Icons" },
                { "upgrade_psfzf", "PSFzf" },
                { "upgrade_modern_unix", "modern-unix-win" },
                { "upgrade_vscode", "VSCode" }
            };

            var componentsToShow = pendingUpgrades
                .Where(flag => componentMap.ContainsKey(flag))
                .Select(flag => componentMap[flag])
                .ToList();

            InitializeUpgradeTable(componentsToShow);
            SetUpgradeSummary(componentsToShow);
        }

        private void SetUpgradeSummary(IList<string> components)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetUpgradeSummary(components)));
                return;
            }

            var displayNames = components
                .Select(GetDisplayComponentName)
                .ToList();

            summaryTitleLabel.Text = displayNames.Count > 0
                ? "Pending updates (" + displayNames.Count + ")"
                : "No pending updates";

            if (displayNames.Count == 0)
            {
                summaryBodyLabel.Text = "Your portable environment is already up to date.";
                return;
            }

            summaryBodyLabel.Text = "Updates will be applied to " + string.Join(", ", displayNames) + ".\r\nYour portable environment, settings, and extensions stay in place.";
        }

        private void UpgradeButton_Click(object sender, EventArgs e)
        {
            if (upgradeCompleted)
            {
                Close();
            }
            else
            {
                if (upgradeStarted)
                    return;

                upgradeStarted = true;
                upgradeButton.Enabled = false;
                upgradeButton.Text = "Upgrading...";
                upgradeButton.BackColor = Color.FromArgb(200, 200, 200);
                upgradeButton.ForeColor = Color.FromArgb(100, 100, 100);
                upgradeButton.FlatStyle = FlatStyle.Flat;
                upgradeButton.FlatAppearance.BorderSize = 0;
                upgradeButton.Cursor = Cursors.WaitCursor;
                cancelButton.Enabled = false;
                UpdateCurrentTask("Now: Preparing updates");

                Task.Run(async () =>
                {
                    await PerformUpgrade(pendingUpgrades);
                });
            }
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            if (!upgradeStarted)
            {
                // Just close form, VSCode will be launched by RunLauncher
                Close();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (upgradeStarted && !upgradeCompleted && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                MessageBox.Show("Please wait until the upgrade process is complete.",
                    "Upgrade in Progress",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            base.OnFormClosing(e);
        }

        private async Task PerformUpgrade(List<string> pendingUpgrades)
        {
            try
            {
                CommonHelper.EnableTls12();

                if (!File.Exists(pvsInfo))
                {
                    ShowError("codelauncher.info file not found.");
                    return;
                }

                // Terminate Code.exe if pwsh7 or vscode upgrade is needed
                bool hasPwsh7Upgrade = pendingUpgrades.Contains("upgrade_pwsh7");
                bool hasModuleUpgrade = pendingUpgrades.Any(f => f == "upgrade_ohmyposh" || f == "upgrade_term_icons" || f == "upgrade_psfzf" || f == "upgrade_modern_unix");
                bool hasVSCodeUpgrade = pendingUpgrades.Contains("upgrade_vscode");

                if (hasPwsh7Upgrade || hasVSCodeUpgrade)
                {
                    LogActivity("Checking for running Code.exe processes...");
                    string codeExePath = Path.Combine(pvsDir, "Code.exe");

                    var codeProcesses = Process.GetProcesses()
                        .Where(p =>
                        {
                            try
                            {
                                if (p.MainModule != null && p.MainModule.FileName != null)
                                    return p.MainModule.FileName.Equals(codeExePath, StringComparison.OrdinalIgnoreCase);
                                return false;
                            }
                            catch { return false; }
                        })
                        .ToList();

                    if (codeProcesses.Any())
                    {
                        LogActivity("Found " + codeProcesses.Count + " Code.exe process(es). Terminating...");
                        foreach (var proc in codeProcesses)
                        {
                            try
                            {
                                proc.Kill();
                                proc.WaitForExit(5000);
                            }
                            catch { }
                        }
                        await Task.Delay(1000); // Wait for cleanup
                        LogActivity("Code.exe processes terminated.");
                    }
                }

                versions = ReadVersionsFromInfo();

                upgradeTimer = Stopwatch.StartNew();
                upgradeStartTime = DateTime.Now;
                LogActivity("=== Upgrade started at " + upgradeStartTime.ToString("HH:mm:ss") + " ===");

                // Start elapsed time timer (update every second)
                StartElapsedTimer();

                var allResults = new List<KeyValuePair<string, string>>();

                // Phase 1: If pwsh7 needs upgrade AND modules need upgrade, do pwsh7 first
                if (hasPwsh7Upgrade && hasModuleUpgrade)
                {
                    LogActivity("PowerShell 7: Upgrading first (required for modules)...");

                    // Run pwsh7 and vscode in parallel if both need upgrade
                    if (hasVSCodeUpgrade)
                    {
                        var phase1Tasks = new List<Task<KeyValuePair<string, string>>>
                        {
                            UpgradePowerShell7Async(),
                            UpgradeVSCodeAsync()
                        };
                        var phase1Results = await Task.WhenAll(phase1Tasks);
                        allResults.AddRange(phase1Results.Where(r => !string.IsNullOrEmpty(r.Value)));
                    }
                    else
                    {
                        var pwsh7Result = await UpgradePowerShell7Async();
                        if (!string.IsNullOrEmpty(pwsh7Result.Value))
                            allResults.Add(pwsh7Result);
                    }

                    // Wait for PowerShell to be ready (optimized polling)
                    string pwshExe = Path.Combine(pvsDir, "data", "lib", "pwsh", "pwsh.exe");
                    int retries = 0;
                    while (!File.Exists(pwshExe) && retries < 20)
                    {
                        await Task.Delay(200); // Check every 200ms
                        retries++;
                    }
                }

                // Phase 2: Run remaining upgrades in parallel
                var parallelTasks = new List<Task<KeyValuePair<string, string>>>();

                // Add pwsh7 if it needs upgrade but no modules need upgrade
                if (hasPwsh7Upgrade && !hasModuleUpgrade)
                    parallelTasks.Add(UpgradePowerShell7Async());

                if (pendingUpgrades.Contains("upgrade_ohmyposh"))
                    parallelTasks.Add(UpgradeOhMyPoshAsync());

                if (pendingUpgrades.Contains("upgrade_term_icons"))
                    parallelTasks.Add(UpgradeModuleAsync("Terminal-Icons", "TERMINAL_ICONS_VERSION"));

                if (pendingUpgrades.Contains("upgrade_psfzf"))
                    parallelTasks.Add(UpgradeModuleAsync("PSFzf", "PSFZF_VERSION"));

                if (pendingUpgrades.Contains("upgrade_modern_unix"))
                    parallelTasks.Add(UpgradeModuleAsync("modern-unix-win", "MODERN_UNIX_WIN_VERSION"));

                // Add vscode if not already upgraded in phase 1
                if (hasVSCodeUpgrade && !(hasPwsh7Upgrade && hasModuleUpgrade))
                    parallelTasks.Add(UpgradeVSCodeAsync());

                if (parallelTasks.Any())
                {
                    var parallelResults = await Task.WhenAll(parallelTasks);
                    allResults.AddRange(parallelResults.Where(r => !string.IsNullOrEmpty(r.Value)));
                }

                UpdateVersionsInInfo(allResults);
                DeleteUpgradeFlags();

                LogActivity("Upgrade completed successfully!");

                if (upgradeTimer != null)
                {
                    upgradeTimer.Stop();
                    var elapsed = upgradeTimer.Elapsed;
                    string elapsedStr = string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}",
                        elapsed.Hours, elapsed.Minutes, elapsed.Seconds, elapsed.Milliseconds);
                    LogActivity("=== Upgrade completed at " + DateTime.Now.ToString("HH:mm:ss") + " ===");
                    LogActivity("*** Total upgrade time: " + elapsedStr + " ***");
                }

                // Stop elapsed timer
                StopElapsedTimer();

                SaveUpgradeLog();

                upgradeCompleted = true;
                Invoke(new Action(() =>
                {
                    upgradeButton.Enabled = true;
                    upgradeButton.Text = "Exit";
                    upgradeButton.BackColor = Color.FromArgb(0, 120, 215);
                    upgradeButton.ForeColor = Color.White;
                    upgradeButton.FlatStyle = FlatStyle.Flat;
                    upgradeButton.FlatAppearance.BorderSize = 0;
                    upgradeButton.Cursor = Cursors.Hand;
                    UpdateCurrentTask("Done: Updates applied successfully");
                }));
            }
            catch (Exception ex)
            {
                LogActivity("Upgrade error: " + ex.Message);
                SaveUpgradeLog();
                ShowError("Upgrade error: " + ex.Message);
                
                // Stop elapsed timer
                StopElapsedTimer();
                
                upgradeCompleted = true;
                Invoke(new Action(() =>
                {
                    upgradeButton.Enabled = true;
                    upgradeButton.Text = "Exit";
                    upgradeButton.BackColor = Color.FromArgb(0, 120, 215);
                    upgradeButton.ForeColor = Color.White;
                    upgradeButton.FlatStyle = FlatStyle.Flat;
                    upgradeButton.FlatAppearance.BorderSize = 0;
                    upgradeButton.Cursor = Cursors.Hand;
                    UpdateCurrentTask("Check: Upgrade failed");
                }));
            }
        }

        private void InitializeUpgradeTable(List<string> components)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => InitializeUpgradeTable(components)));
                return;
            }

            var componentMap = new Dictionary<string, Tuple<Label, Label, ProgressBar>>
            {
                { "PowerShell 7", Tuple.Create(pwsh7NameLabel, pwsh7StatusLabel, pwsh7ProgressBar) },
                { "Oh My Posh", Tuple.Create(ohmyposhNameLabel, ohmyposhStatusLabel, ohmyposhProgressBar) },
                { "Terminal-Icons", Tuple.Create(termIconsNameLabel, termIconsStatusLabel, termIconsProgressBar) },
                { "PSFzf", Tuple.Create(psfzfNameLabel, psfzfStatusLabel, psfzfProgressBar) },
                { "modern-unix-win", Tuple.Create(modernUnixNameLabel, modernUnixStatusLabel, modernUnixProgressBar) },
                { "VSCode", Tuple.Create(vscodeNameLabel, vscodeStatusLabel, vscodeProgressBar) }
            };

            totalComponents = components.Count;
            completedComponents = 0;

            foreach (var kvp in componentMap)
            {
                bool shouldEnable = components.Contains(kvp.Key);
                kvp.Value.Item1.Enabled = shouldEnable;
                kvp.Value.Item2.Enabled = shouldEnable;
                kvp.Value.Item3.Enabled = shouldEnable;
                
                if (shouldEnable)
                {
                    kvp.Value.Item1.Text = GetDisplayComponentName(kvp.Key);
                    kvp.Value.Item2.Text = "Waiting";
                    kvp.Value.Item2.ForeColor = Color.FromArgb(160, 160, 160);
                    kvp.Value.Item3.Value = 0;
                }
                else
                {
                    kvp.Value.Item1.Text = "-";
                    kvp.Value.Item2.Text = "Not needed";
                    kvp.Value.Item2.ForeColor = Color.FromArgb(180, 180, 180);
                    kvp.Value.Item3.Value = 0;
                }
            }

            UpdateCompletedLabel();
            RefreshOverallStatusLabel();
        }

        private void LogActivity(string message, bool fileOnly = false)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string elapsed = "";
            if (upgradeTimer != null && upgradeTimer.IsRunning)
            {
                var e = upgradeTimer.Elapsed;
                elapsed = string.Format(" +{0:D2}:{1:D2}", (int)e.TotalMinutes, e.Seconds);
            }
            string logEntry = "[" + timestamp + elapsed + "] " + message + "\r\n";

            lock (logFileLock)
            {
                logFileBuffer.Append(logEntry);
            }

            if (fileOnly)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action(() => LogActivity(message, false)));
                return;
            }

            string uiMessage = SimplifyActivityMessage(message);
            if (string.IsNullOrEmpty(uiMessage))
                return;

            AppendActivityLogEntry("[" + timestamp + "] " + uiMessage);
        }

        private void UpdateComponentStatus(string componentName, string status, int progress)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateComponentStatus(componentName, status, progress)));
                return;
            }

            Label statusLabel = null;
            ProgressBar progressBar = null;

            switch (componentName)
            {
                case "PowerShell 7":
                    statusLabel = pwsh7StatusLabel;
                    progressBar = pwsh7ProgressBar;
                    break;
                case "Oh My Posh":
                    statusLabel = ohmyposhStatusLabel;
                    progressBar = ohmyposhProgressBar;
                    break;
                case "Terminal-Icons":
                    statusLabel = termIconsStatusLabel;
                    progressBar = termIconsProgressBar;
                    break;
                case "PSFzf":
                    statusLabel = psfzfStatusLabel;
                    progressBar = psfzfProgressBar;
                    break;
                case "modern-unix-win":
                    statusLabel = modernUnixStatusLabel;
                    progressBar = modernUnixProgressBar;
                    break;
                case "VSCode":
                    statusLabel = vscodeStatusLabel;
                    progressBar = vscodeProgressBar;
                    break;
                default:
                    return;
            }

            if (statusLabel != null && progressBar != null)
            {
                string displayStatus = status;
                string category = "Working";

                if (status.Contains("Checking") || status.Contains("Requesting"))
                { displayStatus = "Preparing"; category = "Working"; }
                else if (status.Contains("Downloading") || status.Contains("download"))
                { displayStatus = "Downloading"; category = "Downloading"; }
                else if (status.Contains("Extracting") || status.Contains("extract") ||
                         status.Contains("Installing") || status.Contains("Configuring") ||
                         status.Contains("Creating") || status.Contains("Running") ||
                         status.Contains("Verifying"))
                { displayStatus = "Installing"; category = "Working"; }
                else if (status.StartsWith("✓") || status.Contains("Completed"))
                { displayStatus = "Completed"; category = "Completed"; }
                else if (status.StartsWith("⚠") || status.Contains("Error") || status.Contains("failed"))
                { displayStatus = "Failed"; category = "Failed"; }
                else if (status.Contains("Waiting"))
                { displayStatus = "Waiting"; category = "Waiting"; }

                statusLabel.Text = displayStatus;
                progressBar.Value = Math.Min(progress, 100);

                if (category == "Waiting")
                    statusLabel.ForeColor = Color.FromArgb(160, 160, 160);
                else if (category == "Downloading")
                    statusLabel.ForeColor = Color.FromArgb(0, 120, 212);
                else if (category == "Working")
                    statusLabel.ForeColor = Color.FromArgb(0, 120, 212);
                else if (category == "Completed")
                    statusLabel.ForeColor = Color.FromArgb(16, 124, 16);
                else
                    statusLabel.ForeColor = Color.FromArgb(196, 43, 28);

                overallStatusOverride = null;
                UpdateOverallProgress();
            }
        }

        private void StartElapsedTimer()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(StartElapsedTimer));
                return;
            }

            if (elapsedTimer != null)
            {
                elapsedTimer.Stop();
                elapsedTimer.Dispose();
            }

            elapsedTimer = new System.Windows.Forms.Timer();
            elapsedTimer.Interval = 1000; // Update every 1 second
            elapsedTimer.Tick += (s, e) => UpdateElapsedTimeDisplay();
            elapsedTimer.Start();
            UpdateElapsedTimeDisplay();
        }

        private void StopElapsedTimer()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(StopElapsedTimer));
                return;
            }

            if (elapsedTimer != null)
            {
                elapsedTimer.Stop();
                elapsedTimer.Dispose();
                elapsedTimer = null;
            }
        }

        private void UpdateElapsedTimeDisplay()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateElapsedTimeDisplay));
                return;
            }

            if (upgradeTimer != null && upgradeTimer.IsRunning && elapsedTimeLabel != null)
            {
                var elapsed = upgradeTimer.Elapsed;
                elapsedTimeLabel.Text = "Elapsed (mm:ss): " + FormatElapsedForUi(elapsed);
            }
        }

        private void UpdateOverallProgress()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateOverallProgress));
                return;
            }

            var rows = GetComponentRows().Where(row => row.Item5).ToList();
            if (rows.Count == 0)
            {
                overallProgress.Value = 0;
                UpdateCompletedLabel();
                RefreshOverallStatusLabel();
                return;
            }

            int totalProgress = rows.Sum(row => row.Item4.Value);
            overallProgress.Value = Math.Min(totalProgress / rows.Count, 100);
            completedComponents = rows.Count(row => row.Item4.Value >= 100);

            UpdateCompletedLabel();
            RefreshOverallStatusLabel();
        }

        private void UpdateCompletedLabel()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateCompletedLabel));
                return;
            }

            if (totalComponents > 0)
                completedLabel.Text = string.Format("Overall Progress: {0}% complete ({1}/{2} ready)", overallProgress.Value, completedComponents, totalComponents);
            else
                completedLabel.Text = string.Format("Overall Progress: {0}% complete", overallProgress.Value);
        }

        private void UpdateCurrentTask(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateCurrentTask(message)));
                return;
            }

            overallStatusOverride = SimplifyOverallStatusMessage(message);
            currentTaskLabel.Text = overallStatusOverride;
        }

        private void RefreshOverallStatusLabel()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(RefreshOverallStatusLabel));
                return;
            }

            if (!string.IsNullOrEmpty(overallStatusOverride))
            {
                currentTaskLabel.Text = overallStatusOverride;
                return;
            }

            currentTaskLabel.Text = BuildOverallStatusMessage();
        }

        private string BuildOverallStatusMessage()
        {
            var rows = GetComponentRows()
                .Where(row => row.Item5)
                .OrderByDescending(row => GetComponentPriority(row.Item1))
                .ToList();

            var failed = rows.FirstOrDefault(row => row.Item4.Value < 100 && string.Equals(row.Item3.Text, "Failed", StringComparison.OrdinalIgnoreCase));
            if (failed != null)
                return "Check: one or more updates need attention";

            if (completedComponents == totalComponents && totalComponents > 0)
                return "Done: Portable environment updated";

            if (overallProgress.Value >= 95)
                return "Now: Finalizing update";

            if (overallProgress.Value > 0)
                return "Now: Applying updates";

            if (upgradeStarted && !upgradeCompleted)
                return "Now: Preparing updates";

            return "Ready to upgrade";
        }

        private IEnumerable<Tuple<string, Label, Label, ProgressBar, bool>> GetComponentRows()
        {
            yield return Tuple.Create("PowerShell 7", pwsh7NameLabel, pwsh7StatusLabel, pwsh7ProgressBar, pwsh7NameLabel.Enabled);
            yield return Tuple.Create("Oh My Posh", ohmyposhNameLabel, ohmyposhStatusLabel, ohmyposhProgressBar, ohmyposhNameLabel.Enabled);
            yield return Tuple.Create("Terminal-Icons", termIconsNameLabel, termIconsStatusLabel, termIconsProgressBar, termIconsNameLabel.Enabled);
            yield return Tuple.Create("PSFzf", psfzfNameLabel, psfzfStatusLabel, psfzfProgressBar, psfzfNameLabel.Enabled);
            yield return Tuple.Create("modern-unix-win", modernUnixNameLabel, modernUnixStatusLabel, modernUnixProgressBar, modernUnixNameLabel.Enabled);
            yield return Tuple.Create("VSCode", vscodeNameLabel, vscodeStatusLabel, vscodeProgressBar, vscodeNameLabel.Enabled);
        }

        private int GetComponentPriority(string componentName)
        {
            switch (componentName)
            {
                case "VSCode":
                    return 6;
                case "PowerShell 7":
                    return 5;
                case "Oh My Posh":
                    return 4;
                case "Terminal-Icons":
                    return 3;
                case "PSFzf":
                    return 2;
                case "modern-unix-win":
                    return 1;
                default:
                    return 0;
            }
        }

        private string GetDisplayComponentName(string componentName)
        {
            return string.Equals(componentName, "VSCode", StringComparison.OrdinalIgnoreCase)
                ? "VS Code"
                : componentName;
        }

        private string FormatElapsedForUi(TimeSpan elapsed)
        {
            return string.Format("{0:D2}:{1:D2}", (int)elapsed.TotalMinutes, elapsed.Seconds);
        }

        private string SimplifyOverallStatusMessage(string message)
        {
            string normalized = (message ?? "").Trim();
            if (string.IsNullOrEmpty(normalized))
                return "Ready to upgrade";

            if (normalized.StartsWith("Now:") || normalized.StartsWith("Done:") || normalized.StartsWith("Check:") || normalized.StartsWith("Queued:"))
                return normalized;

            if (normalized.IndexOf("Upgrade completed successfully", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Done: Updates applied successfully";
            if (normalized.IndexOf("Upgrade error", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Check: Upgrade failed";

            return normalized.TrimStart('✓', '⚠', ' ');
        }

        private string SimplifyActivityMessage(string message)
        {
            string normalized = (message ?? "").Trim();
            if (string.IsNullOrEmpty(normalized))
                return null;

            normalized = normalized.Replace("VSCode", "VS Code");

            if (normalized.IndexOf("Parallel extraction using", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("Created directory:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("Received version info -", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("Upgrade log saved to:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("Skipping recursive unblock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("pwsh.exe extracted successfully", StringComparison.OrdinalIgnoreCase) >= 0)
                return null;

            if (Regex.IsMatch(normalized, @"Downloading\.\.\.\s+\d+%", RegexOptions.IgnoreCase))
                return null;

            if (normalized.StartsWith("=== Upgrade started at ", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("=== Upgrade completed at ", StringComparison.OrdinalIgnoreCase))
                return normalized.Trim('=').Trim();

            if (normalized.StartsWith("*** Total upgrade time:", StringComparison.OrdinalIgnoreCase))
                return "Total time: " + FormatDurationFromLog(normalized);

            if (normalized.Equals("Checking for running Code.exe processes...", StringComparison.OrdinalIgnoreCase))
                return "Checking for running VS Code windows";
            if (normalized.IndexOf("Found ", StringComparison.OrdinalIgnoreCase) == 0 && normalized.IndexOf("Code.exe process", StringComparison.OrdinalIgnoreCase) > 0)
                return "Closing running VS Code windows";
            if (normalized.Equals("Code.exe processes terminated.", StringComparison.OrdinalIgnoreCase))
                return "VS Code windows closed";
            if (normalized.Equals("PowerShell 7: Upgrading first (required for modules)...", StringComparison.OrdinalIgnoreCase))
                return "PowerShell 7: Updating first for module compatibility";

            var upgradedVersionMatch = Regex.Match(normalized, @"^(PowerShell 7|Oh My Posh|Terminal-Icons|PSFzf|modern-unix-win|VS Code):\s+Upgrade completed\s+-\s+version\s+(.+)$", RegexOptions.IgnoreCase);
            if (upgradedVersionMatch.Success)
                return upgradedVersionMatch.Groups[1].Value + ": Updated to " + upgradedVersionMatch.Groups[2].Value.Trim();

            return normalized;
        }

        private string FormatDurationFromLog(string message)
        {
            var match = Regex.Match(message, @"(\d{2}):(\d{2}):(\d{2})");
            if (!match.Success)
                return message;

            int hours = int.Parse(match.Groups[1].Value);
            int minutes = int.Parse(match.Groups[2].Value);
            int seconds = int.Parse(match.Groups[3].Value);
            return string.Format("{0:D2}:{1:D2}", (hours * 60) + minutes, seconds);
        }

        private void AppendActivityLogEntry(string entry)
        {
            if (string.IsNullOrEmpty(entry))
                return;

            if (activityDisplayEntries.Count > 0 && activityDisplayEntries.Last() == entry)
                return;

            activityDisplayEntries.Enqueue(entry);
            while (activityDisplayEntries.Count > MAX_ACTIVITY_LOG_LINES)
                activityDisplayEntries.Dequeue();

            activityLog.Lines = activityDisplayEntries.ToArray();
            activityLog.SelectionStart = activityLog.Text.Length;
            activityLog.ScrollToCaret();
        }

        private async Task<KeyValuePair<string, string>> UpgradePowerShell7Async()
        {
            const string component = "PowerShell 7";
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "vscode-pvs-upgrade");

                var installer = new PowerShellInstaller(
                    pvsDir,
                    (name, status, progress) => UpdateComponentStatus(component, ConvertStatus(status), progress),
                    (message) => LogActivity(message)
                );

                var result = await installer.InstallAsync(tempDir, true);

                if (!string.IsNullOrEmpty(result.Value))
                {
                    UpdateComponentStatus(component, "Completed", 100);
                    LogActivity("PowerShell 7: Upgrade completed - version " + result.Value);
                    return result;
                }
                else
                {
                    throw new Exception("PowerShell 7 installation returned empty version");
                }
            }
            catch (Exception ex)
            {
                UpdateComponentStatus(component, "Error", 0);
                LogActivity("PowerShell 7: Error - " + ex.Message);
                return new KeyValuePair<string, string>();
            }
        }

        private async Task<KeyValuePair<string, string>> UpgradeOhMyPoshAsync()
        {
            const string component = "Oh My Posh";
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "vscode-pvs-upgrade");

                var installer = new OhMyPoshInstaller(
                    pvsDir,
                    (name, status, progress) => UpdateComponentStatus(component, ConvertStatus(status), progress),
                    (message) => LogActivity(message)
                );

                var result = await installer.InstallAsync(tempDir);

                if (!string.IsNullOrEmpty(result.Value))
                {
                    UpdateComponentStatus(component, "Completed", 100);
                    LogActivity("Oh My Posh: Upgrade completed - version " + result.Value);
                    return result;
                }
                else
                {
                    throw new Exception("Oh My Posh installation returned empty version");
                }
            }
            catch (Exception ex)
            {
                UpdateComponentStatus(component, "Error", 0);
                LogActivity("Oh My Posh: Error - " + ex.Message);
                return new KeyValuePair<string, string>();
            }
        }

        private async Task<KeyValuePair<string, string>> UpgradeModuleAsync(string moduleName, string versionKey)
        {
            try
            {
                var installer = new ModuleInstaller(
                    pvsDir,
                    (name, status, progress) => UpdateComponentStatus(moduleName, ConvertStatus(status), progress),
                    (message) => LogActivity(message)
                );

                var result = await installer.InstallModuleAsync(moduleName, versionKey);

                if (!string.IsNullOrEmpty(result.Value))
                {
                    UpdateComponentStatus(moduleName, "Completed", 100);
                    LogActivity(moduleName + ": Upgrade completed - version " + result.Value);
                    return result;
                }
                else
                {
                    throw new Exception(moduleName + " installation returned empty version");
                }
            }
            catch (Exception ex)
            {
                UpdateComponentStatus(moduleName, "Error", 0);
                LogActivity(moduleName + ": Error - " + ex.Message);
                return new KeyValuePair<string, string>();
            }
        }

        private async Task<KeyValuePair<string, string>> UpgradeVSCodeAsync()
        {
            const string component = "VSCode";
            try
            {
                var installer = new VSCodeInstaller(
                    pvsDir,
                    (name, status, progress) => UpdateComponentStatus(component, ConvertStatus(status), progress),
                    (message) => LogActivity(message)
                );

                var result = await installer.UpgradeAsync(Path.Combine(Path.GetTempPath(), "vscode-pvs-upgrade"));

                if (!string.IsNullOrEmpty(result.Value))
                {
                    UpdateComponentStatus(component, "Completed", 100);
                    LogActivity("VSCode: Upgrade completed - version " + result.Value);
                    return result;
                }
                else
                {
                    throw new Exception("VSCode installation returned empty version");
                }
            }
            catch (Exception ex)
            {
                UpdateComponentStatus(component, "Error", 0);
                LogActivity("VSCode: Error - " + ex.Message);
                return new KeyValuePair<string, string>();
            }
        }

        private string ConvertStatus(string status)
        {
            if (status.Contains("Checking") || status.Contains("Requesting"))
                return "Downloading";
            else if (status.Contains("Downloading") || status.Contains("download"))
                return "Downloading";
            else if (status.Contains("Extracting") || status.Contains("extract") ||
                     status.Contains("Installing") || status.Contains("Configuring") ||
                     status.Contains("Creating") || status.Contains("Running") ||
                     status.Contains("Verifying"))
                return "Installing";
            else if (status.StartsWith("✓") || status.Contains("Completed"))
                return "Completed";
            else if (status.StartsWith("⚠") || status.Contains("Error"))
                return "Error";
            else if (status.Contains("Waiting"))
                return "Waiting";
            return status;
        }

        private void DeleteUpgradeFlags()
        {
            var flagFiles = new[]
            {
                Path.Combine(pvsDir, "upgrade_pwsh7"),
                Path.Combine(pvsDir, "upgrade_ohmyposh"),
                Path.Combine(pvsDir, "upgrade_term_icons"),
                Path.Combine(pvsDir, "upgrade_psfzf"),
                Path.Combine(pvsDir, "upgrade_modern_unix"),
                Path.Combine(pvsDir, "upgrade_vscode")
            };

            foreach (var flag in flagFiles)
            {
                if (File.Exists(flag))
                {
                    File.Delete(flag);
                }
            }
        }

        private Dictionary<string, string> ReadVersionsFromInfo()
        {
            var result = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(pvsInfo))
            {
                int idx = line.IndexOf('=');
                if (idx > 0)
                {
                    result[line.Substring(0, idx)] = line.Substring(idx + 1);
                }
            }
            return result;
        }

        private void UpdateVersionsInInfo(List<KeyValuePair<string, string>> updates)
        {
            var lines = File.ReadAllLines(pvsInfo).ToList();

            foreach (var update in updates)
            {
                bool found = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith(update.Key + "="))
                    {
                        lines[i] = update.Key + "=" + update.Value;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    lines.Add(update.Key + "=" + update.Value);
                }
            }

            File.WriteAllLines(pvsInfo, lines);
        }

        private void ShowError(string message)
        {
            Invoke(new Action(() =>
            {
                MessageBox.Show(this, message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }));
        }

        private void SaveUpgradeLog()
        {
            try
            {
                string logPath = Path.Combine(pvsDir, CommonHelper.RuntimeLogFileName);
                string logHeader = "\r\n" + new string('=', 80) + "\r\n";
                logHeader += "Upgrade Session: " + upgradeStartTime.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n";
                logHeader += new string('=', 80) + "\r\n";

                string logContent;
                lock (logFileLock)
                {
                    logContent = logFileBuffer.ToString();
                }

                File.AppendAllText(logPath, logHeader + logContent + "\r\n", Encoding.UTF8);
                LogActivity("Upgrade log saved to: " + logPath);
            }
            catch (Exception ex)
            {
                LogActivity("Failed to save upgrade log: " + ex.Message);
            }
        }
    }
}
