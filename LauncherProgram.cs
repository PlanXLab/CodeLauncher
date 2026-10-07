using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using VSCodePortableCommon;

namespace VSCodePortableLauncher
{
    class Program
    {
        static readonly string[] PlatformSensitiveVSCodeExtensions = new[]
        {
            "ms-python.vscode-python-envs"
        };

        static readonly string[] OptionalAutoInstalledVSCodeExtensions = new[]
        {
            "ms-python.debugpy"
        };

        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                // --init argument for PVS reset/initialization
                if (args.Contains("--init"))
                {
                    return InitializePVS();
                }

                // Internal argument for background version check
                if (args.Contains("--check-versions-internal"))
                {
                    return CheckVersionsAsync().Result;
                }

                // Check if launched from shortcut
                bool fromShortcut = args.Contains("--from-shortcut");

                // Default: Run launcher
                return RunLauncher(fromShortcut);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message + "\n\n" + ex.StackTrace,
                    "VSCode Portable Launcher Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
        }

        static int RunLauncher(bool fromShortcut = false)
        {
            string pvsDir = AppDomain.CurrentDomain.BaseDirectory;
            string pvsInfo = Path.Combine(pvsDir, "pvs.info");

            // Step 1: Check if launched from shortcut
            bool runtimeContextChanged = false;
            if (!fromShortcut)
            {
                // Step 2: Check if path, machine, or user context changed
                runtimeContextChanged = CheckRuntimeMigration(pvsDir, pvsInfo);

                if (runtimeContextChanged)
                {
                    // Step 3: Path or PC changed - Update shortcuts and fonts (background)
                    ManageShortcuts(pvsDir, true);
                    EnsureFonts(pvsDir);
                }
            }

            // Step 4: Check for pending upgrades
            var upgradeFlags = new[] {
                "upgrade_pwsh7", "upgrade_ohmyposh", "upgrade_term_icons",
                "upgrade_psfzf", "upgrade_modern_unix", "upgrade_vscode"
            };

            var pendingUpgrades = upgradeFlags.Where(flag => File.Exists(Path.Combine(pvsDir, flag))).ToList();

            if (pendingUpgrades.Any())
            {
                // Step 5: Show upgrade form
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var form = new UpgradeForm(pvsDir, pendingUpgrades);
                Application.Run(form);

                // After form closes, launch VSCode and check versions in background
                // (both Cancel and Upgrade paths)
                LaunchVSCodeAndCheckVersions(pvsDir);
                return 0;
            }

            // Step 6: No upgrades - Launch VSCode and check versions in background
            LaunchVSCodeAndCheckVersions(pvsDir);

            return 0;
        }

        static void LaunchVSCodeAndCheckVersions(string pvsDir)
        {
            // Launch VSCode
            LaunchVSCode(pvsDir);

            // Start background version check
            string launcherExe = Path.Combine(pvsDir, "launcher.exe");
            if (File.Exists(launcherExe))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = launcherExe,
                    Arguments = "--check-versions-internal",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
            }
        }

        static void LaunchVSCode(string pvsDir)
        {
            string codeExe = Path.Combine(pvsDir, "Code.exe");
            if (!File.Exists(codeExe))
            {
                MessageBox.Show("Code.exe not found. Please run installer.exe to install.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            Process.Start(CreateVSCodeStartInfo(pvsDir));
        }

        static ProcessStartInfo CreateVSCodeStartInfo(string pvsDir)
        {
            string codeExe = Path.Combine(pvsDir, "Code.exe");
            string extensionsDir = Path.Combine(pvsDir, "data", "extensions");
            string userDataDir = Path.Combine(pvsDir, "data", "user-data");

            var psi = new ProcessStartInfo
            {
                FileName = codeExe,
                Arguments = "--extensions-dir \"" + extensionsDir + "\" --user-data-dir \"" + userDataDir + "\"",
                WorkingDirectory = pvsDir,
                UseShellExecute = false
            };

            ApplyPortableRuntimeEnvironment(psi, pvsDir);
            return psi;
        }

        static void ApplyPortableRuntimeEnvironment(ProcessStartInfo psi, string pvsDir)
        {
            var preferredPaths = new[]
            {
                Path.Combine(pvsDir, "bin"),
                Path.Combine(pvsDir, "data", "lib", "python"),
                Path.Combine(pvsDir, "data", "lib", "python", "Scripts"),
                Path.Combine(pvsDir, "data", "lib", "pwsh"),
                Path.Combine(pvsDir, "data", "lib", "pwsh", "bin")
            };

            string basePath = psi.EnvironmentVariables["PATH"];
            if (string.IsNullOrEmpty(basePath))
                basePath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

            var pyenvRoots = new List<string>();
            CollectIfPresent(pyenvRoots, psi.EnvironmentVariables["PYENV"]);
            CollectIfPresent(pyenvRoots, psi.EnvironmentVariables["PYENV_ROOT"]);
            CollectIfPresent(pyenvRoots, psi.EnvironmentVariables["PYENV_HOME"]);

            RemoveEnvironmentVariable(psi, "PYENV");
            RemoveEnvironmentVariable(psi, "PYENV_ROOT");
            RemoveEnvironmentVariable(psi, "PYENV_HOME");
            RemoveEnvironmentVariable(psi, "PYENV_DIR");
            RemoveEnvironmentVariable(psi, "PYENV_VERSION");
            RemoveEnvironmentVariable(psi, "PYENV_SHELL");
            RemoveEnvironmentVariable(psi, "PYENV_HOOK_PATH");

            var pathEntries = basePath
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(existing => !IsPyenvPath(existing, pyenvRoots))
                .ToList();

            for (int i = preferredPaths.Length - 1; i >= 0; i--)
            {
                string candidate = preferredPaths[i];
                if (!Directory.Exists(candidate))
                    continue;

                bool alreadyPresent = pathEntries.Any(existing =>
                    string.Equals(existing.TrimEnd('\\'), candidate.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
                if (!alreadyPresent)
                    pathEntries.Insert(0, candidate);
            }

            psi.EnvironmentVariables["PATH"] = string.Join(";", pathEntries.ToArray());
        }

        static void CollectIfPresent(ICollection<string> values, string candidate)
        {
            if (!string.IsNullOrEmpty(candidate))
                values.Add(candidate);
        }

        static void RemoveEnvironmentVariable(ProcessStartInfo psi, string name)
        {
            if (psi.EnvironmentVariables.ContainsKey(name))
                psi.EnvironmentVariables.Remove(name);
        }

        static bool IsPyenvPath(string pathEntry, IEnumerable<string> pyenvRoots)
        {
            if (string.IsNullOrEmpty(pathEntry))
                return false;

            string normalizedEntry = pathEntry.Trim().TrimEnd('\\');
            foreach (var root in pyenvRoots)
            {
                if (string.IsNullOrEmpty(root))
                    continue;

                string normalizedRoot = root.Trim().TrimEnd('\\');
                if (normalizedEntry.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return normalizedEntry.IndexOf("\\.pyenv\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalizedEntry.IndexOf("\\pyenv-win\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool CheckRuntimeMigration(string pvsDir, string pvsInfo)
        {
            if (!File.Exists(pvsInfo))
                return false;

            var lines = File.ReadAllLines(pvsInfo).ToList();
            string currentPath = pvsDir.TrimEnd('\\');
            string currentMachineName = Environment.MachineName;
            string currentDesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string currentProgramsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

            string storedPath = GetInfoValue(lines, "INSTALL_PATH");
            string storedMachineName = GetInfoValue(lines, "INSTALL_HOST_MACHINE");
            string storedDesktopPath = GetInfoValue(lines, "INSTALL_USER_DESKTOP");
            string storedProgramsPath = GetInfoValue(lines, "INSTALL_START_MENU");

            bool pathChanged = !PathsEqual(currentPath, storedPath);
            bool machineChanged = !string.Equals(currentMachineName ?? string.Empty, storedMachineName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            bool desktopChanged = !PathsEqual(currentDesktopPath, storedDesktopPath);
            bool programsChanged = !PathsEqual(currentProgramsPath, storedProgramsPath);
            bool missingContext = string.IsNullOrEmpty(storedMachineName) || string.IsNullOrEmpty(storedDesktopPath) || string.IsNullOrEmpty(storedProgramsPath);

            if (!pathChanged && !machineChanged && !desktopChanged && !programsChanged && !missingContext)
                return false;

            UpsertInfoValue(lines, "INSTALL_PATH", currentPath);
            UpsertInfoValue(lines, "INSTALL_HOST_MACHINE", currentMachineName);
            UpsertInfoValue(lines, "INSTALL_USER_DESKTOP", currentDesktopPath);
            UpsertInfoValue(lines, "INSTALL_START_MENU", currentProgramsPath);

            File.WriteAllLines(pvsInfo, lines);

            return true;
        }

        static string GetInfoValue(IList<string> lines, string key)
        {
            foreach (var line in lines)
            {
                if (line.StartsWith(key + "="))
                    return line.Substring(key.Length + 1);
            }

            return null;
        }

        static void UpsertInfoValue(IList<string> lines, string key, string value)
        {
            string normalizedValue = value ?? string.Empty;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith(key + "="))
                {
                    lines[i] = key + "=" + normalizedValue;
                    return;
                }
            }

            lines.Add(key + "=" + normalizedValue);
        }

        static bool PathsEqual(string left, string right)
        {
            string normalizedLeft = string.IsNullOrEmpty(left) ? string.Empty : left.TrimEnd('\\');
            string normalizedRight = string.IsNullOrEmpty(right) ? string.Empty : right.TrimEnd('\\');
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        static void ManageShortcuts(string pvsDir, bool forceUpdate)
        {
            string launcherPath = Path.Combine(pvsDir, "launcher.exe");
            string iconPath = Path.Combine(pvsDir, "Code.exe");

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            string shortcutName = "VSCode (Portable)";

            ShortcutManager.CreateOrUpdateShortcut(desktop, shortcutName, launcherPath, iconPath, pvsDir, "--from-shortcut", forceUpdate);
            ShortcutManager.CreateOrUpdateShortcut(startMenu, shortcutName, launcherPath, iconPath, pvsDir, "--from-shortcut", forceUpdate);
        }

        static void EnsureFonts(string pvsDir)
        {
            string fontDir = Path.Combine(pvsDir, "data", "lib", "fonts");

            FontManager.EnsureFontInstalled(fontDir, "0xProtoNerdFont-Regular.ttf", "0xProto Nerd Font Mono");
            FontManager.EnsureFontInstalled(fontDir, "DalseoHealingMedium.ttf", "DalseoHealing");
        }

        static async Task<int> CheckVersionsAsync()
        {
            string pvsDir = AppDomain.CurrentDomain.BaseDirectory;
            string pvsInfo = Path.Combine(pvsDir, "pvs.info");

            try
            {
                CommonHelper.EnableTls12();

                if (!File.Exists(pvsInfo))
                    return 0;

                var versions = ReadVersionsFromInfo(pvsInfo);

                await Task.WhenAll(
                    CheckPowerShell7Version(pvsDir, versions),
                    CheckOhMyPoshVersion(pvsDir, versions),
                    CheckTerminalIconsVersion(pvsDir, versions),
                    CheckPSFzfVersion(pvsDir, versions),
                    CheckModernUnixVersion(pvsDir, versions),
                    CheckVSCodeVersion(pvsDir, versions)
                );

                return 0;
            }
            catch
            {
                return 1;
            }
        }

        static Dictionary<string, string> ReadVersionsFromInfo(string pvsInfo)
        {
            var versions = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(pvsInfo))
            {
                int idx = line.IndexOf('=');
                if (idx > 0)
                {
                    versions[line.Substring(0, idx)] = line.Substring(idx + 1);
                }
            }
            return versions;
        }

        static async Task CheckPowerShell7Version(string pvsDir, Dictionary<string, string> versions)
        {
            try
            {
                if (!versions.ContainsKey("PWSH7_VERSION"))
                    return;

                string currentVer = versions["PWSH7_VERSION"];
                string latestVer = await GetLatestGitHubRelease("PowerShell/PowerShell");

                if (!string.IsNullOrEmpty(latestVer) && latestVer != currentVer)
                {
                    File.WriteAllText(Path.Combine(pvsDir, "upgrade_pwsh7"), latestVer);
                    versions["PWSH7_VERSION"] = latestVer;
                }
            }
            catch { }
        }

        static async Task CheckOhMyPoshVersion(string pvsDir, Dictionary<string, string> versions)
        {
            try
            {
                if (!versions.ContainsKey("OHMYPOSH_VERSION"))
                    return;

                string currentVer = versions["OHMYPOSH_VERSION"];
                string latestVer = await GetLatestGitHubRelease("JanDeDobbeleer/oh-my-posh");

                if (!string.IsNullOrEmpty(latestVer) && latestVer != currentVer)
                {
                    File.WriteAllText(Path.Combine(pvsDir, "upgrade_ohmyposh"), latestVer);
                    versions["OHMYPOSH_VERSION"] = latestVer;
                }
            }
            catch { }
        }

        static async Task CheckTerminalIconsVersion(string pvsDir, Dictionary<string, string> versions)
        {
            try
            {
                if (!versions.ContainsKey("TERMINAL_ICONS_VERSION"))
                    return;

                string currentVer = versions["TERMINAL_ICONS_VERSION"];
                string latestVer = await GetLatestPSGalleryVersion("Terminal-Icons");

                if (!string.IsNullOrEmpty(latestVer) && latestVer != currentVer)
                {
                    File.WriteAllText(Path.Combine(pvsDir, "upgrade_term_icons"), latestVer);
                    versions["TERMINAL_ICONS_VERSION"] = latestVer;
                }
            }
            catch { }
        }

        static async Task CheckPSFzfVersion(string pvsDir, Dictionary<string, string> versions)
        {
            try
            {
                if (!versions.ContainsKey("PSFZF_VERSION"))
                    return;

                string currentVer = versions["PSFZF_VERSION"];
                string latestVer = await GetLatestPSGalleryVersion("PSFzf");

                if (!string.IsNullOrEmpty(latestVer) && latestVer != currentVer)
                {
                    File.WriteAllText(Path.Combine(pvsDir, "upgrade_psfzf"), latestVer);
                    versions["PSFZF_VERSION"] = latestVer;
                }
            }
            catch { }
        }

        static async Task CheckModernUnixVersion(string pvsDir, Dictionary<string, string> versions)
        {
            try
            {
                if (!versions.ContainsKey("MODERN_UNIX_WIN_VERSION"))
                    return;

                string currentVer = versions["MODERN_UNIX_WIN_VERSION"];
                string latestVer = await GetLatestPSGalleryVersion("modern-unix-win");

                if (!string.IsNullOrEmpty(latestVer) && latestVer != currentVer)
                {
                    File.WriteAllText(Path.Combine(pvsDir, "upgrade_modern_unix"), latestVer);
                    versions["MODERN_UNIX_WIN_VERSION"] = latestVer;
                }
            }
            catch { }
        }

        static async Task CheckVSCodeVersion(string pvsDir, Dictionary<string, string> versions)
        {
            try
            {
                if (!versions.ContainsKey("VSCODE_VERSION"))
                    return;

                string currentVer = versions["VSCODE_VERSION"];
                string latestVer = await GetLatestVSCodeVersion();

                if (!string.IsNullOrEmpty(latestVer) && latestVer != currentVer)
                {
                    File.WriteAllText(Path.Combine(pvsDir, "upgrade_vscode"), latestVer);
                    versions["VSCODE_VERSION"] = latestVer;
                }
            }
            catch { }
        }

        static async Task<string> GetLatestGitHubRelease(string repo)
        {
            try
            {
                string url = string.Format("https://api.github.com/repos/{0}/releases/latest", repo);
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "VSCode-Portable-Launcher");
                    client.DefaultRequestHeaders.ConnectionClose = false; // Keep-Alive
                    client.Timeout = TimeSpan.FromSeconds(30);
                    string json = await client.GetStringAsync(url);
                    var match = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([^\"]+)\"");
                    return match.Success ? match.Groups[1].Value : "";
                }
            }
            catch
            {
                return "";
            }
        }

        static async Task<string> GetLatestPSGalleryVersion(string moduleName)
        {
            try
            {
                string url = string.Format("https://www.powershellgallery.com/packages/{0}", moduleName);
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "VSCode-Portable-Launcher");
                    client.DefaultRequestHeaders.ConnectionClose = false; // Keep-Alive
                    client.Timeout = TimeSpan.FromSeconds(30);
                    string html = await client.GetStringAsync(url);
                    var match = Regex.Match(html, string.Format("{0}\\s+([\\d\\.]+)", moduleName));
                    return match.Success ? match.Groups[1].Value : "";
                }
            }
            catch
            {
                return "";
            }
        }

        static int InitializePVS()
        {
            string pvsDir = AppDomain.CurrentDomain.BaseDirectory;
            
            try
            {
                Console.WriteLine("Initializing PVS environment...");
                
                // 1. Sync VSCode extensions
                Console.WriteLine("1/6: Synchronizing VSCode extensions...");
                SyncVSCodeExtensions(pvsDir);
                
                // 2. Restore origin files
                Console.WriteLine("2/6: Restoring configuration files...");
                RestoreOriginFiles(pvsDir);
                
                // 3. Clean Python environment
                Console.WriteLine("3/6: Cleaning Python environment...");
                CleanPythonEnvironment(pvsDir);
                
                // 4. Install fonts and create shortcuts
                Console.WriteLine("4/6: Installing fonts and creating shortcuts...");
                InstallFontsAndShortcuts(pvsDir);
                
                // 5. Clean user-data (except settings.json)
                Console.WriteLine("5/6: Cleaning user data...");
                CleanUserData(pvsDir);
                
                // 6. Launch VSCode
                Console.WriteLine("6/6: Launching VSCode...");
                LaunchVSCodeAndExit(pvsDir);
                
                Console.WriteLine("PVS initialization completed successfully!");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Initialization error: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static void SyncVSCodeExtensions(string pvsDir)
        {
            try
            {
                string extensionsDir = Path.Combine(pvsDir, "data", "extensions");
                if (!Directory.Exists(extensionsDir))
                {
                    Console.WriteLine("  Extensions directory not found, skipping...");
                    return;
                }

                // Get installed extensions
                var installedDirs = Directory.GetDirectories(extensionsDir)
                    .Select(d => Path.GetFileName(d))
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();

                var requiredExtensions = UpgradeForm.VSCODE_EXTENSIONS.ToList();

                foreach (var required in PlatformSensitiveVSCodeExtensions)
                {
                    var invalidInstalls = installedDirs
                        .Where(installed => installed.StartsWith(required + "-", StringComparison.OrdinalIgnoreCase))
                        .Where(installed => !ValidateInstalledVSCodeExtension(Path.Combine(extensionsDir, installed), required))
                        .ToList();

                    foreach (var invalidInstall in invalidInstalls)
                    {
                        string invalidPath = Path.Combine(extensionsDir, invalidInstall);
                        Console.WriteLine("  Removing invalid extension payload: " + invalidInstall);
                        Directory.Delete(invalidPath, true);
                        installedDirs.Remove(invalidInstall);
                    }
                }
                
                // Find extensions to remove (not in required list)
                var toRemove = installedDirs.Where(installed =>
                {
                    return !requiredExtensions.Any(required =>
                        installed.StartsWith(required + "-", StringComparison.OrdinalIgnoreCase));
                }).ToList();

                // Remove unwanted extensions
                foreach (var ext in toRemove)
                {
                    string extPath = Path.Combine(extensionsDir, ext);
                    Console.WriteLine("  Removing: " + ext);
                    Directory.Delete(extPath, true);
                }

                // Find extensions to install (required but not installed)
                var toInstall = requiredExtensions.Where(required =>
                {
                    return !installedDirs.Any(installed =>
                        installed.StartsWith(required + "-", StringComparison.OrdinalIgnoreCase));
                }).ToList();

                // Install missing extensions
                if (toInstall.Any())
                {
                    string codeExe = Path.Combine(pvsDir, "Code.exe");
                    string cliJs = ResolveVSCodeCliJsPath(pvsDir);
                    string relativeExtensionsDir = Path.Combine("data", "extensions");
                    string relativeUserDataDir = Path.Combine("data", "user-data");

                    if (!File.Exists(codeExe) || string.IsNullOrEmpty(cliJs) || !File.Exists(cliJs))
                    {
                        Console.WriteLine("  VS Code CLI not found, skipping extension install...");
                    }
                    else
                    {
                        foreach (var ext in toInstall)
                        {
                            Console.WriteLine("  Installing: " + ext);
                            var psi = new ProcessStartInfo
                            {
                                FileName = codeExe,
                                Arguments = "\"" + cliJs + "\" --extensions-dir \"" + relativeExtensionsDir + "\" --user-data-dir \"" + relativeUserDataDir + "\" --install-extension " + ext + " --force",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = pvsDir
                            };
                            psi.EnvironmentVariables["ELECTRON_RUN_AS_NODE"] = "1";
                            psi.EnvironmentVariables["VSCODE_DEV"] = "";

                            using (var process = Process.Start(psi))
                            {
                                process.WaitForExit();
                            }
                        }
                    }
                }

                RemoveExtensionsByPrefix(extensionsDir, OptionalAutoInstalledVSCodeExtensions, "  Removing optional extension: ");

                // Delete extensions.json
                string extensionsJson = Path.Combine(extensionsDir, "extensions.json");
                if (File.Exists(extensionsJson))
                {
                    Console.WriteLine("  Deleting extensions.json");
                    File.Delete(extensionsJson);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  Extension sync error: " + ex.Message);
            }
        }

        static string ResolveVSCodeCliJsPath(string pvsDir)
        {
            string cliJs = Path.Combine(pvsDir, "resources", "app", "out", "cli.js");
            if (File.Exists(cliJs))
                return cliJs;

            var candidates = Directory.GetFiles(pvsDir, "cli.js", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(Path.Combine("resources", "app", "out", "cli.js"), StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return candidates.FirstOrDefault() ?? string.Empty;
        }

        static bool ValidateInstalledVSCodeExtension(string extensionPath, string extensionId)
        {
            if (string.IsNullOrEmpty(extensionPath) || string.IsNullOrEmpty(extensionId) || !Directory.Exists(extensionPath))
                return false;

            if (!PlatformSensitiveVSCodeExtensions.Any(ext => string.Equals(ext, extensionId, StringComparison.OrdinalIgnoreCase)))
                return true;

            string toolsDir = Path.Combine(extensionPath, "python-env-tools", "bin");
            string windowsToolPath = Path.Combine(toolsDir, "pet.exe");
            if (File.Exists(windowsToolPath))
                return true;

            string extractedToolPath = Path.Combine(toolsDir, "pet");
            if (!File.Exists(extractedToolPath) || !HasPortableExecutableHeader(extractedToolPath))
                return false;

            try
            {
                File.Copy(extractedToolPath, windowsToolPath, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool HasPortableExecutableHeader(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length < 2)
                        return false;

                    int first = stream.ReadByte();
                    int second = stream.ReadByte();
                    return first == 'M' && second == 'Z';
                }
            }
            catch
            {
                return false;
            }
        }

        static void RemoveExtensionsByPrefix(string extensionsDir, IEnumerable<string> extensionIds, string messagePrefix)
        {
            if (string.IsNullOrEmpty(extensionsDir) || !Directory.Exists(extensionsDir) || extensionIds == null)
                return;

            foreach (var extensionId in extensionIds)
            {
                if (string.IsNullOrEmpty(extensionId))
                    continue;

                foreach (var installedDir in Directory.GetDirectories(extensionsDir)
                    .Where(dir => Path.GetFileName(dir).StartsWith(extensionId + "-", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine(messagePrefix + Path.GetFileName(installedDir));
                    Directory.Delete(installedDir, true);
                }
            }
        }

        static void RestoreOriginFiles(string pvsDir)
        {
            try
            {
                string originDir = Path.Combine(pvsDir, "data", "lib", "origin");
                if (!Directory.Exists(originDir))
                {
                    Console.WriteLine("  Origin directory not found, skipping...");
                    return;
                }

                // Restore settings.json
                string originSettings = Path.Combine(originDir, "settings.json");
                if (File.Exists(originSettings))
                {
                    string targetSettings = Path.Combine(pvsDir, "data", "user-data", "User", "settings.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(targetSettings));
                    File.Copy(originSettings, targetSettings, true);
                    Console.WriteLine("  Restored: settings.json");
                }

                // Restore Microsoft.PowerShell_profile.ps1
                string originProfile = Path.Combine(originDir, "Microsoft.PowerShell_profile.ps1");
                if (File.Exists(originProfile))
                {
                    string targetProfile = Path.Combine(pvsDir, "data", "lib", "pwsh", "Microsoft.PowerShell_profile.ps1");
                    File.Copy(originProfile, targetProfile, true);
                    Console.WriteLine("  Restored: Microsoft.PowerShell_profile.ps1");
                }

                // Restore tos-term.omp.json
                string originTheme = Path.Combine(originDir, "tos-term.omp.json");
                if (File.Exists(originTheme))
                {
                    string targetTheme = Path.Combine(pvsDir, "data", "lib", "pwsh", "ohmyposh", "themes", "tos-term.omp.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(targetTheme));
                    File.Copy(originTheme, targetTheme, true);
                    Console.WriteLine("  Restored: tos-term.omp.json");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  Origin file restore error: " + ex.Message);
            }
        }

        static void CleanPythonEnvironment(string pvsDir)
        {
            try
            {
                // Delete Scripts folder
                string scriptsDir = Path.Combine(pvsDir, "data", "lib", "python", "Scripts");
                if (Directory.Exists(scriptsDir))
                {
                    Directory.Delete(scriptsDir, true);
                    Console.WriteLine("  Deleted: Scripts folder");
                }

                // Clean site-packages (keep only pip*)
                string sitePackages = Path.Combine(pvsDir, "data", "lib", "python", "Lib", "site-packages");
                if (Directory.Exists(sitePackages))
                {
                    var items = Directory.GetFileSystemEntries(sitePackages);
                    foreach (var item in items)
                    {
                        string name = Path.GetFileName(item);
                        if (!name.StartsWith("pip", StringComparison.OrdinalIgnoreCase))
                        {
                            if (Directory.Exists(item))
                                Directory.Delete(item, true);
                            else
                                File.Delete(item);
                        }
                    }
                    Console.WriteLine("  Cleaned: site-packages (kept pip* only)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  Python cleanup error: " + ex.Message);
            }
        }

        static void InstallFontsAndShortcuts(string pvsDir)
        {
            try
            {
                // Install fonts
                string fontsDir = Path.Combine(pvsDir, "data", "lib", "fonts");
                if (Directory.Exists(fontsDir))
                {
                    string nerdFont = Path.Combine(fontsDir, "0xProtoNerdFont-Regular.ttf");
                    string dalseoFont = Path.Combine(fontsDir, "DalseoHealingMedium.ttf");

                    if (File.Exists(nerdFont))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "reg",
                            Arguments = "add \"HKCU\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Fonts\" /v \"0xProto Nerd Font Mono\" /d \"" + nerdFont + "\" /f",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using (var process = Process.Start(psi))
                            process.WaitForExit();
                        Console.WriteLine("  Installed: 0xProto Nerd Font");
                    }

                    if (File.Exists(dalseoFont))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "reg",
                            Arguments = "add \"HKCU\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Fonts\" /v \"DalseoHealing\" /d \"" + dalseoFont + "\" /f",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using (var process = Process.Start(psi))
                            process.WaitForExit();
                        Console.WriteLine("  Installed: DalseoHealing Font");
                    }
                }

                // Create shortcuts
                string launcherPath = Path.Combine(pvsDir, "launcher.exe");
                string iconPath = Path.Combine(pvsDir, "Code.exe");
                
                if (File.Exists(launcherPath) && File.Exists(iconPath))
                {
                    ShortcutManager.CreateShortcuts(pvsDir, launcherPath, iconPath);
                    Console.WriteLine("  Created: Desktop and Start Menu shortcuts");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  Font/shortcut error: " + ex.Message);
            }
        }

        static void CleanUserData(string pvsDir)
        {
            try
            {
                string userDataDir = Path.Combine(pvsDir, "data", "user-data");
                if (!Directory.Exists(userDataDir))
                    return;

                // Get settings.json path to preserve
                string settingsPath = Path.Combine(userDataDir, "User", "settings.json");
                
                // Delete all items except User/settings.json
                var items = Directory.GetFileSystemEntries(userDataDir);
                foreach (var item in items)
                {
                    string name = Path.GetFileName(item);
                    
                    if (name.Equals("User", StringComparison.OrdinalIgnoreCase))
                    {
                        // Keep User folder but clean its contents except settings.json
                        var userItems = Directory.GetFileSystemEntries(item);
                        foreach (var userItem in userItems)
                        {
                            if (!userItem.Equals(settingsPath, StringComparison.OrdinalIgnoreCase))
                            {
                                if (Directory.Exists(userItem))
                                    Directory.Delete(userItem, true);
                                else
                                    File.Delete(userItem);
                            }
                        }
                    }
                    else
                    {
                        // Delete everything else
                        if (Directory.Exists(item))
                            Directory.Delete(item, true);
                        else
                            File.Delete(item);
                    }
                }
                
                Console.WriteLine("  Cleaned: user-data (kept settings.json)");
            }
            catch (Exception ex)
            {
                Console.WriteLine("  User data cleanup error: " + ex.Message);
            }
        }

        static void LaunchVSCodeAndExit(string pvsDir)
        {
            try
            {
                string codeExe = Path.Combine(pvsDir, "Code.exe");
                if (File.Exists(codeExe))
                {
                    var psi = CreateVSCodeStartInfo(pvsDir);
                    
                    using (var process = Process.Start(psi))
                    {
                        // Wait a bit for VSCode to initialize
                        System.Threading.Thread.Sleep(2000);
                    }
                    
                    Console.WriteLine("  VSCode launched");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("  VSCode launch error: " + ex.Message);
            }
        }

        static async Task<string> GetLatestVSCodeVersion()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create("https://update.code.visualstudio.com/latest/win32-x64-archive/stable");
                request.Method = "HEAD";
                request.AllowAutoRedirect = false;

                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                {
                    string redirectUrl = response.Headers["Location"];
                    if (!string.IsNullOrEmpty(redirectUrl))
                    {
                        var match = Regex.Match(redirectUrl, "VSCode-win32-x64-([\\d\\.]+)\\.zip");
                        return match.Success ? match.Groups[1].Value : "";
                    }
                }
            }
            catch
            {
            }
            return "";
        }
    }
}
