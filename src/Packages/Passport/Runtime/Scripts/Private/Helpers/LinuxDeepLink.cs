#if UNITY_STANDALONE_LINUX || (UNITY_ANDROID && UNITY_EDITOR_LINUX) || (UNITY_IPHONE && UNITY_EDITOR_LINUX)
using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;
using Immutable.Passport.Core.Logging;
#nullable enable
namespace Immutable.Passport.Helpers
{
    public class LinuxDeepLink : MonoBehaviour
    {
        private const string DeepLinkFile = "deeplink.txt";
        private static LinuxDeepLink? _instance;
        private Action<string>? _callback;
        private string? _protocolName;
        private string? _desktopFilePath;
        private string? _handlerScriptPath;
        private string _deepLinkFilePath => Path.Combine(Application.persistentDataPath, DeepLinkFile);

        /// <summary>
        /// Initialises the Linux deep link handler for a given protocol.
        /// </summary>
        /// <param name="redirectUri">The redirect URI containing the protocol to handle (e.g. "immutable://")</param>
        /// <param name="callback">Callback to invoke when a deep link is received</param>
        public static void Initialise(string redirectUri, Action<string> callback)
        {
            if (_instance == null)
            {
                _instance = new GameObject(nameof(LinuxDeepLink)).AddComponent<LinuxDeepLink>();
                DontDestroyOnLoad(_instance.gameObject);
            }

            if (string.IsNullOrEmpty(redirectUri)) return;

            // Extract protocol name from URI (e.g. "immutable" from "immutable://")
            var protocolName = redirectUri.Split(new[] { "://" }, StringSplitOptions.None)[0];
            _instance._protocolName = protocolName;
            _instance._callback = callback;

            // Register protocol handler
            _instance.RegisterProtocol(protocolName);
        }

        private void RegisterProtocol(string protocolName)
        {
            PassportLogger.Debug($"Register protocol: {protocolName}");

            try
            {
                // Create handler script
                _handlerScriptPath = CreateHandlerScript(protocolName);
                
                // Make the script executable
                SetExecutablePermission(_handlerScriptPath);
                
                // Create desktop entry file
                _desktopFilePath = CreateDesktopEntry(protocolName);
                
                // Update desktop database
                UpdateDesktopDatabase();
                
                PassportLogger.Debug($"Protocol {protocolName} registered successfully");
            }
            catch (Exception ex)
            {
                PassportLogger.Error($"Failed to register protocol: {ex}");
            }
        }

        private string CreateHandlerScript(string protocolName)
        {
            var scriptPath = Path.Combine(Application.persistentDataPath, $"{protocolName}-handler.sh");
            PassportLogger.Debug($"Creating handler script at {scriptPath}");

#if UNITY_EDITOR_LINUX
            // Get Unity project path
            var projectPath = Application.dataPath.Replace("/Assets", "");
            var unityExe = EditorApplication.applicationPath;
            
            string[] scriptLines = {
                "#!/bin/bash",
                "",
                "# Extract the URI from command-line arguments",
                "uri=\"$1\"",
                "",
                $"# Write the URI to the deeplink file",
                $"echo \"$uri\" > \"{_deepLinkFilePath}\"",
                "",
                "# Check if Unity is already running with this project",
                $"if pgrep -f \"{projectPath}\" > /dev/null; then",
                "  # Unity is already running, try to focus the window",
                "  unity_window=$(wmctrl -l | grep -i \"Unity\" | head -n 1 | cut -d' ' -f1)",
                "  if [ -n \"$unity_window\" ]; then",
                "    wmctrl -i -a \"$unity_window\"",
                "  fi",
                "else",
                "  # Start Unity with the project",
                $"  nohup \"{unityExe}\" -projectPath \"{projectPath}\" > /dev/null 2>&1 &",
                "fi",
                "",
                "exit 0"
            };
#else
            // Get game executable path
            string gameExePath = GetGameExecutablePath();
            string gameExeName = Path.GetFileNameWithoutExtension(gameExePath);
            
            string[] scriptLines = {
                "#!/bin/bash",
                "",
                "# Extract the URI from command-line arguments",
                "uri=\"$1\"",
                "",
                $"# Write the URI to the deeplink file",
                $"echo \"$uri\" > \"{_deepLinkFilePath}\"",
                "",
                "# Check if the game is already running",
                $"if pgrep -x \"{gameExeName}\" > /dev/null; then",
                "  # Game is already running, try to focus the window",
                $"  game_window=$(wmctrl -l | grep -i \"{gameExeName}\" | head -n 1 | cut -d' ' -f1)",
                "  if [ -n \"$game_window\" ]; then",
                "    wmctrl -i -a \"$game_window\"",
                "  fi",
                "else",
                "  # Start the game",
                $"  nohup \"{gameExePath}\" > /dev/null 2>&1 &",
                "fi",
                "",
                "exit 0"
            };
#endif

            File.WriteAllLines(scriptPath, scriptLines);
            return scriptPath;
        }

        private void SetExecutablePermission(string filePath)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{filePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            process.WaitForExit();
            
            if (process.ExitCode != 0)
            {
                PassportLogger.Warn($"Failed to set executable permission for {filePath}. Exit code: {process.ExitCode}");
            }
        }

        private string CreateDesktopEntry(string protocolName)
        {
            // Desktop entry file goes in user's local applications directory
            var localAppDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                ".local/share/applications");
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(localAppDir))
            {
                Directory.CreateDirectory(localAppDir);
            }
            
            var desktopFilePath = Path.Combine(localAppDir, $"{protocolName}-handler.desktop");
            PassportLogger.Debug($"Creating desktop entry at {desktopFilePath}");

            string[] desktopFileLines = {
                "[Desktop Entry]",
                "Type=Application",
                $"Name={Application.productName} URI Handler",
                $"Exec=\"{_handlerScriptPath}\" %u",
                "Terminal=false",
                "MimeType=x-scheme-handler/" + protocolName + ";",
                "NoDisplay=true"
            };
            
            File.WriteAllLines(desktopFilePath, desktopFileLines);
            return desktopFilePath;
        }

        private void UpdateDesktopDatabase()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "update-desktop-database",
                        Arguments = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                            ".local/share/applications"),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                process.WaitForExit();
                
                if (process.ExitCode != 0)
                {
                    PassportLogger.Warn($"Failed to update desktop database. Exit code: {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                PassportLogger.Warn($"Failed to update desktop database: {ex.Message}");
            }
        }

        private static string GetGameExecutablePath()
        {
#if UNITY_EDITOR_LINUX
            // Returns the persistent data path in editor
            return EditorApplication.applicationPath;
#else
            // Returns game root directory in build
            var executablePath = Application.dataPath;
            // Strip _Data suffix to get executable path
            executablePath = executablePath.Substring(0, executablePath.Length - 5);
            return executablePath;
#endif
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Only handle deeplink when application regains focus
            if (!hasFocus) return;
            HandleDeeplink();
        }

        private void HandleDeeplink()
        {
            if (!File.Exists(_deepLinkFilePath))
            {
                return;
            }

            try
            {
                // Read deeplink from file
                string uri = File.ReadAllText(_deepLinkFilePath).Trim();
                
                if (string.IsNullOrEmpty(uri))
                {
                    PassportLogger.Warn("Deeplink file exists but is empty");
                    return;
                }

                if (_protocolName != null && !uri.StartsWith(_protocolName))
                {
                    PassportLogger.Error($"Incorrect prefix uri {uri}");
                }
                else
                {
                    // Invoke callback with valid URI
                    _callback?.Invoke(uri);
                    
                    // Delete the deeplink file after processing
                    File.Delete(_deepLinkFilePath);
                    PassportLogger.Debug("Successfully deleted deeplink file");
                    
                    // Clean up desktop entry and handler script
                    CleanUp();
                }
            }
            catch (Exception ex)
            {
                PassportLogger.Error($"Failed to handle deeplink: {ex.Message}");
            }
        }

        private void CleanUp()
        {
            // Clean up instance
            Destroy(gameObject);
            _instance = null;
            
            // No need to clean up handler script and desktop entry
            // They can be reused for future deep links
        }

        private void OnDestroy()
        {
            // Additional cleanup when component is destroyed
            if (File.Exists(_deepLinkFilePath))
            {
                try
                {
                    File.Delete(_deepLinkFilePath);
                }
                catch (Exception ex)
                {
                    PassportLogger.Warn($"Failed to delete deeplink file: {ex.Message}");
                }
            }
        }
    }
}
#endif