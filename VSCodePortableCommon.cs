using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VSCodePortableCommon
{
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

            try
            {
                updateStatus(componentName, "Checking version...", 5);
                logActivity("PowerShell 7: Checking latest version...");

                CommonHelper.EnableTls12();

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "VSCode-Portable-Installer");
                    client.DefaultRequestHeaders.ConnectionClose = false; // Keep-Alive
                    client.Timeout = TimeSpan.FromMinutes(10);

                    string url = "";

                    try
                    {
                        // Use GitHub redirect (no API rate limit) to get latest version
                        var request = new HttpRequestMessage(HttpMethod.Head, "https://github.com/PowerShell/PowerShell/releases/latest");
                        request.Headers.Add("User-Agent", "VSCode-Portable-Installer");

                        var response = await client.SendAsync(request);

                        // Extract version from redirect URL: /releases/tag/v7.5.4
                        string redirectUrl = response.RequestMessage.RequestUri.ToString();
                        var versionMatch = Regex.Match(redirectUrl, @"/releases/tag/v([\d\.]+)");

                        if (versionMatch.Success)
                        {
                            version = versionMatch.Groups[1].Value;
                            url = "https://github.com/PowerShell/PowerShell/releases/download/v" + version + "/PowerShell-" + version + "-win-x64.zip";
                            logActivity("PowerShell 7: Found latest version " + version + " (via redirect)");
                        }
                        else
                        {
                            throw new Exception("Version not found in redirect URL: " + redirectUrl);
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

                    // Verify download
                    if (!File.Exists(zipPath))
                    {
                        throw new Exception("Download failed - file not created");
                    }

                    var zipInfo = new FileInfo(zipPath);
                    if (zipInfo.Length < 50 * 1024 * 1024) // Should be > 50MB
                    {
                        throw new Exception("Download failed - file too small: " + zipInfo.Length + " bytes");
                    }
                    logActivity("PowerShell 7: Download verified (" + (zipInfo.Length / 1024 / 1024) + " MB)");

                    updateStatus(componentName, "Extracting...", 45);
                    string pwshDir = Path.Combine(baseDir, "data", "lib", "pwsh");
                    string tempBackup = Path.Combine(tempDir, "backup");

                    if (Directory.Exists(pwshDir) && !preserveExisting)
                    {
                        logActivity("PowerShell 7: Deleting existing directory: " + pwshDir);
                        Directory.Delete(pwshDir, true);
                    }
                    else if (preserveExisting && Directory.Exists(pwshDir))
                    {
                        logActivity("PowerShell 7: Preserving existing components...");
                        // Preserve specific directories and files during upgrade
                        var preserveDirs = new[] { "bin", "ohmyposh" };
                        var preserveFiles = new[] { "Microsoft.PowerShell_profile.ps1" };
                        var preserveModules = new[] { "Terminal-Icons", "PSFzf", "modern-unix-win" };

                        Directory.CreateDirectory(tempBackup);

                        foreach (var dir in preserveDirs)
                        {
                            string srcDir = Path.Combine(pwshDir, dir);
                            if (Directory.Exists(srcDir))
                            {
                                CommonHelper.CopyDirectoryRecursive(srcDir, Path.Combine(tempBackup, dir));
                            }
                        }

                        foreach (var file in preserveFiles)
                        {
                            string srcFile = Path.Combine(pwshDir, file);
                            if (File.Exists(srcFile))
                            {
                                File.Copy(srcFile, Path.Combine(tempBackup, file), true);
                            }
                        }

                        string modulesDir = Path.Combine(pwshDir, "Modules");
                        if (Directory.Exists(modulesDir))
                        {
                            foreach (var module in preserveModules)
                            {
                                string moduleDir = Path.Combine(modulesDir, module);
                                if (Directory.Exists(moduleDir))
                                {
                                    CommonHelper.CopyDirectoryRecursive(moduleDir, Path.Combine(tempBackup, "Modules", module));
                                }
                            }
                        }

                        Directory.Delete(pwshDir, true);
                    }

                    Directory.CreateDirectory(pwshDir);
                    logActivity("PowerShell 7: Created directory: " + pwshDir);
                    logActivity("PowerShell 7: Extracting directly to target directory...");

                    // Extract directly to target directory (no intermediate copy)
                    await ExtractZipAsync(zipPath, pwshDir, componentName, 45, 58);
                    logActivity("PowerShell 7: Core archive extracted");

                    updateStatus(componentName, "Verifying...", 58);

                    // Verify installation
                    string pwshExePath = Path.Combine(pwshDir, "pwsh.exe");
                    if (File.Exists(pwshExePath))
                    {
                        logActivity("PowerShell 7: pwsh.exe extracted successfully");
                    }
                    else
                    {
                        logActivity("PowerShell 7: WARNING - pwsh.exe not found after extraction!");
                    }

                    // Restore preserved items if upgrade
                    if (preserveExisting && Directory.Exists(tempBackup))
                    {
                        logActivity("PowerShell 7: Restoring preserved components...");
                        CommonHelper.CopyDirectoryRecursive(tempBackup, pwshDir, true);
                    }

                    logActivity("PowerShell 7: Unblocking files...");

                    // Unblock all files in PowerShell directory (batch processing)
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell",
                            Arguments = "-NoProfile -Command \"& {Get-ChildItem -Path '" + pwshDir + "' -Recurse -File | ForEach-Object {try {Unblock-File -Path $_.FullName -ErrorAction SilentlyContinue} catch {}}}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        };
                        using (var process = Process.Start(psi))
                        {
                            process.WaitForExit(60000); // 60초로 증가 (배치 처리에 더 많은 시간 필요)
                        }
                    }
                    catch (Exception ex)
                    {
                        logActivity("PowerShell 7: Unblock warning - " + ex.Message);
                    }

                    updateStatus(componentName, "Cleaning up...", 92);
                    var localeDirs = new[] { "cs", "de", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant" };
                    foreach (var locale in localeDirs)
                    {
                        string localeDir = Path.Combine(pwshDir, locale);
                        if (Directory.Exists(localeDir))
                            Directory.Delete(localeDir, true);
                    }

                    Directory.Delete(tempDir, true);
                    updateStatus(componentName, preserveExisting ? "✓ Completed" : "Core installed", preserveExisting ? 100 : 60);
                }
            }
            catch (Exception ex)
            {
                updateStatus(componentName, "⚠ Error: " + ex.Message, 0);
                logActivity("PowerShell 7: Error - " + ex.Message);
            }

            return new KeyValuePair<string, string>("PWSH7_VERSION", version);
        }

        private async Task DownloadFileAsync(HttpClient client, string url, string destPath, string componentName, int startProgress, int endProgress)
        {
            // Single attempt - no retry loop
            // If it fails (503, etc.), let caller handle retry logic
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, CommonHelper.BUFFER_SIZE, true))
            {
                byte[] buffer = new byte[CommonHelper.BUFFER_SIZE];
                long downloadedBytes = 0;
                int bytesRead;
                int lastProgress = startProgress;
                int lastLoggedPercentage = 0;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    // Update progress only when it changes by at least 1% to reduce UI overhead
                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        int progressPercentage = (int)(downloadedBytes * 100 / totalBytes.Value);
                        int progress = startProgress + (int)((endProgress - startProgress) * downloadedBytes / totalBytes.Value);
                        if (progress > lastProgress)
                        {
                            updateStatus(componentName, "Downloading...", progress);
                            lastProgress = progress;
                            
                            // Log at 10% intervals (performance: direct log, no UI overhead)
                            if (progressPercentage % 10 == 0 && progressPercentage != lastLoggedPercentage && progressPercentage > 0)
                            {
                                double downloadedMB = downloadedBytes / 1048576.0;
                                double totalMB = totalBytes.Value / 1048576.0;
                                logActivity(string.Format("{0}: Downloading... {1}% ({2:F1}/{3:F1} MB)", 
                                    componentName, progressPercentage, downloadedMB, totalMB));
                                lastLoggedPercentage = progressPercentage;
                            }
                        }
                    }
                }
            }
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
                        var request = new HttpRequestMessage(HttpMethod.Head, "https://github.com/JanDeDobbeleer/oh-my-posh/releases/latest");
                        request.Headers.Add("User-Agent", "VSCode-Portable-Installer");

                        var response = await client.SendAsync(request);

                        // Extract version from redirect URL: /releases/tag/v27.6.0
                        string redirectUrl = response.RequestMessage.RequestUri.ToString();
                        var versionMatch = Regex.Match(redirectUrl, @"/releases/tag/v([\d\.]+)");

                        if (versionMatch.Success)
                        {
                            version = versionMatch.Groups[1].Value;
                            downloadUrl = "https://github.com/JanDeDobbeleer/oh-my-posh/releases/download/v" + version + "/install-x64.msix";
                            logActivity("Oh My Posh: Found latest version " + version + " (via redirect)");
                        }
                        else
                        {
                            throw new Exception("Version not found in redirect URL: " + redirectUrl);
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

                string tempDir = Path.Combine(tempBaseDir, "ohmyposh");
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                string msixPath = Path.Combine(tempDir, "ohmyposh.msix");

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(10);
                    client.DefaultRequestHeaders.ConnectionClose = false; // Keep-Alive
                    var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(msixPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                }

                logActivity("Oh My Posh: Download completed");
                updateStatus(componentName, "Extracting...", 55);
                logActivity("Oh My Posh: Extracting...");

                string zipPath = Path.Combine(tempDir, "ohmyposh.zip");
                File.Move(msixPath, zipPath);

                string extractDir = Path.Combine(tempDir, "ohmyposh_extract");
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                updateStatus(componentName, "Installing...", 75);

                var exeFiles = Directory.GetFiles(extractDir, "oh-my-posh.exe", SearchOption.AllDirectories);
                if (exeFiles.Length > 0)
                {
                    updateStatus(componentName, "Replacing executable...", 85);
                    string pwshBin = Path.Combine(baseDir, "data", "lib", "pwsh", "bin");
                    Directory.CreateDirectory(pwshBin);

                    logActivity("Oh My Posh: Replacing executable...");

                    foreach (var oldFile in Directory.GetFiles(pwshBin, "oh-my-posh*"))
                    {
                        try { File.Delete(oldFile); } catch { }
                    }

                    string targetExe = Path.Combine(pwshBin, "oh-my-posh.exe");
                    File.Copy(exeFiles[0], targetExe, true);

                    updateStatus(componentName, "✓ Completed", 100);
                    logActivity("Oh My Posh: Installed " + version);
                }
                else
                {
                    updateStatus(componentName, "⚠ Executable not found", 0);
                    logActivity("Oh My Posh: Executable not found");
                }

                Directory.Delete(tempDir, true);
            }
            catch (Exception ex)
            {
                updateStatus(componentName, "⚠ Error: " + ex.Message, 0);
                logActivity("Oh My Posh: Error - " + ex.Message);
            }

            return new KeyValuePair<string, string>("OHMYPOSH_VERSION", version);
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
        private static readonly object trustStateLock = new object();
        private static readonly Dictionary<string, Task<bool>> trustTasks = new Dictionary<string, Task<bool>>(StringComparer.OrdinalIgnoreCase);

        public ModuleInstaller(string baseDir, Action<string, string, int> updateStatus, Action<string> logActivity = null)
        {
            this.baseDir = baseDir;
            this.updateStatus = updateStatus;
            this.logActivity = logActivity ?? (_ => { });
        }

        private Task<bool> EnsurePSGalleryTrustedAsync(string pwshExe)
        {
            Task<bool> trustTask;

            lock (trustStateLock)
            {
                if (!trustTasks.TryGetValue(pwshExe, out trustTask))
                {
                    trustTask = EnsurePSGalleryTrustedCoreAsync(pwshExe);
                    trustTasks[pwshExe] = trustTask;
                }
            }

            return trustTask;
        }

        private async Task<bool> EnsurePSGalleryTrustedCoreAsync(string pwshExe)
        {
            try
            {
                logActivity("PowerShell Gallery: Setting PSGallery as trusted...");

                var trustPsi = new ProcessStartInfo
                {
                    FileName = pwshExe,
                    Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command \"Set-PSRepository PSGallery -InstallationPolicy Trusted\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var trustProcess = Process.Start(trustPsi))
                {
                    var trustOutput = await trustProcess.StandardOutput.ReadToEndAsync();
                    var trustError = await trustProcess.StandardError.ReadToEndAsync();
                    await Task.Run(() => trustProcess.WaitForExit(30000));

                    if (trustProcess.ExitCode != 0)
                    {
                        logActivity("PowerShell Gallery: Set-PSRepository warning (exit " + trustProcess.ExitCode + ")");
                        if (!string.IsNullOrEmpty(trustError))
                            logActivity("PowerShell Gallery: " + trustError.Trim());
                        return false;
                    }

                    if (!string.IsNullOrEmpty(trustOutput))
                        logActivity("PowerShell Gallery: " + trustOutput.Trim());
                }

                logActivity("PowerShell Gallery: PSGallery trust ready");
                return true;
            }
            catch (Exception ex)
            {
                logActivity("PowerShell Gallery: Trust setup warning - " + ex.Message);
                return false;
            }
        }

        public async Task<KeyValuePair<string, string>> InstallModuleAsync(string moduleName, string versionKey, int maxWaitSeconds = 30)
        {
            string version = "";
            string modulesPath = Path.Combine(baseDir, "data", "lib", "pwsh", "Modules");
            string tempZip = Path.Combine(Path.GetTempPath(), moduleName + "_" + Guid.NewGuid().ToString().Substring(0, 8) + ".zip");
            string extractPath = Path.Combine(Path.GetTempPath(), moduleName + "_extract_" + Guid.NewGuid().ToString().Substring(0, 8));

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
                        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko)");
                        var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();

                        using (var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await response.Content.CopyToAsync(fs);
                        }
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
                            version = "1.0.0"; // Fallback
                        }
                    }
                    else
                    {
                        version = "1.0.0";
                    }
                }

                string finalModuleDir = Path.Combine(modulesPath, moduleName, version);
                bool installSucceeded = false;
                try
                {
                    if (Directory.Exists(finalModuleDir))
                        Directory.Delete(finalModuleDir, true);
                    Directory.CreateDirectory(finalModuleDir);

                    // Move all files to final path
                    int prefixLength = extractPath.Length;
                    if (!extractPath.EndsWith("\\") && !extractPath.EndsWith("/"))
                        prefixLength++;
                        
                    foreach (string dirPath in Directory.GetDirectories(extractPath, "*", SearchOption.AllDirectories))
                        Directory.CreateDirectory(Path.Combine(finalModuleDir, dirPath.Substring(prefixLength)));
                    foreach (string newPath in Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories))
                        File.Copy(newPath, Path.Combine(finalModuleDir, newPath.Substring(prefixLength)), true);

                    // Cleanup temp files
                    Directory.Delete(extractPath, true);
                    File.Delete(tempZip);

                    installSucceeded = true;
                }
                catch (Exception ex)
                {
                    logActivity(moduleName + ": Moving module files failed, falling back to Save-Module: " + ex.Message);
                }

                if (!installSucceeded)
                {
                    try { if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true); } catch { }
                    try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                    return await InstallModuleLegacyAsync(moduleName, versionKey, maxWaitSeconds);
                }

                logActivity(moduleName + ": Successfully installed version " + version);
                return new KeyValuePair<string, string>(versionKey, version);
            }
            catch (Exception ex)
            {
                logActivity(moduleName + ": Installation failed - " + ex.Message);
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

                bool trustReady = await EnsurePSGalleryTrustedAsync(pwshExe);
                if (!trustReady)
                    logActivity(moduleName + ": Proceeding without PSGallery trust confirmation");

                // Install module
                updateStatus(moduleName, "Downloading...", 50);
                logActivity(moduleName + ": Executing Save-Module command...");

                var psi = new ProcessStartInfo
                {
                    FileName = pwshExe,
                    Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command \"Save-Module -Name " + moduleName + " -Path '" + modulesPath + "' -Force -ErrorAction Stop\"",
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
                        .OrderByDescending(v => v)
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
                updateStatus(moduleName, "⚠ Error: " + ex.Message, 0);
                logActivity(moduleName + ": EXCEPTION - " + ex.Message);
                logActivity(moduleName + ": Stack trace: " + ex.StackTrace);
            }

            return new KeyValuePair<string, string>(versionKey, version);
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

        public async Task<KeyValuePair<string, string>> UpgradeAsync(string tempBaseDir)
        {
            string componentName = "VSCode";
            string version = "";

            try
            {
                updateStatus(componentName, "Checking version...", 5);
                logActivity("VSCode: Checking latest version...");

                CommonHelper.EnableTls12();

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(20);
                    client.DefaultRequestHeaders.Add("User-Agent", "VSCode-Portable-Installer");
                    client.DefaultRequestHeaders.ConnectionClose = false;

                    // Get latest version via redirect
                    var request = new HttpRequestMessage(HttpMethod.Head, "https://update.code.visualstudio.com/latest/win32-x64-archive/stable");
                    var response = await client.SendAsync(request);

                    string redirectUrl = response.RequestMessage.RequestUri.ToString();
                    var match = Regex.Match(redirectUrl, @"VSCode-win32-x64-([\d\.]+)\.zip");

                    if (!match.Success)
                    {
                        updateStatus(componentName, "⚠ Version not found", 0);
                        logActivity("VSCode: Version not found in redirect URL");
                        return new KeyValuePair<string, string>();
                    }

                    version = match.Groups[1].Value;
                    logActivity("VSCode: Found version " + version);

                    string url = "https://update.code.visualstudio.com/" + version + "/win32-x64-archive/stable";

                    updateStatus(componentName, "Downloading...", 10);
                    logActivity("VSCode: Downloading version " + version + "...");

                    string tempDir = Path.Combine(tempBaseDir, "vscode");
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                    Directory.CreateDirectory(tempDir);

                    string zipPath = Path.Combine(tempDir, "vscode.zip");

                    // Download
                    await DownloadFileAsync(client, url, zipPath, componentName, 10, 55);

                    logActivity("VSCode: Download completed");
                    updateStatus(componentName, "Cleaning...", 60);
                    logActivity("VSCode: Removing old files...");

                    // Remove old VSCode files (preserve only data directory, pvs.info, and upgrade.log)
                    var excludeDirs = new[] { "data" };
                    var excludeFiles = new[] { "pvs.info", "upgrade.log" };

                    foreach (var dir in Directory.GetDirectories(baseDir))
                    {
                        string dirName = Path.GetFileName(dir);
                        if (!excludeDirs.Contains(dirName))
                        {
                            try { Directory.Delete(dir, true); } catch { }
                        }
                    }

                    foreach (var file in Directory.GetFiles(baseDir))
                    {
                        string fileName = Path.GetFileName(file);
                        if (!excludeFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }

                    updateStatus(componentName, "Extracting...", 70);
                    logActivity("VSCode: Extracting to PVS directory...");

                    // Extract directly to PVS directory
                    await ExtractZipAsync(zipPath, baseDir, componentName, 70, 95);
                    logActivity("VSCode: Core archive extracted");

                    updateStatus(componentName, "Finalizing...", 99);
                    logActivity("VSCode: Skipping recursive unblock (streamed extraction produces local files)");

                    // Cleanup
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);

                    updateStatus(componentName, "✓ Completed", 100);
                    logActivity("VSCode: Upgrade completed successfully");
                }
            }
            catch (Exception ex)
            {
                updateStatus(componentName, "⚠ Error: " + ex.Message, 0);
                logActivity("VSCode: Error - " + ex.Message);
            }

            return new KeyValuePair<string, string>("VSCODE_VERSION", version);
        }

        private async Task DownloadFileAsync(HttpClient client, string url, string destPath, string componentName, int startProgress, int endProgress)
        {
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long? totalBytes = response.Content.Headers.ContentLength;
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, CommonHelper.BUFFER_SIZE, true))
            {
                byte[] buffer = new byte[CommonHelper.BUFFER_SIZE];
                long downloadedBytes = 0;
                int bytesRead;
                int lastProgress = startProgress;
                int lastLoggedPercentage = 0;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        int progressPercentage = (int)(downloadedBytes * 100 / totalBytes.Value);
                        int progress = startProgress + (int)((endProgress - startProgress) * downloadedBytes / totalBytes.Value);
                        if (progress > lastProgress)
                        {
                            updateStatus(componentName, "Downloading...", progress);
                            lastProgress = progress;
                            
                            // Log at 10% intervals (performance: direct log, no UI overhead)
                            if (progressPercentage % 10 == 0 && progressPercentage != lastLoggedPercentage && progressPercentage > 0)
                            {
                                double downloadedMB = downloadedBytes / 1048576.0;
                                double totalMB = totalBytes.Value / 1048576.0;
                                logActivity(string.Format("{0}: Downloading... {1}% ({2:F1}/{3:F1} MB)", 
                                    componentName, progressPercentage, downloadedMB, totalMB));
                                lastLoggedPercentage = progressPercentage;
                            }
                        }
                    }
                }
            }
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
        private static extern int SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_FONTCHANGE = 0x001D;
        private const int HWND_BROADCAST = 0xffff;
        private const uint FR_PRIVATE = 0x10;
        private const uint FR_NOT_ENUM = 0x20;

        public static void EnsureFontInstalled(string fontDir, string fontFile, string fontName)
        {
            try
            {
                string fontPath = Path.Combine(fontDir, fontFile);
                if (!File.Exists(fontPath))
                    return;

                bool fontInstalledInSystem = false;

                // Check system fonts (HKLM) - only to determine if installed system-wide
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts", false))
                    {
                        if (key != null)
                        {
                            object value = key.GetValue(fontName);
                            if (value != null)
                            {
                                fontInstalledInSystem = true;
                            }
                        }
                    }
                }
                catch { }

                // If font is installed system-wide, skip user registry update
                if (fontInstalledInSystem)
                {
                    return;
                }

                // Always update user registry to ensure VSCode can access the font
                // This is important when the path changes or after migration
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts", true))
                    {
                        if (key != null)
                        {
                            // Always set/update the registry value with current path
                            key.SetValue(fontName, fontPath, RegistryValueKind.String);
                        }
                    }
                }
                catch { }

                // Refresh font cache so VSCode can use fonts immediately
                try
                {
                    // Add font resource to current session
                    AddFontResourceEx(fontPath, FR_PRIVATE, IntPtr.Zero);

                    // Notify all windows that font list has changed
                    SendMessage(new IntPtr(HWND_BROADCAST), WM_FONTCHANGE, IntPtr.Zero, IntPtr.Zero);
                }
                catch { }
            }
            catch { }
        }
    }

    /// <summary>
    /// Shortcut manager for creating/updating Windows shortcuts
    /// </summary>
    public class ShortcutManager
    {
        public static void CreateOrUpdateShortcut(string folder, string name, string targetPath, string iconPath, string workingDir, string arguments = "--from-shortcut", bool forceUpdate = false)
        {
            try
            {
                string shortcutPath = Path.Combine(folder, name + ".lnk");
                bool needsUpdate = forceUpdate || !File.Exists(shortcutPath);

                if (!needsUpdate)
                {
                    try
                    {
                        Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                        dynamic shell = Activator.CreateInstance(shellType);
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);

                        string currentTarget = shortcut.TargetPath;
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);

                        needsUpdate = !string.Equals(currentTarget, targetPath, StringComparison.OrdinalIgnoreCase);
                    }
                    catch { needsUpdate = true; }
                }

                if (needsUpdate)
                {
                    Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                    dynamic shell = Activator.CreateInstance(shellType);
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);

                    shortcut.TargetPath = targetPath;
                    shortcut.Arguments = arguments;
                    shortcut.WorkingDirectory = workingDir;
                    shortcut.IconLocation = iconPath;
                    shortcut.WindowStyle = 7; // Minimized
                    shortcut.Save();

                    System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
                }
            }
            catch { }
        }

        public static void CreateShortcuts(string baseDir, string launcherPath, string iconPath)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            string shortcutName = "VSCode (Portable)";

            CreateOrUpdateShortcut(desktop, shortcutName, launcherPath, iconPath, baseDir);
            CreateOrUpdateShortcut(startMenu, shortcutName, launcherPath, iconPath, baseDir);
        }
    }
}
