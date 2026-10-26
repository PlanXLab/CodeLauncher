using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VSCodePortableCommon
{
    public static class CodeLauncherTheme
    {
        public static readonly Color Background = Color.FromArgb(238, 243, 249);
        public static readonly Color Surface = Color.White;
        public static readonly Color LogBackground = Color.FromArgb(247, 249, 252);
        public static readonly Color Border = Color.FromArgb(211, 221, 232);
        public static readonly Color Header = Color.FromArgb(35, 58, 83);
        public static readonly Color HeaderSubtitle = Color.FromArgb(218, 229, 241);
        public static readonly Color HeaderBrand = Color.FromArgb(180, 199, 219);
        public static readonly Color TextPrimary = Color.FromArgb(35, 45, 58);
        public static readonly Color TextSecondary = Color.FromArgb(83, 97, 114);
        public static readonly Color TextTertiary = Color.FromArgb(115, 130, 148);
        public static readonly Color Accent = Color.FromArgb(31, 111, 181);
        public static readonly Color AccentHover = Color.FromArgb(25, 94, 155);
        public static readonly Color AccentPressed = Color.FromArgb(19, 77, 130);
        public static readonly Color Success = Color.FromArgb(34, 125, 75);
        public static readonly Color Error = Color.FromArgb(190, 55, 55);
        public static readonly Color DisabledBackground = Color.FromArgb(226, 233, 241);

        private static readonly Font BodyFont = new Font("Segoe UI", 9F, FontStyle.Regular);
        private static readonly Font SmallFont = new Font("Segoe UI", 8.5F, FontStyle.Regular);
        private static readonly Font SmallEmphasisFont = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        private static readonly Font SectionFont = new Font("Segoe UI", 11F, FontStyle.Bold);
        private static readonly Font TitleFont = new Font("Segoe UI", 16F, FontStyle.Bold);
        private static readonly Font ButtonFont = new Font("Segoe UI", 9F, FontStyle.Bold);
        private static readonly Font LogFont = new Font("Consolas", 8.5F, FontStyle.Regular);

        public static void Apply(
            Form form,
            Panel headerPanel,
            Label titleLabel,
            Label subtitleLabel,
            Label brandLabel,
            Panel settingsPanel,
            Panel separatorPanel,
            Panel progressPanel,
            TableLayoutPanel componentTable,
            TextBox activityLog,
            Label logHeaderLabel,
            Button primaryButton,
            Button secondaryButton)
        {
            form.BackColor = Background;
            form.ForeColor = TextPrimary;
            form.Font = BodyFont;

            headerPanel.BackColor = Header;
            settingsPanel.BackColor = Surface;
            separatorPanel.BackColor = Border;
            progressPanel.BackColor = Background;
            componentTable.BackColor = Surface;

            ApplyControlDefaults(form);

            titleLabel.Font = TitleFont;
            titleLabel.ForeColor = Color.White;
            subtitleLabel.Font = SmallFont;
            subtitleLabel.ForeColor = HeaderSubtitle;
            brandLabel.Font = SmallFont;
            brandLabel.ForeColor = HeaderBrand;
            brandLabel.Text = "PlanXLab";
            logHeaderLabel.Font = SmallEmphasisFont;
            logHeaderLabel.ForeColor = TextTertiary;
            logHeaderLabel.Text = "ACTIVITY LOG";

            activityLog.BackColor = LogBackground;
            activityLog.BorderStyle = BorderStyle.FixedSingle;
            activityLog.Font = LogFont;
            activityLog.ForeColor = TextSecondary;

            StylePrimaryButton(primaryButton);
            StyleSecondaryButton(secondaryButton);
        }

        public static void StylePrimaryButton(Button button)
        {
            button.BackColor = Accent;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = AccentHover;
            button.FlatAppearance.MouseDownBackColor = AccentPressed;
            button.Font = ButtonFont;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
        }

        public static void StyleSecondaryButton(Button button)
        {
            button.BackColor = Surface;
            button.ForeColor = TextPrimary;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = Background;
            button.FlatAppearance.MouseDownBackColor = Border;
            button.Font = BodyFont;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
        }

        public static void StyleBusyButton(Button button, string text)
        {
            button.Enabled = false;
            button.Text = text;
            button.BackColor = DisabledBackground;
            button.ForeColor = TextSecondary;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.WaitCursor;
        }

        private static void ApplyControlDefaults(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                Label label = control as Label;
                if (label != null)
                {
                    string name = label.Name ?? string.Empty;
                    label.Font = BodyFont;
                    label.ForeColor = TextPrimary;

                    if (name.EndsWith("StatusLabel", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("pythonHint", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("summaryBodyLabel", StringComparison.OrdinalIgnoreCase))
                    {
                        label.Font = SmallFont;
                        label.ForeColor = TextSecondary;
                    }
                    else if (name.EndsWith("NameLabel", StringComparison.OrdinalIgnoreCase))
                    {
                        label.Font = BodyFont;
                    }
                    else if (name.Equals("summaryTitleLabel", StringComparison.OrdinalIgnoreCase))
                    {
                        label.Font = SectionFont;
                    }
                    else if (name.Equals("currentTaskLabel", StringComparison.OrdinalIgnoreCase))
                    {
                        label.Font = SmallEmphasisFont;
                        label.ForeColor = Accent;
                    }
                    else if (name.Equals("completedLabel", StringComparison.OrdinalIgnoreCase) ||
                             name.Equals("elapsedTimeLabel", StringComparison.OrdinalIgnoreCase))
                    {
                        label.Font = SmallFont;
                        label.ForeColor = TextSecondary;
                    }
                }

                TextBox textBox = control as TextBox;
                if (textBox != null)
                {
                    textBox.BackColor = Surface;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    textBox.Font = BodyFont;
                    textBox.ForeColor = TextPrimary;
                }

                TableLayoutPanel table = control as TableLayoutPanel;
                if (table != null)
                    table.BackColor = Surface;

                if (control.HasChildren)
                    ApplyControlDefaults(control);
            }
        }
    }

    /// <summary>
    /// Helper class for file extraction (C# 5 compatible)
    /// </summary>
    internal class FileData
    {
        public string Path { get; set; }
        public byte[] Data { get; set; }
    }

    internal class ZipEntryWorkItem
    {
        public string FullName { get; set; }
        public long Length { get; set; }
    }

    internal sealed class ParallelZipArchiveReader : IDisposable
    {
        private readonly FileStream zipStream;
        private readonly ZipArchive archive;

        public ParallelZipArchiveReader(string zipPath)
        {
            zipStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                CommonHelper.EXTRACT_BUFFER_SIZE, FileOptions.SequentialScan);
            archive = new ZipArchive(zipStream, ZipArchiveMode.Read, false);
        }

        public ZipArchiveEntry GetEntry(string fullName)
        {
            return archive.GetEntry(fullName);
        }

        public void Dispose()
        {
            archive.Dispose();
            zipStream.Dispose();
        }
    }

    /// <summary>
    /// Common utilities for VSCode Portable installer and launcher
    /// </summary>
    public static class CommonHelper
    {
        // Constants - Optimized buffer sizes
        public const int BUFFER_SIZE = 65536; // 64KB for better I/O performance
        public const int EXTRACT_BUFFER_SIZE = 81920; // 80KB for extraction
        public const string RuntimeInfoFileName = "codelauncher.info";
        public const string RuntimeLogFileName = "codelauncher.log";

        public static bool RemoveUtf8BomFromFile(string filePath)
        {
            byte[] contents = File.ReadAllBytes(filePath);
            if (contents.Length < 3 ||
                contents[0] != 0xEF ||
                contents[1] != 0xBB ||
                contents[2] != 0xBF)
            {
                return false;
            }

            string text = new UTF8Encoding(false, true)
                .GetString(contents, 3, contents.Length - 3)
                .TrimStart('\uFEFF');
            string tempPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tempPath, text, new UTF8Encoding(false));
                File.Replace(tempPath, filePath, null);
                return true;
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        public static void EnsureEmbeddedPythonWorkspaceImports(string pythonDir)
        {
            if (string.IsNullOrWhiteSpace(pythonDir))
                throw new ArgumentException("A Python installation directory is required.", "pythonDir");
            if (!Directory.Exists(pythonDir))
                throw new DirectoryNotFoundException("Embedded Python directory not found: " + pythonDir);

            string[] pathConfigurationFiles = Directory.GetFiles(pythonDir, "python*._pth", SearchOption.TopDirectoryOnly);
            foreach (string pathConfigurationFile in pathConfigurationFiles)
            {
                byte[] originalBytes = File.ReadAllBytes(pathConfigurationFile);
                bool hasUtf8Bom = originalBytes.Length >= 3 &&
                    originalBytes[0] == 0xEF &&
                    originalBytes[1] == 0xBB &&
                    originalBytes[2] == 0xBF;
                string originalContents = new UTF8Encoding(false, true).GetString(
                    originalBytes,
                    hasUtf8Bom ? 3 : 0,
                    originalBytes.Length - (hasUtf8Bom ? 3 : 0));
                string updatedContents = EnsurePythonPathConfiguration(originalContents);

                if (hasUtf8Bom || !string.Equals(originalContents, updatedContents, StringComparison.Ordinal))
                    WriteUtf8FileAtomicallyIfChanged(pathConfigurationFile, updatedContents);
            }

            string sitePackagesDir = Path.Combine(pythonDir, "Lib", "site-packages");
            Directory.CreateDirectory(sitePackagesDir);

            const string workspacePathHookFileName = "codelauncher_workspace_paths.py";
            const string workspacePathHookContents =
                "import os\r\n" +
                "import sys\r\n" +
                "\r\n" +
                "def _add_workspace_paths():\r\n" +
                "    candidates = []\r\n" +
                "    if sys.argv and sys.argv[0] and sys.argv[0] not in ('-c', '-m'):\r\n" +
                "        script_path = os.path.abspath(sys.argv[0])\r\n" +
                "        script_dir = script_path if os.path.isdir(script_path) else os.path.dirname(script_path)\r\n" +
                "        if script_dir:\r\n" +
                "            candidates.append(script_dir)\r\n" +
                "    try:\r\n" +
                "        workspace_dir = os.getcwd()\r\n" +
                "        candidates.append(workspace_dir)\r\n" +
                "        candidates.append(os.path.join(workspace_dir, 'src'))\r\n" +
                "    except OSError:\r\n" +
                "        pass\r\n" +
                "    existing = set()\r\n" +
                "    for entry in sys.path:\r\n" +
                "        try:\r\n" +
                "            existing.add(os.path.normcase(os.path.abspath(entry or os.getcwd())))\r\n" +
                "        except OSError:\r\n" +
                "            continue\r\n" +
                "    for candidate in reversed(candidates):\r\n" +
                "        normalized = os.path.normcase(os.path.abspath(candidate))\r\n" +
                "        if os.path.isdir(candidate) and normalized not in existing:\r\n" +
                "            sys.path.insert(0, candidate)\r\n" +
                "            existing.add(normalized)\r\n" +
                "\r\n" +
                "_add_workspace_paths()\r\n" +
                "del _add_workspace_paths\r\n";
            string workspacePathHookFile = Path.Combine(sitePackagesDir, workspacePathHookFileName);
            WriteUtf8FileAtomicallyIfChanged(workspacePathHookFile, workspacePathHookContents);

            string workspacePathHookEntry = "import codelauncher_workspace_paths" + Environment.NewLine;
            string workspacePathHookPth = Path.Combine(sitePackagesDir, "codelauncher_workspace_paths.pth");
            WriteUtf8FileAtomicallyIfChanged(workspacePathHookPth, workspacePathHookEntry);
        }

        private static string EnsurePythonPathConfiguration(string contents)
        {
            string newline = contents.IndexOf("\r\n", StringComparison.Ordinal) >= 0
                ? "\r\n"
                : Environment.NewLine;
            var lines = Regex.Split(contents, "\r\n|\n|\r").ToList();
            bool hadTrailingNewline = contents.EndsWith("\r", StringComparison.Ordinal) ||
                contents.EndsWith("\n", StringComparison.Ordinal);
            int siteImportIndex = -1;
            bool hasSitePackages = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmedLine = lines[i].Trim();
                if (Regex.IsMatch(trimmedLine, @"^#\s*import\s+site\b", RegexOptions.IgnoreCase))
                {
                    lines[i] = "import site";
                    siteImportIndex = i;
                }
                else if (Regex.IsMatch(trimmedLine, @"^import\s+site\b", RegexOptions.IgnoreCase))
                {
                    siteImportIndex = i;
                }

                string normalizedPath = trimmedLine.Replace('/', '\\').TrimEnd('\\');
                if (string.Equals(normalizedPath, @"Lib\site-packages", StringComparison.OrdinalIgnoreCase))
                    hasSitePackages = true;
            }

            if (!hasSitePackages)
            {
                int insertIndex = siteImportIndex >= 0 ? siteImportIndex : lines.Count;
                lines.Insert(insertIndex, @"Lib\site-packages");
                if (siteImportIndex >= 0)
                    siteImportIndex++;
            }

            if (siteImportIndex < 0)
                lines.Add("import site");

            string updatedContents = string.Join(newline, lines);
            if (hadTrailingNewline && !updatedContents.EndsWith(newline, StringComparison.Ordinal))
                updatedContents += newline;
            return updatedContents;
        }

        private static void WriteUtf8FileAtomicallyIfChanged(string filePath, string contents)
        {
            if (File.Exists(filePath) &&
                string.Equals(File.ReadAllText(filePath, new UTF8Encoding(false, true)), contents, StringComparison.Ordinal))
            {
                return;
            }

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tempPath, contents, new UTF8Encoding(false));
                if (File.Exists(filePath))
                    File.Replace(tempPath, filePath, null);
                else
                    File.Move(tempPath, filePath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        public static void MigrateLegacyRuntimeFiles(string installDir)
        {
            MigrateLegacyRuntimeFile(installDir, "pvs.info", RuntimeInfoFileName);
            MigrateLegacyRuntimeFile(Path.Combine(installDir, "data"), "pvs.info", RuntimeInfoFileName);
            MigrateLegacyRuntimeFile(installDir, "pvs.log", RuntimeLogFileName);
        }

        private static void MigrateLegacyRuntimeFile(string directory, string legacyFileName, string currentFileName)
        {
            string legacyPath = Path.Combine(directory, legacyFileName);
            string currentPath = Path.Combine(directory, currentFileName);
            if (File.Exists(legacyPath) && !File.Exists(currentPath))
                File.Move(legacyPath, currentPath);
        }

        public static async Task DownloadFileWithRetryAsync(
            HttpClient client,
            string url,
            string destPath,
            System.Threading.CancellationToken cancellationToken,
            Action<long, long> reportProgress = null,
            int maxAttempts = 3)
        {
            if (client == null)
                throw new ArgumentNullException("client");
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("A download URL is required.", "url");
            if (maxAttempts < 1)
                throw new ArgumentOutOfRangeException("maxAttempts");

            string destinationDirectory = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string partialPath = destPath + "." + Guid.NewGuid().ToString("N") + ".part";

                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    {
                        int statusCode = (int)response.StatusCode;
                        if (!response.IsSuccessStatusCode)
                        {
                            if (statusCode == 408 || statusCode == 429 || statusCode >= 500)
                                throw new HttpRequestException("Transient download response: HTTP " + statusCode + ".");

                            throw new InvalidDataException("Download failed with HTTP " + statusCode + ".");
                        }

                        long? contentLength = response.Content.Headers.ContentLength;
                        long downloadedBytes = 0;
                        byte[] buffer = new byte[262144];
                        using (var input = await response.Content.ReadAsStreamAsync())
                        using (var output = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, true))
                        {
                            int bytesRead;
                            while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                            {
                                await output.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                                downloadedBytes += bytesRead;
                                if (reportProgress != null)
                                    reportProgress(downloadedBytes, contentLength.HasValue ? contentLength.Value : 0);
                            }
                        }

                        if (downloadedBytes == 0)
                            throw new IOException("The download returned no data.");
                        if (contentLength.HasValue && downloadedBytes != contentLength.Value)
                            throw new IOException("The downloaded size did not match the server's Content-Length.");
                    }

                    if (File.Exists(destPath))
                        File.Replace(partialPath, destPath, null);
                    else
                        File.Move(partialPath, destPath);
                    return;
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested ||
                        attempt == maxAttempts ||
                        !(ex is HttpRequestException || ex is IOException || ex is TaskCanceledException))
                    {
                        throw;
                    }
                }
                finally
                {
                    if (File.Exists(partialPath))
                        File.Delete(partialPath);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), cancellationToken);
            }
        }

        public static void ReplaceDirectoryAtomically(string stagingDir, string targetDir, string backupDir, Action<string> logActivity = null)
        {
            if (!Directory.Exists(stagingDir))
                throw new DirectoryNotFoundException("Staging directory not found: " + stagingDir);

            string stagingRoot = Path.GetPathRoot(Path.GetFullPath(stagingDir));
            string targetRoot = Path.GetPathRoot(Path.GetFullPath(targetDir));
            string backupRoot = Path.GetPathRoot(Path.GetFullPath(backupDir));
            if (!string.Equals(stagingRoot, targetRoot, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(targetRoot, backupRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Staging, target, and backup directories must be on the same volume.");
            }

            string targetParent = Path.GetDirectoryName(targetDir);
            if (!string.IsNullOrEmpty(targetParent))
                Directory.CreateDirectory(targetParent);

            RecoverDirectoryReplacement(targetDir, backupDir);

            bool hadTarget = Directory.Exists(targetDir);
            if (hadTarget)
                Directory.Move(targetDir, backupDir);

            try
            {
                Directory.Move(stagingDir, targetDir);
            }
            catch (Exception commitError)
            {
                try
                {
                    if (Directory.Exists(targetDir))
                        Directory.Delete(targetDir, true);
                    if (hadTarget && Directory.Exists(backupDir))
                        Directory.Move(backupDir, targetDir);
                }
                catch (Exception rollbackError)
                {
                    throw new AggregateException("Directory replacement failed and rollback also failed.", commitError, rollbackError);
                }

                throw;
            }

            if (hadTarget && Directory.Exists(backupDir))
            {
                try
                {
                    Directory.Delete(backupDir, true);
                }
                catch (IOException ex)
                {
                    if (logActivity != null)
                        logActivity("Updated directory is active, but old backup cleanup failed: " + ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    if (logActivity != null)
                        logActivity("Updated directory is active, but old backup cleanup failed: " + ex.Message);
                }
            }
        }

        public static void RecoverDirectoryReplacement(string targetDir, string backupDir)
        {
            if (!Directory.Exists(backupDir))
                return;

            if (Directory.Exists(targetDir))
                Directory.Delete(backupDir, true);
            else
                Directory.Move(backupDir, targetDir);
        }

        /// <summary>
        /// Enable TLS 1.2 for HTTPS requests
        /// </summary>
        public static void EnableTls12()
        {
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 |
                System.Net.SecurityProtocolType.Tls11 |
                System.Net.SecurityProtocolType.Tls;
        }

        public static async Task<string> GetLatestPythonPatchVersionAsync(string majorMinor)
        {
            string requestedVersion = (majorMinor ?? string.Empty).Trim();
            bool findLatestAvailable = string.Equals(requestedVersion, "latest", StringComparison.OrdinalIgnoreCase);
            if (!findLatestAvailable && !Regex.IsMatch(requestedVersion, @"^3\.\d+$"))
            {
                throw new ArgumentException("Python version must be 'latest' or have the form 3.x.", "majorMinor");
            }

            EnableTls12();

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };

            using (var client = new HttpClient(handler))
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("CodeLauncher/1.0");
                string html = await client.GetStringAsync("https://www.python.org/ftp/python/");
                string versionPattern = findLatestAvailable
                    ? @"3\.\d+\.\d+"
                    : Regex.Escape(requestedVersion) + @"\.\d+";
                var matches = Regex.Matches(
                    html,
                    @"href=[""'](?<version>" + versionPattern + @")/[""']",
                    RegexOptions.IgnoreCase);

                var candidates = new List<KeyValuePair<Version, string>>();
                foreach (Match match in matches)
                {
                    Version parsedVersion;
                    string candidate = match.Groups["version"].Value;
                    if (Version.TryParse(candidate, out parsedVersion) &&
                        !candidates.Any(item => item.Key.Equals(parsedVersion)))
                        candidates.Add(new KeyValuePair<Version, string>(parsedVersion, candidate));
                }

                foreach (var candidate in candidates.OrderByDescending(item => item.Key))
                {
                    string packageUrl = "https://www.python.org/ftp/python/" + candidate.Value +
                        "/python-" + candidate.Value + "-embed-amd64.zip";
                    using (var request = new HttpRequestMessage(HttpMethod.Head, packageUrl))
                    using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (response.IsSuccessStatusCode)
                            return candidate.Value;

                        bool headCanIndicateUnavailablePackage =
                            response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed ||
                            response.StatusCode == System.Net.HttpStatusCode.NotImplemented ||
                            response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                            response.StatusCode == System.Net.HttpStatusCode.Gone;
                        using (var getRequest = new HttpRequestMessage(HttpMethod.Get, packageUrl))
                        {
                            getRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                            using (var getResponse = await client.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead))
                            {
                                if (getResponse.IsSuccessStatusCode)
                                    return candidate.Value;

                                if (getResponse.StatusCode == System.Net.HttpStatusCode.NotFound ||
                                    getResponse.StatusCode == System.Net.HttpStatusCode.Gone)
                                {
                                    if (headCanIndicateUnavailablePackage)
                                        continue;
                                    response.EnsureSuccessStatusCode();
                                }

                                getResponse.EnsureSuccessStatusCode();
                            }
                        }
                    }
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Copy directory recursively with optimized buffering
        /// </summary>
        public static void CopyDirectoryRecursive(string sourceDir, string destDir, bool overwrite = true)
        {
            Directory.CreateDirectory(destDir);

            // Get all files and copy in parallel for better performance
            var files = Directory.GetFiles(sourceDir);
            System.Threading.Tasks.Parallel.ForEach(files, file =>
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite);
            });

            // Process subdirectories recursively
            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectoryRecursive(dir, destSubDir, overwrite);
            }
        }

        public static Task ExtractZipWithProgressAsync(
            string zipPath,
            string extractPath,
            string componentName,
            int startProgress,
            int endProgress,
            Action<int> reportProgress,
            Action<string> logActivity,
            System.Threading.CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<ZipEntryWorkItem> fileEntries;
                long totalBytes = 0;

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var entries = archive.Entries.ToList();
                    Directory.CreateDirectory(extractPath);

                    foreach (var entry in entries.Where(e => string.IsNullOrEmpty(e.Name)))
                    {
                        string dirPath = GetSafeExtractionPath(extractPath, entry.FullName);
                        Directory.CreateDirectory(dirPath);
                    }

                    fileEntries = entries
                        .Where(e => !string.IsNullOrEmpty(e.Name))
                        .Select(e => new ZipEntryWorkItem { FullName = e.FullName, Length = Math.Max(e.Length, 1L) })
                        .OrderByDescending(e => e.Length)
                        .ToList();

                    totalBytes = fileEntries.Sum(e => e.Length);
                }

                if (fileEntries.Count == 0)
                {
                    reportProgress(endProgress);
                    return;
                }

                int workerCount = GetParallelExtractionWorkerCount(fileEntries.Count);
                logActivity(componentName + ": Parallel extraction using " + workerCount + " worker(s) for " + fileEntries.Count + " file(s)");

                var errors = new System.Collections.Concurrent.ConcurrentQueue<string>();
                long extractedBytes = 0;
                int processedEntries = 0;
                int lastProgress = startProgress;
                object progressLock = new object();

                var options = new System.Threading.Tasks.ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = workerCount
                };

                System.Threading.Tasks.Parallel.ForEach(
                    fileEntries,
                    options,
                    () => new ParallelZipArchiveReader(zipPath),
                    (entryInfo, loopState, reader) =>
                    {
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var entry = reader.GetEntry(entryInfo.FullName);
                            if (entry == null)
                                throw new InvalidOperationException("Entry not found in archive");

                            string destPath = GetSafeExtractionPath(extractPath, entryInfo.FullName);
                            string destDir = Path.GetDirectoryName(destPath);
                            if (!string.IsNullOrEmpty(destDir))
                                Directory.CreateDirectory(destDir);

                            using (var entryStream = entry.Open())
                            using (var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, EXTRACT_BUFFER_SIZE))
                            {
                                entryStream.CopyTo(fileStream, EXTRACT_BUFFER_SIZE);
                            }

                            long completedBytes = System.Threading.Interlocked.Add(ref extractedBytes, entryInfo.Length);
                            int currentProgress = startProgress + (int)((endProgress - startProgress) * completedBytes / Math.Max(totalBytes, 1L));

                            if (currentProgress > lastProgress)
                            {
                                lock (progressLock)
                                {
                                    if (currentProgress > lastProgress)
                                    {
                                        reportProgress(currentProgress);
                                        lastProgress = currentProgress;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Enqueue(entryInfo.FullName + ": " + ex.Message);
                        }
                        finally
                        {
                            System.Threading.Interlocked.Increment(ref processedEntries);
                        }

                        return reader;
                    },
                    reader => reader.Dispose());

                if (!errors.IsEmpty)
                {
                    string firstError;
                    if (!errors.TryPeek(out firstError))
                        firstError = "Unknown extraction error";
                    throw new Exception(componentName + " extraction failed. First error: " + firstError);
                }

                if (endProgress > lastProgress)
                    reportProgress(endProgress);
            }, cancellationToken);
        }

        private static int GetParallelExtractionWorkerCount(int fileCount)
        {
            if (fileCount < 64)
                return 1;

            int workerCount = Math.Max(2, Environment.ProcessorCount / 2);
            workerCount = Math.Min(workerCount, 6);
            return Math.Min(workerCount, fileCount);
        }

        private static string GetSafeExtractionPath(string basePath, string entryPath)
        {
            string normalizedBasePath = Path.GetFullPath(basePath);
            if (!normalizedBasePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                normalizedBasePath += Path.DirectorySeparatorChar;

            string fullPath = Path.GetFullPath(Path.Combine(basePath, entryPath));
            if (!fullPath.StartsWith(normalizedBasePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Entry resolves outside extraction directory: " + entryPath);

            return fullPath;
        }
    }

    /// <summary>
    /// PowerShell 7 installer/upgrader
    /// </summary>
    public class PowerShellInstaller
    {
        private readonly string baseDir;
        private readonly Action<string, string, int> updateStatus;
        private readonly Action<string> logActivity;

        public PowerShellInstaller(string baseDir, Action<string, string, int> updateStatus, Action<string> logActivity = null)
        {
            this.baseDir = baseDir;
            this.updateStatus = updateStatus;
            this.logActivity = logActivity ?? (_ => { });
        }

        public async Task<KeyValuePair<string, string>> InstallAsync(string tempBaseDir, bool preserveExisting = false)
        {
            string componentName = "PowerShell 7";
            string version = "";
            string stagingDir = null;

            try
            {
                updateStatus(componentName, "Checking version...", 5);
                logActivity("PowerShell 7: Checking latest version...");

                CommonHelper.EnableTls12();

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "VSCode-Portable-Installer");
                    client.DefaultRequestHeaders.ConnectionClose = false; // Keep-Alive
                    client.Timeout = TimeSpan.FromMinutes(20);

                    string url = "";

                    try
                    {
                        // Use GitHub redirect (no API rate limit) to get latest version
                        using (var request = new HttpRequestMessage(HttpMethod.Head, "https://github.com/PowerShell/PowerShell/releases/latest"))
                        {
                            request.Headers.Add("User-Agent", "VSCode-Portable-Installer");
                            using (var response = await client.SendAsync(request))
                            {
                                response.EnsureSuccessStatusCode();
                                string redirectUrl = response.RequestMessage.RequestUri.ToString();
                                var versionMatch = Regex.Match(redirectUrl, @"/releases/tag/v([\d\.]+)");
                                if (!versionMatch.Success)
                                    throw new InvalidDataException("Version not found in redirect URL: " + redirectUrl);

                                version = versionMatch.Groups[1].Value;
                                url = "https://github.com/PowerShell/PowerShell/releases/download/v" + version + "/PowerShell-" + version + "-win-x64.zip";
                                logActivity("PowerShell 7: Found latest version " + version + " (via redirect)");
                            }
                        }

                        if (string.IsNullOrWhiteSpace(url))
                        {
                            throw new InvalidDataException("PowerShell release URL was not resolved.");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Fail immediately without retry
                        string errorMsg = "Failed to check PowerShell 7 version. ";
                        errorMsg += "Internet connection required for installation. ";
                        errorMsg += "Error: " + ex.Message;

                        updateStatus(componentName, "⚠ Version check failed", 0);
                        logActivity("PowerShell 7: CRITICAL ERROR - " + errorMsg);
                        logActivity("PowerShell 7: Exception type: " + ex.GetType().Name);
                        if (ex.InnerException != null)
                            logActivity("PowerShell 7: Inner exception: " + ex.InnerException.Message);

                        throw new Exception(errorMsg);
                    }

                    updateStatus(componentName, "Downloading...", 10);
                    logActivity("PowerShell 7: Downloading " + version + "...");

                    string tempDir = Path.Combine(tempBaseDir, "pwsh7");

                    // Clean temp directory to avoid corrupted files
                    if (Directory.Exists(tempDir))
                    {
                        logActivity("PowerShell 7: Cleaning temp directory...");
                        Directory.Delete(tempDir, true);
                    }
                    Directory.CreateDirectory(tempDir);

                    string zipPath = Path.Combine(tempDir, "pwsh.zip");

                    try
                    {
                        await DownloadFileAsync(client, url, zipPath, componentName, 10, 40);
                    }
                    catch (Exception ex)
                    {
                        logActivity("PowerShell 7: Download failed - " + ex.Message);
                        updateStatus(componentName, "⚠ Download failed", 0);
                        throw new Exception("Download failed: " + ex.Message);
                    }

                    if (!File.Exists(zipPath) || new FileInfo(zipPath).Length == 0)
                        throw new InvalidDataException("PowerShell download was empty.");

                    string dataLibDir = Path.Combine(baseDir, "data", "lib");
                    Directory.CreateDirectory(dataLibDir);
                    string pwshDir = Path.Combine(dataLibDir, "pwsh");
                    stagingDir = Path.Combine(dataLibDir, ".pwsh-stage-" + Guid.NewGuid().ToString("N"));
                    string backupDir = Path.Combine(dataLibDir, ".pwsh-backup");

                    using (var archive = ZipFile.OpenRead(zipPath))
                    {
                        if (archive.GetEntry("pwsh.exe") == null)
                            throw new InvalidDataException("PowerShell archive does not contain pwsh.exe.");
                    }

                    updateStatus(componentName, "Extracting...", 45);
                    Directory.CreateDirectory(stagingDir);
                    await ExtractZipAsync(zipPath, stagingDir, componentName, 45, 80);

                    string stagedPwshExe = Path.Combine(stagingDir, "pwsh.exe");
                    if (!File.Exists(stagedPwshExe) || new FileInfo(stagedPwshExe).Length == 0)
                        throw new InvalidDataException("PowerShell extraction did not produce a valid pwsh.exe.");

                    if (preserveExisting && Directory.Exists(pwshDir))
                    {
                        logActivity("PowerShell 7: Preserving portable modules and custom tools...");
                        foreach (string dirName in new[] { "bin", "ohmyposh" })
                        {
                            string sourceDir = Path.Combine(pwshDir, dirName);
                            if (Directory.Exists(sourceDir))
                                CommonHelper.CopyDirectoryRecursive(sourceDir, Path.Combine(stagingDir, dirName));
                        }

                        foreach (string moduleName in new[] { "Terminal-Icons", "PSFzf", "modern-unix-win" })
                        {
                            string sourceDir = Path.Combine(pwshDir, "Modules", moduleName);
                            if (Directory.Exists(sourceDir))
                                CommonHelper.CopyDirectoryRecursive(sourceDir, Path.Combine(stagingDir, "Modules", moduleName));
                        }

                        string profilePath = Path.Combine(pwshDir, "Microsoft.PowerShell_profile.ps1");
                        if (File.Exists(profilePath))
                            File.Copy(profilePath, Path.Combine(stagingDir, "Microsoft.PowerShell_profile.ps1"), true);
                    }

                    var localeDirs = new[] { "cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant" };
                    foreach (string locale in localeDirs)
                    {
                        string localeDir = Path.Combine(stagingDir, locale);
                        if (Directory.Exists(localeDir))
                            Directory.Delete(localeDir, true);
                    }

                    updateStatus(componentName, "Installing...", 90);
                    CommonHelper.ReplaceDirectoryAtomically(
                        stagingDir,
                        pwshDir,
                        backupDir,
                        message => logActivity("PowerShell 7: " + message));
                    logActivity("PowerShell 7: pwsh.exe installed successfully");

                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch (IOException ex)
                    {
                        logActivity("PowerShell 7: Temporary file cleanup warning - " + ex.Message);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        logActivity("PowerShell 7: Temporary file cleanup warning - " + ex.Message);
                    }

                    updateStatus(componentName, preserveExisting ? "✓ Completed" : "Core installed", preserveExisting ? 100 : 60);
                }
            }
            catch (Exception ex)
            {
                version = "";
                updateStatus(componentName, "⚠ Error: " + ex.Message, 0);
                logActivity("PowerShell 7: Error - " + ex.Message);
                if (!string.IsNullOrEmpty(stagingDir) && Directory.Exists(stagingDir))
                {
                    try { Directory.Delete(stagingDir, true); }
                    catch (IOException cleanupError) { logActivity("PowerShell 7: Staging cleanup warning - " + cleanupError.Message); }
                    catch (UnauthorizedAccessException cleanupError) { logActivity("PowerShell 7: Staging cleanup warning - " + cleanupError.Message); }
                }
            }

            return string.IsNullOrEmpty(version)
                ? new KeyValuePair<string, string>()
                : new KeyValuePair<string, string>("PWSH7_VERSION", version);
        }

        private async Task DownloadFileAsync(HttpClient client, string url, string destPath, string componentName, int startProgress, int endProgress)
        {
            int lastProgress = startProgress;
            await CommonHelper.DownloadFileWithRetryAsync(
                client,
                url,
                destPath,
                default(System.Threading.CancellationToken),
                (downloaded, total) =>
                {
                    if (total <= 0)
                        return;
                    int progress = startProgress + (int)((endProgress - startProgress) * downloaded / total);
                    if (progress > lastProgress)
                    {
                        updateStatus(componentName, "Downloading...", Math.Min(progress, endProgress));
                        lastProgress = progress;
                    }
                });
        }

        private async Task ExtractZipAsync(string zipPath, string extractPath, string componentName, int startProgress, int endProgress)
        {
            await CommonHelper.ExtractZipWithProgressAsync(
                zipPath,
                extractPath,
                componentName,
                startProgress,
                endProgress,
                progress => updateStatus(componentName, "Extracting...", progress),
                message => logActivity(message),
                default(System.Threading.CancellationToken));
        }
    }

    /// <summary>
    /// Oh My Posh installer/upgrader
    /// </summary>
    public class OhMyPoshInstaller
    {
        private readonly string baseDir;
        private readonly Action<string, string, int> updateStatus;
        private readonly Action<string> logActivity;

        public OhMyPoshInstaller(string baseDir, Action<string, string, int> updateStatus, Action<string> logActivity = null)
        {
            this.baseDir = baseDir;
            this.updateStatus = updateStatus;
            this.logActivity = logActivity ?? (_ => { });
        }

        public async Task<KeyValuePair<string, string>> InstallAsync(string tempBaseDir)
        {
            string componentName = "Oh My Posh";
            string version = "";
            string tempDir = null;
            string stagedExePath = null;

            try
            {
                updateStatus(componentName, "Checking version...", 5);
                logActivity("Oh My Posh: Checking latest version...");

                string downloadUrl = "";

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "VSCode-Portable-Installer");
                    client.Timeout = TimeSpan.FromMinutes(5);

                    try
                    {
                        // Use GitHub redirect (no API rate limit) to get latest version
                        using (var request = new HttpRequestMessage(HttpMethod.Head, "https://github.com/JanDeDobbeleer/oh-my-posh/releases/latest"))
                        {
                            request.Headers.Add("User-Agent", "VSCode-Portable-Installer");
                            using (var response = await client.SendAsync(request))
                            {
                                response.EnsureSuccessStatusCode();
                                string redirectUrl = response.RequestMessage.RequestUri.ToString();
                                var versionMatch = Regex.Match(redirectUrl, @"/releases/tag/v([\d\.]+)");
                                if (!versionMatch.Success)
                                    throw new InvalidDataException("Version not found in redirect URL: " + redirectUrl);

                                version = versionMatch.Groups[1].Value;
                                downloadUrl = "https://github.com/JanDeDobbeleer/oh-my-posh/releases/download/v" + version + "/install-x64.msix";
                                logActivity("Oh My Posh: Found latest version " + version + " (via redirect)");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Fail immediately without retry
                        string errorMsg = "Failed to check Oh My Posh version. ";
                        errorMsg += "Internet connection required for installation. ";
                        errorMsg += "Error: " + ex.Message;

                        updateStatus(componentName, "⚠ Version check failed", 0);
                        logActivity("Oh My Posh: CRITICAL ERROR - " + errorMsg);
                        logActivity("Oh My Posh: Exception type: " + ex.GetType().Name);
                        if (ex.InnerException != null)
                            logActivity("Oh My Posh: Inner exception: " + ex.InnerException.Message);

                        throw new Exception(errorMsg);
                    }
                }

                updateStatus(componentName, "Downloading...", 10);
                logActivity("Oh My Posh: Downloading " + version + "...");

                tempDir = Path.Combine(tempBaseDir, "ohmyposh");
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                string msixPath = Path.Combine(tempDir, "ohmyposh.msix");

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(20);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("CodeLauncher/1.0");
                    client.DefaultRequestHeaders.ConnectionClose = false; // Keep-Alive
                    await CommonHelper.DownloadFileWithRetryAsync(client, downloadUrl, msixPath, default(System.Threading.CancellationToken));
                }

                if (!File.Exists(msixPath) || new FileInfo(msixPath).Length == 0)
                    throw new InvalidDataException("Oh My Posh download was empty.");

                logActivity("Oh My Posh: Download completed");
                updateStatus(componentName, "Extracting...", 55);
                logActivity("Oh My Posh: Extracting...");

                string extractDir = Path.Combine(tempDir, "ohmyposh_extract");
                ZipFile.ExtractToDirectory(msixPath, extractDir);

                updateStatus(componentName, "Installing...", 75);

                var exeFiles = Directory.GetFiles(extractDir, "oh-my-posh.exe", SearchOption.AllDirectories);
                if (exeFiles.Length > 0)
                {
                    updateStatus(componentName, "Replacing executable...", 85);
                    string pwshBin = Path.Combine(baseDir, "data", "lib", "pwsh", "bin");
                    Directory.CreateDirectory(pwshBin);

                    logActivity("Oh My Posh: Replacing executable...");

                    string targetExe = Path.Combine(pwshBin, "oh-my-posh.exe");
                    stagedExePath = targetExe + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    File.Copy(exeFiles[0], stagedExePath, true);
                    if (new FileInfo(stagedExePath).Length == 0)
                        throw new InvalidDataException("Downloaded Oh My Posh executable was empty.");
                    if (File.Exists(targetExe))
                        File.Replace(stagedExePath, targetExe, null);
                    else
                        File.Move(stagedExePath, targetExe);
                    stagedExePath = null;

                    updateStatus(componentName, "✓ Completed", 100);
                    logActivity("Oh My Posh: Installed " + version);
                }
                else
                {
                    throw new InvalidDataException("Oh My Posh package did not contain oh-my-posh.exe.");
                }

                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch (IOException ex)
                {
                    logActivity("Oh My Posh: Temporary file cleanup warning - " + ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    logActivity("Oh My Posh: Temporary file cleanup warning - " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                version = "";
                updateStatus(componentName, "⚠ Error: " + ex.Message, 0);
                logActivity("Oh My Posh: Error - " + ex.Message);
                if (!string.IsNullOrEmpty(stagedExePath) && File.Exists(stagedExePath))
                {
                    try { File.Delete(stagedExePath); }
                    catch (IOException cleanupError) { logActivity("Oh My Posh: Staging cleanup warning - " + cleanupError.Message); }
                    catch (UnauthorizedAccessException cleanupError) { logActivity("Oh My Posh: Staging cleanup warning - " + cleanupError.Message); }
                }
                if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); }
                    catch (IOException cleanupError) { logActivity("Oh My Posh: Temporary cleanup warning - " + cleanupError.Message); }
                    catch (UnauthorizedAccessException cleanupError) { logActivity("Oh My Posh: Temporary cleanup warning - " + cleanupError.Message); }
                }
            }

            return string.IsNullOrEmpty(version)
                ? new KeyValuePair<string, string>()
                : new KeyValuePair<string, string>("OHMYPOSH_VERSION", version);
        }
    }

    /// <summary>
    /// PowerShell module installer/upgrader
    /// </summary>
    public class ModuleInstaller
    {
        private readonly string baseDir;
        private readonly Action<string, string, int> updateStatus;
        private readonly Action<string> logActivity;

        public ModuleInstaller(string baseDir, Action<string, string, int> updateStatus, Action<string> logActivity = null)
        {
            this.baseDir = baseDir;
            this.updateStatus = updateStatus;
            this.logActivity = logActivity ?? (_ => { });
        }

        public async Task<KeyValuePair<string, string>> InstallModuleAsync(string moduleName, string versionKey, int maxWaitSeconds = 30)
        {
            string version = "";
            string modulesPath = Path.Combine(baseDir, "data", "lib", "pwsh", "Modules");
            string tempZip = Path.Combine(Path.GetTempPath(), moduleName + "_" + Guid.NewGuid().ToString().Substring(0, 8) + ".zip");
            string extractPath = Path.Combine(Path.GetTempPath(), moduleName + "_extract_" + Guid.NewGuid().ToString().Substring(0, 8));
            string stagingModuleDir = null;

            try
            {
                updateStatus(moduleName, "Downloading...", 10);
                logActivity(moduleName + ": Downloading from PSGallery API...");

                Directory.CreateDirectory(modulesPath);
                string downloadUrl = "https://www.powershellgallery.com/api/v2/package/" + moduleName;
                bool downloadSucceeded = false;

                try
                {
                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromMinutes(5);
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("CodeLauncher/1.0");
                        await CommonHelper.DownloadFileWithRetryAsync(
                            client,
                            downloadUrl,
                            tempZip,
                            System.Threading.CancellationToken.None);
                    }

                    downloadSucceeded = true;
                }
                catch (Exception dlEx)
                {
                    logActivity(moduleName + ": Fast download failed, falling back to Save-Module: " + dlEx.Message);
                }

                if (!downloadSucceeded)
                    return await InstallModuleLegacyAsync(moduleName, versionKey, maxWaitSeconds);

                updateStatus(moduleName, "Extracting...", 50);
                logActivity(moduleName + ": Extracting package...");
                Directory.CreateDirectory(extractPath);
                bool extractSucceeded = false;
                
                try
                {
                    await CommonHelper.ExtractZipWithProgressAsync(
                        tempZip,
                        extractPath,
                        moduleName,
                        50,
                        80,
                        progress => updateStatus(moduleName, "Extracting...", progress),
                        logActivity,
                        System.Threading.CancellationToken.None
                    );

                    extractSucceeded = true;
                }
                catch (Exception ex)
                {
                    logActivity(moduleName + ": Fast extract failed, falling back to Save-Module: " + ex.Message);
                }

                if (!extractSucceeded)
                {
                    try { if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true); } catch { }
                    try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                    return await InstallModuleLegacyAsync(moduleName, versionKey, maxWaitSeconds);
                }

                // Parse nuspec or psd1 for version
                updateStatus(moduleName, "Configuring...", 80);
                string[] nuspecs = Directory.GetFiles(extractPath, "*.nuspec", SearchOption.AllDirectories);
                if (nuspecs.Length > 0)
                {
                    string nuspecText = File.ReadAllText(nuspecs[0]);
                    var match = System.Text.RegularExpressions.Regex.Match(nuspecText, @"<version>(.+?)</version>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        version = match.Groups[1].Value.Trim();
                    }
                }

                if (string.IsNullOrEmpty(version))
                {
                    string[] psd1s = Directory.GetFiles(extractPath, "*.psd1", SearchOption.AllDirectories);
                    if (psd1s.Length > 0)
                    {
                        string psd1Text = File.ReadAllText(psd1s[0]);
                        var match = System.Text.RegularExpressions.Regex.Match(psd1Text, @"ModuleVersion\s*=\s*'([^']+)'", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            version = match.Groups[1].Value.Trim();
                        }
                        else
                        {
                            throw new InvalidDataException("Could not read the " + moduleName + " package version.");
                        }
                    }
                    else
                    {
                        throw new InvalidDataException("The " + moduleName + " package contains no module manifest.");
                    }
                }

                string finalModuleDir = Path.Combine(modulesPath, moduleName, version);
                string moduleParentDir = Path.GetDirectoryName(finalModuleDir);
                Directory.CreateDirectory(moduleParentDir);
                stagingModuleDir = Path.Combine(modulesPath, "." + moduleName + ".stage-" + Guid.NewGuid().ToString("N"));
                string backupModuleDir = Path.Combine(modulesPath, "." + moduleName + ".backup-" + version);
                CommonHelper.CopyDirectoryRecursive(extractPath, stagingModuleDir);
                if (Directory.GetFiles(stagingModuleDir, "*.psd1", SearchOption.AllDirectories).Length == 0)
                    throw new InvalidDataException("The extracted " + moduleName + " package has no module manifest.");

                CommonHelper.ReplaceDirectoryAtomically(stagingModuleDir, finalModuleDir, backupModuleDir, logActivity);
                try { Directory.Delete(extractPath, true); }
                catch (IOException ex) { logActivity(moduleName + ": Temporary extraction cleanup warning - " + ex.Message); }
                catch (UnauthorizedAccessException ex) { logActivity(moduleName + ": Temporary extraction cleanup warning - " + ex.Message); }
                try { File.Delete(tempZip); }
                catch (IOException ex) { logActivity(moduleName + ": Temporary package cleanup warning - " + ex.Message); }
                catch (UnauthorizedAccessException ex) { logActivity(moduleName + ": Temporary package cleanup warning - " + ex.Message); }

                logActivity(moduleName + ": Successfully installed version " + version);
                return new KeyValuePair<string, string>(versionKey, version);
            }
            catch (Exception ex)
            {
                logActivity(moduleName + ": Installation failed - " + ex.Message);
                if (!string.IsNullOrEmpty(stagingModuleDir) && Directory.Exists(stagingModuleDir))
                {
                    try { Directory.Delete(stagingModuleDir, true); }
                    catch (IOException cleanupError) { logActivity(moduleName + ": Staging cleanup warning - " + cleanupError.Message); }
                    catch (UnauthorizedAccessException cleanupError) { logActivity(moduleName + ": Staging cleanup warning - " + cleanupError.Message); }
                }
                try { if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true); } catch { }
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                return new KeyValuePair<string, string>(versionKey, "");
            }
        }

        public async Task<KeyValuePair<string, string>> InstallModuleLegacyAsync(string moduleName, string versionKey, int maxWaitSeconds = 30)
        {
                string version = "";

                try
                {
                    updateStatus(moduleName, "Waiting for PowerShell...", 10);
                    logActivity(moduleName + ": Waiting for PowerShell to be ready...");

                    string pwshExe = Path.Combine(baseDir, "data", "lib", "pwsh", "pwsh.exe");
                int retries = 0;
                while (!File.Exists(pwshExe) && retries < maxWaitSeconds * 2)
                {
                    await Task.Delay(500);
                    retries++;
                }

                if (!File.Exists(pwshExe))
                {
                    updateStatus(moduleName, "⚠ PowerShell not found", 0);
                    logActivity(moduleName + ": ERROR - PowerShell not found at: " + pwshExe);
                    return new KeyValuePair<string, string>(versionKey, "");
                }

                logActivity(moduleName + ": PowerShell found at: " + pwshExe);
                updateStatus(moduleName, "Installing...", 40);
                logActivity(moduleName + ": Installing module via Save-Module...");

                string modulesPath = Path.Combine(baseDir, "data", "lib", "pwsh", "Modules");
                Directory.CreateDirectory(modulesPath);
                logActivity(moduleName + ": Modules path: " + modulesPath);

                // Install module
                updateStatus(moduleName, "Downloading...", 50);
                logActivity(moduleName + ": Executing Save-Module command...");

                var psi = new ProcessStartInfo
                {
                    FileName = pwshExe,
                    Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command \"Save-Module -Name " + moduleName + " -Repository PSGallery -Path '" + modulesPath + "' -Force -Confirm:$false -ErrorAction Stop\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(psi))
                {
                    // Read output streams asynchronously to prevent deadlock
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    // Simulate progress during download (runs in background)
                    var progressTask = Task.Run(async () =>
                    {
                        int progress = 55;
                        while (!process.HasExited && progress < 85)
                        {
                            await Task.Delay(5000); // Update every 5 seconds to reduce log spam
                            progress += 3;
                            if (!process.HasExited)
                            {
                                updateStatus(moduleName, "Downloading...", Math.Min(progress, 85));
                                // Only log at significant milestones to reduce clutter
                                if (progress % 15 == 0 || progress >= 70)
                                {
                                    logActivity(moduleName + ": Still downloading... (" + progress + "%)");
                                }
                            }
                        }
                    });

                    // Wait for process to complete with timeout
                    logActivity(moduleName + ": Waiting for Save-Module to complete (5 min timeout)...");

                    // Use proper async wait with timeout
                    var timeoutTask = Task.Delay(300000); // 5 min timeout
                    var processTask = Task.Run(() => process.WaitForExit());
                    var completedTask = await Task.WhenAny(processTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        logActivity(moduleName + ": Timeout - killing process");
                        try { process.Kill(); } catch { }
                        updateStatus(moduleName, "⚠ Timeout", 0);
                        return new KeyValuePair<string, string>(versionKey, "");
                    }

                    // Ensure process has fully exited
                    if (!process.HasExited)
                    {
                        process.WaitForExit(); // Wait without timeout for full exit
                    }

                    string output = await outputTask;
                    string error = await errorTask;

                    try { await progressTask; } catch { } // Wait for progress task to complete

                    logActivity(moduleName + ": Save-Module completed with exit code: " + process.ExitCode);

                    if (process.ExitCode != 0)
                    {
                        logActivity(moduleName + ": Save-Module failed with exit code " + process.ExitCode);
                        if (!string.IsNullOrEmpty(error))
                            logActivity(moduleName + ": Error output: " + error.Trim());
                        if (!string.IsNullOrEmpty(output))
                            logActivity(moduleName + ": Standard output: " + output.Trim());
                        throw new InvalidOperationException(moduleName + " Save-Module failed with exit code " + process.ExitCode + ".");
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(output))
                            logActivity(moduleName + ": Output: " + output.Trim());
                    }
                }

                updateStatus(moduleName, "Verifying...", 90);

                // Wait a moment for file system to sync
                await Task.Delay(500);

                string moduleDir = Path.Combine(modulesPath, moduleName);
                logActivity(moduleName + ": Checking module directory: " + moduleDir);

                if (Directory.Exists(moduleDir))
                {
                    var versionDirs = Directory.GetDirectories(moduleDir);
                    logActivity(moduleName + ": Found " + versionDirs.Length + " version directories");

                    version = versionDirs
                        .Select(d => Path.GetFileName(d))
                        .OrderByDescending(ParseModuleVersion)
                        .FirstOrDefault() ?? "";

                    if (!string.IsNullOrEmpty(version))
                    {
                        updateStatus(moduleName, "✓ Completed", 100);
                        logActivity(moduleName + ": Successfully installed version " + version);
                    }
                    else
                    {
                        updateStatus(moduleName, "⚠ Version not found", 0);
                        logActivity(moduleName + ": ERROR - No version directory found");
                    }
                }
                else
                {
                    updateStatus(moduleName, "⚠ Module not found", 0);
                    logActivity(moduleName + ": ERROR - Module directory not found: " + moduleDir);
                }
            }
            catch (Exception ex)
            {
                version = "";
                updateStatus(moduleName, "⚠ Error: " + ex.Message, 0);
                logActivity(moduleName + ": EXCEPTION - " + ex.Message);
                logActivity(moduleName + ": Stack trace: " + ex.StackTrace);
            }

            return string.IsNullOrEmpty(version)
                ? new KeyValuePair<string, string>()
                : new KeyValuePair<string, string>(versionKey, version);
        }

        private static Version ParseModuleVersion(string value)
        {
            Version parsed;
            return Version.TryParse(value, out parsed) ? parsed : new Version(0, 0);
        }
    }

    public class PythonInstaller
    {
        private readonly string baseDir;
        private readonly Action<string, string, int> updateStatus;
        private readonly Action<string> logActivity;

        public PythonInstaller(string baseDir, Action<string, string, int> updateStatus, Action<string> logActivity = null)
        {
            this.baseDir = baseDir;
            this.updateStatus = updateStatus;
            this.logActivity = logActivity ?? (_ => { });
        }

        public async Task<KeyValuePair<string, string>> UpgradeAsync(string currentVersion, string targetVersion)
        {
            const string componentName = "Python";
            string version = "";
            string dataLibDir = Path.Combine(baseDir, "data", "lib");
            string pythonDir = Path.Combine(dataLibDir, "python");
            string stagingDir = Path.Combine(dataLibDir, ".python-stage-" + Guid.NewGuid().ToString("N"));
            string backupDir = Path.Combine(dataLibDir, ".python-backup");
            string tempDir = Path.Combine(Path.GetTempPath(), "codelauncher-python-" + Guid.NewGuid().ToString("N"));

            try
            {
                Version current;
                Version requested;
                if (!Version.TryParse(currentVersion, out current))
                    throw new InvalidDataException("The installed Python version is not a valid numeric version: " + currentVersion);
                if (!Version.TryParse(targetVersion, out requested))
                    throw new InvalidDataException("The requested Python version is invalid: " + targetVersion);
                if (current.Major != requested.Major || current.Minor != requested.Minor)
                    throw new InvalidOperationException("Python updates must keep the installed major.minor version.");

                if (requested <= current)
                    return new KeyValuePair<string, string>("PYTHON_VERSION", currentVersion);

                updateStatus(componentName, "Preparing patch update...", 5);
                logActivity("Python: Updating " + currentVersion + " to " + targetVersion + "...");

                Directory.CreateDirectory(dataLibDir);
                RecoverBackup(pythonDir, backupDir);
                Directory.CreateDirectory(tempDir);

                string zipPath = Path.Combine(tempDir, "python.zip");
                string downloadUrl = "https://www.python.org/ftp/python/" + targetVersion +
                    "/python-" + targetVersion + "-embed-amd64.zip";
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(20);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("CodeLauncher/1.0");
                    await CommonHelper.DownloadFileWithRetryAsync(
                        client,
                        downloadUrl,
                        zipPath,
                        default(System.Threading.CancellationToken),
                        (downloaded, total) =>
                        {
                            if (total > 0)
                            {
                                int progress = 10 + (int)(35.0 * downloaded / total);
                                updateStatus(componentName, "Downloading...", Math.Min(progress, 45));
                            }
                        });
                }

                int shortVersion = requested.Major * 100 + requested.Minor;
                string pthFileName = "python" + shortVersion + "._pth";
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    if (archive.GetEntry("python.exe") == null || archive.GetEntry(pthFileName) == null)
                        throw new InvalidDataException("The Python embeddable archive is missing required runtime files.");
                }

                if (Directory.Exists(stagingDir))
                    Directory.Delete(stagingDir, true);
                Directory.CreateDirectory(stagingDir);
                await CommonHelper.ExtractZipWithProgressAsync(
                    zipPath,
                    stagingDir,
                    componentName,
                    45,
                    80,
                    progress => updateStatus(componentName, "Extracting...", progress),
                    logActivity,
                    default(System.Threading.CancellationToken));

                ConfigureEmbeddedPython(stagingDir, pthFileName);
                PreservePythonPackages(pythonDir, stagingDir);
                ValidatePythonRuntime(Path.Combine(stagingDir, "python.exe"), targetVersion);

                updateStatus(componentName, "Installing...", 92);
                CommonHelper.ReplaceDirectoryAtomically(
                    stagingDir,
                    pythonDir,
                    backupDir,
                    message => logActivity("Python: " + message));

                version = targetVersion;
                updateStatus(componentName, "✓ Completed", 100);
                logActivity("Python: Patch update completed - " + version);
            }
            catch (Exception ex)
            {
                version = "";
                updateStatus(componentName, "⚠ Error: " + ex.Message, 0);
                logActivity("Python: Update failed - " + ex.Message);
                if (Directory.Exists(stagingDir))
                {
                    try { Directory.Delete(stagingDir, true); }
                    catch (IOException cleanupError) { logActivity("Python: Staging cleanup warning - " + cleanupError.Message); }
                    catch (UnauthorizedAccessException cleanupError) { logActivity("Python: Staging cleanup warning - " + cleanupError.Message); }
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); }
                    catch (IOException ex) { logActivity("Python: Temporary download cleanup warning - " + ex.Message); }
                    catch (UnauthorizedAccessException ex) { logActivity("Python: Temporary download cleanup warning - " + ex.Message); }
                }
            }

            return string.IsNullOrEmpty(version)
                ? new KeyValuePair<string, string>()
                : new KeyValuePair<string, string>("PYTHON_VERSION", version);
        }

        private static void RecoverBackup(string pythonDir, string backupDir)
        {
            CommonHelper.RecoverDirectoryReplacement(pythonDir, backupDir);
        }

        private static void ConfigureEmbeddedPython(string stagingDir, string pthFileName)
        {
            string pthPath = Path.Combine(stagingDir, pthFileName);
            string content = File.ReadAllText(pthPath, Encoding.UTF8);
            if (content.Length > 0 && content[0] == '\uFEFF')
                content = content.Substring(1);

            content = Regex.Replace(
                content,
                @"(?m)^#\s*import\s+site\s*$",
                "import site",
                RegexOptions.IgnoreCase);
            File.WriteAllText(pthPath, content, new UTF8Encoding(false));
        }

        private static void PreservePythonPackages(string pythonDir, string stagingDir)
        {
            string[] preservedDirectories =
            {
                Path.Combine("Lib", "site-packages"),
                "Scripts"
            };

            foreach (string relativePath in preservedDirectories)
            {
                string source = Path.Combine(pythonDir, relativePath);
                if (Directory.Exists(source))
                    CommonHelper.CopyDirectoryRecursive(source, Path.Combine(stagingDir, relativePath), true);
            }

            string pipCommand = Path.Combine(pythonDir, "pip.cmd");
            if (File.Exists(pipCommand))
                File.Copy(pipCommand, Path.Combine(stagingDir, "pip.cmd"), true);
        }

        private static void ValidatePythonRuntime(string pythonExe, string expectedVersion)
        {
            if (!File.Exists(pythonExe) || new FileInfo(pythonExe).Length == 0)
                throw new InvalidDataException("The staged Python executable is missing or empty.");

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = "--version",
                WorkingDirectory = Path.GetDirectoryName(pythonExe),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(10000))
                {
                    process.Kill();
                    throw new TimeoutException("The staged Python runtime did not start within 10 seconds.");
                }
                if (process.ExitCode != 0)
                    throw new InvalidDataException("The staged Python runtime failed validation: " + (error + " " + output).Trim());

                var match = Regex.Match(output + " " + error, @"Python\s+(\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);
                if (!match.Success || !string.Equals(match.Groups[1].Value, expectedVersion, StringComparison.Ordinal))
                    throw new InvalidDataException("The staged Python runtime version did not match " + expectedVersion + ".");
            }
        }
    }

    /// <summary>
    /// VSCode installer/upgrader
    /// </summary>
    public class VSCodeInstaller
    {
        private readonly string baseDir;
        private readonly Action<string, string, int> updateStatus;
        private readonly Action<string> logActivity;

        public VSCodeInstaller(string baseDir, Action<string, string, int> updateStatus, Action<string> logActivity = null)
        {
            this.baseDir = baseDir;
            this.updateStatus = updateStatus;
            this.logActivity = logActivity ?? (_ => { });
        }

        public static void RecoverInterruptedPortableUpdate(string baseDir)
        {
            string dataDir = Path.Combine(baseDir, "data");
            if (!Directory.Exists(dataDir))
                return;

            RecoverInterruptedUpdate(
                baseDir,
                Path.Combine(dataDir, ".codelauncher-vscode-stage"),
                Path.Combine(dataDir, ".codelauncher-vscode-backup"),
                Path.Combine(dataDir, ".codelauncher-vscode-update"));
        }

        public async Task<KeyValuePair<string, string>> UpgradeAsync(string tempBaseDir)
        {
            const string componentName = "VSCode";
            string version = "";
            string tempDir = Path.Combine(tempBaseDir, "vscode");
            string dataDir = Path.Combine(baseDir, "data");
            string stageDir = Path.Combine(dataDir, ".codelauncher-vscode-stage");
            string backupDir = Path.Combine(dataDir, ".codelauncher-vscode-backup");
            string updateMarker = Path.Combine(dataDir, ".codelauncher-vscode-update");

            try
            {
                CommonHelper.EnableTls12();
                Directory.CreateDirectory(dataDir);
                RecoverInterruptedUpdate(baseDir, stageDir, backupDir, updateMarker);
                if (!File.Exists(Path.Combine(baseDir, "Code.exe")))
                    throw new FileNotFoundException("The portable VS Code installation was not found.", Path.Combine(baseDir, "Code.exe"));

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(20);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("CodeLauncher/1.0");
                    client.DefaultRequestHeaders.ConnectionClose = false;

                    updateStatus(componentName, "Checking version...", 5);
                    logActivity("VS Code: Checking latest version...");

                    string url = "https://update.code.visualstudio.com/latest/win32-x64-archive/stable";
                    string resolvedVersion;
                    using (var request = new HttpRequestMessage(HttpMethod.Head, url))
                    using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        string resolvedUrl = response.RequestMessage.RequestUri.ToString();
                        var match = Regex.Match(resolvedUrl, @"VSCode-win32-x64-([\d\.]+)\.zip");
                        if (!match.Success)
                            throw new InvalidDataException("VS Code version was not found in the resolved download URL.");
                        resolvedVersion = match.Groups[1].Value;
                    }

                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                    Directory.CreateDirectory(tempDir);

                    string zipPath = Path.Combine(tempDir, "vscode.zip");
                    string downloadUrl = "https://update.code.visualstudio.com/" + resolvedVersion + "/win32-x64-archive/stable";
                    updateStatus(componentName, "Downloading...", 10);
                    logActivity("VS Code: Downloading version " + resolvedVersion + "...");
                    await DownloadFileAsync(client, downloadUrl, zipPath, componentName, 10, 50);
                    ValidateVSCodeArchive(zipPath);

                    if (Directory.Exists(stageDir))
                        Directory.Delete(stageDir, true);
                    Directory.CreateDirectory(stageDir);
                    updateStatus(componentName, "Extracting...", 55);
                    await ExtractZipAsync(zipPath, stageDir, componentName, 55, 90);
                    ValidateStagedVSCode(stageDir);

                    updateStatus(componentName, "Installing...", 93);
                    CommitStagedVSCode(stageDir, backupDir, updateMarker);
                    version = resolvedVersion;
                    updateStatus(componentName, "✓ Completed", 100);
                    logActivity("VS Code: Update completed successfully - " + version);
                }
            }
            catch (Exception ex)
            {
                updateStatus(componentName, "⚠ Error: " + ex.Message, 0);
                logActivity("VS Code: Error - " + ex.Message);
                version = "";
                try
                {
                    RecoverInterruptedUpdate(baseDir, stageDir, backupDir, updateMarker);
                }
                catch (Exception recoveryError)
                {
                    logActivity("VS Code: Recovery failed - " + recoveryError.Message);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); }
                    catch (IOException ex) { logActivity("VS Code: Temporary download cleanup warning - " + ex.Message); }
                    catch (UnauthorizedAccessException ex) { logActivity("VS Code: Temporary download cleanup warning - " + ex.Message); }
                }

                if (Directory.Exists(stageDir) && !File.Exists(updateMarker))
                {
                    try { Directory.Delete(stageDir, true); }
                    catch (IOException ex) { logActivity("VS Code: Staging cleanup warning - " + ex.Message); }
                    catch (UnauthorizedAccessException ex) { logActivity("VS Code: Staging cleanup warning - " + ex.Message); }
                }
            }

            return string.IsNullOrEmpty(version)
                ? new KeyValuePair<string, string>()
                : new KeyValuePair<string, string>("VSCODE_VERSION", version);
        }

        private static void ValidateVSCodeArchive(string zipPath)
        {
            if (!File.Exists(zipPath) || new FileInfo(zipPath).Length == 0)
                throw new InvalidDataException("VS Code archive download was empty.");

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                if (archive.GetEntry("Code.exe") == null)
                    throw new InvalidDataException("VS Code archive does not contain Code.exe.");
            }
        }

        private static void ValidateStagedVSCode(string stageDir)
        {
            string codeExe = Path.Combine(stageDir, "Code.exe");
            if (!File.Exists(codeExe) || new FileInfo(codeExe).Length == 0)
                throw new InvalidDataException("Staged VS Code package does not contain a valid Code.exe.");
        }

        private void CommitStagedVSCode(string stageDir, string backupDir, string updateMarker)
        {
            if (Directory.Exists(backupDir))
                Directory.Delete(backupDir, true);
            Directory.CreateDirectory(backupDir);
            WriteUpdateMarker(updateMarker, "BackingUp");

            try
            {
                foreach (string entry in Directory.GetFileSystemEntries(baseDir))
                {
                    string name = Path.GetFileName(entry);
                    if (!IsReplaceableVSCodeEntry(name))
                        continue;

                    MoveEntry(entry, Path.Combine(backupDir, name));
                }

                WriteUpdateMarker(updateMarker, "Installing");
                foreach (string entry in Directory.GetFileSystemEntries(stageDir))
                {
                    string name = Path.GetFileName(entry);
                    if (!IsReplaceableVSCodeEntry(name))
                        continue;

                    MoveEntry(entry, Path.Combine(baseDir, name));
                }

                ValidateStagedVSCode(baseDir);
                WriteUpdateMarker(updateMarker, "Committed");
                try { File.Delete(updateMarker); }
                catch (IOException ex) { logActivity("VS Code: Update marker cleanup warning - " + ex.Message); }
                catch (UnauthorizedAccessException ex) { logActivity("VS Code: Update marker cleanup warning - " + ex.Message); }
            }
            catch (Exception commitError)
            {
                try
                {
                    RecoverInterruptedUpdate(baseDir, stageDir, backupDir, updateMarker);
                }
                catch (Exception rollbackError)
                {
                    throw new AggregateException("VS Code update failed and rollback also failed.", commitError, rollbackError);
                }

                throw;
            }

            if (Directory.Exists(backupDir))
            {
                try { Directory.Delete(backupDir, true); }
                catch (IOException ex) { logActivity("VS Code: Old file cleanup warning - " + ex.Message); }
                catch (UnauthorizedAccessException ex) { logActivity("VS Code: Old file cleanup warning - " + ex.Message); }
            }
        }

        private static void RecoverInterruptedUpdate(string baseDir, string stageDir, string backupDir, string updateMarker)
        {
            string phase = File.Exists(updateMarker) ? File.ReadAllText(updateMarker).Trim() : string.Empty;
            bool backupExists = Directory.Exists(backupDir);

            if (string.Equals(phase, "Installing", StringComparison.OrdinalIgnoreCase) && !backupExists)
                throw new InvalidOperationException("The previous VS Code update backup is missing.");
            if (string.Equals(phase, "BackingUp", StringComparison.OrdinalIgnoreCase) &&
                !backupExists &&
                !File.Exists(Path.Combine(baseDir, "Code.exe")))
            {
                throw new InvalidOperationException("The previous VS Code update has no usable backup.");
            }

            if (string.Equals(phase, "Installing", StringComparison.OrdinalIgnoreCase))
                DeleteReplaceableVSCodeEntries(baseDir);
            else if (!string.IsNullOrEmpty(phase) &&
                     !string.Equals(phase, "BackingUp", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(phase, "Committed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unknown VS Code update recovery state: " + phase);

            bool shouldRestoreBackup =
                string.Equals(phase, "BackingUp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(phase, "Installing", StringComparison.OrdinalIgnoreCase) ||
                (string.IsNullOrEmpty(phase) && !File.Exists(Path.Combine(baseDir, "Code.exe")));

            if (backupExists && shouldRestoreBackup)
            {
                foreach (string backupEntry in Directory.GetFileSystemEntries(backupDir))
                {
                    string targetPath = Path.Combine(baseDir, Path.GetFileName(backupEntry));
                    DeletePathIfExists(targetPath);
                    MoveEntry(backupEntry, targetPath);
                }
            }
            else if (backupExists)
            {
                Directory.Delete(backupDir, true);
            }
            if (Directory.Exists(backupDir))
                Directory.Delete(backupDir, true);
            if (File.Exists(updateMarker))
                File.Delete(updateMarker);
            if (Directory.Exists(stageDir))
                Directory.Delete(stageDir, true);
        }

        private static bool IsReplaceableVSCodeEntry(string name)
        {
            return !name.Equals("data", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("launcher.exe", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("CodeLauncher.exe", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals(CommonHelper.RuntimeInfoFileName, StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals(CommonHelper.RuntimeLogFileName, StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("pvs.info", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("pvs.log", StringComparison.OrdinalIgnoreCase) &&
                   !name.Equals("upgrade.log", StringComparison.OrdinalIgnoreCase) &&
                   !name.StartsWith("upgrade_", StringComparison.OrdinalIgnoreCase);
        }

        private static void DeleteReplaceableVSCodeEntries(string directory)
        {
            foreach (string entry in Directory.GetFileSystemEntries(directory))
            {
                if (IsReplaceableVSCodeEntry(Path.GetFileName(entry)))
                    DeletePathIfExists(entry);
            }
        }

        private static void DeletePathIfExists(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            else if (File.Exists(path))
                File.Delete(path);
        }

        private static void MoveEntry(string sourcePath, string targetPath)
        {
            if (Directory.Exists(sourcePath))
                Directory.Move(sourcePath, targetPath);
            else
                File.Move(sourcePath, targetPath);
        }

        private static void WriteUpdateMarker(string markerPath, string phase)
        {
            string tempPath = markerPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tempPath, phase);
            if (File.Exists(markerPath))
                File.Replace(tempPath, markerPath, null);
            else
                File.Move(tempPath, markerPath);
        }

        private async Task DownloadFileAsync(HttpClient client, string url, string destPath, string componentName, int startProgress, int endProgress)
        {
            int lastProgress = startProgress;
            await CommonHelper.DownloadFileWithRetryAsync(
                client,
                url,
                destPath,
                default(System.Threading.CancellationToken),
                (downloaded, total) =>
                {
                    if (total <= 0)
                        return;
                    int progress = startProgress + (int)((endProgress - startProgress) * downloaded / total);
                    if (progress > lastProgress)
                    {
                        updateStatus(componentName, "Downloading...", Math.Min(progress, endProgress));
                        lastProgress = progress;
                    }
                });
        }

        private async Task ExtractZipAsync(string zipPath, string extractPath, string componentName, int startProgress, int endProgress)
        {
            await CommonHelper.ExtractZipWithProgressAsync(
                zipPath,
                extractPath,
                componentName,
                startProgress,
                endProgress,
                progress => updateStatus(componentName, "Extracting...", progress),
                message => logActivity(message),
                default(System.Threading.CancellationToken));
        }
    }

    /// <summary>
    /// Font manager for registry-based font installation
    /// </summary>
    public class FontManager
    {
        [DllImport("gdi32.dll")]
        private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

        private const int WM_FONTCHANGE = 0x001D;
        private const int HWND_BROADCAST = 0xffff;
        private const uint SMTO_ABORTIFHUNG = 0x0002;
        public static bool EnsureFontInstalled(string fontDir, string fontFile, string fontName)
        {
            string fontPath = Path.Combine(fontDir, fontFile);
            if (!File.Exists(fontPath))
                return false;

            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts"))
                {
                    if (key == null)
                        return false;

                    string registeredPath = key.GetValue(fontName) as string;
                    if (string.Equals(registeredPath, fontPath, StringComparison.OrdinalIgnoreCase))
                        return true;

                    key.SetValue(fontName, fontPath, RegistryValueKind.String);
                }

                AddFontResourceEx(fontPath, 0, IntPtr.Zero);
                IntPtr result;
                SendMessageTimeout(new IntPtr(HWND_BROADCAST), WM_FONTCHANGE, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 2000, out result);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Shortcut manager for creating/updating Windows shortcuts
    /// </summary>
    public class ShortcutManager
    {
        public static bool CreateOrUpdateShortcut(string folder, string name, string targetPath, string iconPath, string workingDir, string arguments = "--from-shortcut", bool forceUpdate = false)
        {
            object shell = null;
            object shortcut = null;
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(targetPath))
                    return false;

                Directory.CreateDirectory(folder);
                string shortcutPath = Path.Combine(folder, name + ".lnk");
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                    return false;

                shell = Activator.CreateInstance(shellType);
                dynamic shellObject = shell;
                shortcut = shellObject.CreateShortcut(shortcutPath);
                dynamic shortcutObject = shortcut;

                if (!forceUpdate && File.Exists(shortcutPath))
                {
                    try
                    {
                        string currentTarget = shortcutObject.TargetPath as string;
                        int currentWindowStyle = Convert.ToInt32(shortcutObject.WindowStyle);
                        string currentWorkingDir = shortcutObject.WorkingDirectory as string;
                        if (PathsEqual(currentTarget, targetPath) &&
                            PathsEqual(currentWorkingDir, workingDir) &&
                            currentWindowStyle == 1)
                            return true;
                    }
                    catch (Exception)
                    {
                    }
                }

                shortcutObject.TargetPath = targetPath;
                shortcutObject.Arguments = arguments ?? string.Empty;
                shortcutObject.WorkingDirectory = workingDir ?? string.Empty;
                shortcutObject.IconLocation = iconPath ?? string.Empty;
                shortcutObject.WindowStyle = 1;
                shortcutObject.Save();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut))
                {
                    try { Marshal.ReleaseComObject(shortcut); }
                    catch (System.Runtime.InteropServices.COMException) { }
                }
                if (shell != null && Marshal.IsComObject(shell))
                {
                    try { Marshal.ReleaseComObject(shell); }
                    catch (System.Runtime.InteropServices.COMException) { }
                }
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            try
            {
                return string.Equals(
                    Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        public static bool CreateShortcuts(string baseDir, string launcherPath, string iconPath)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            string shortcutName = "VSCode (Portable)";

            bool desktopReady = CreateOrUpdateShortcut(desktop, shortcutName, launcherPath, iconPath, baseDir);
            bool startMenuReady = CreateOrUpdateShortcut(startMenu, shortcutName, launcherPath, iconPath, baseDir);
            return desktopReady && startMenuReady;
        }
    }
}
