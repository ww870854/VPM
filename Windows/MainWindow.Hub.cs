using System;
using System.Linq;
using System.Windows;
using VPM.Language;
using VPM.Windows;

namespace VPM
{
    /// <summary>
    /// Hub-related functionality for MainWindow
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Opens the Hub Browser window
        /// </summary>
        private void HubBrowser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the destination folder (AddonPackages or AllPackages)
                var destinationFolder = GetHubDownloadFolder();
                
                // CRITICAL FIX: Get dictionary of ALL local package names from PackageMetadata
                // NOT from the filtered Packages UI collection!
                // This ensures we include packages from BOTH AddonPackages AND AllPackages folders
                var localPackagePaths = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (_packageManager?.PackageMetadata != null)
                {
                    foreach (var metadata in _packageManager.PackageMetadata.Values)
                    {
                        // Only include packages that are on disk (Loaded or Available)
                        if (metadata.Status != "Loaded" && metadata.Status != "Available")
                            continue;
                        
                        if (!string.IsNullOrEmpty(metadata.FilePath))
                        {
                            // Use the actual filename from the file path as the key
                            // This preserves the exact casing from disk
                            var name = System.IO.Path.GetFileNameWithoutExtension(metadata.FilePath);
                            if (!string.IsNullOrEmpty(name) && !localPackagePaths.ContainsKey(name))
                            {
                                localPackagePaths[name] = metadata.FilePath;
                            }
                        }
                    }
                }
                
                var hubWindow = new HubBrowserWindow(destinationFolder, localPackagePaths, _packageManager, _settingsManager, _imageManager);
                hubWindow.Owner = this;
                hubWindow.ShowDialog();
                
                // Refresh packages after closing Hub browser (in case new packages were downloaded)
                RefreshPackagesAfterHubDownload();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LanguageManager.Instance.GetCodeString("msg_215"), ex.Message), LanguageManager.Instance.GetCodeString("Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Check for package updates from Hub
        /// </summary>
        private async void HubCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetStatus(LanguageManager.Instance.GetCodeString("msg_216"));
                
                using var hubService = new Services.HubService();
                
                // Load packages.json from Hub
                var loaded = await hubService.LoadPackagesJsonAsync();
                if (!loaded)
                {
                    SetStatus(LanguageManager.Instance.GetCodeString("msg_217"));
                    return;
                }
                
                // Check each local package for updates
                int updatesFound = 0;
                foreach (var package in Packages ?? Enumerable.Empty<Models.PackageItem>())
                {
                    var groupName = GetPackageGroupName(package.Name);
                    var localVersion = ExtractVersion(package.Name);
                    
                    if (localVersion > 0 && hubService.HasUpdate(groupName, localVersion))
                    {
                        updatesFound++;
                        // Mark package as having update available
                        package.Status = "Outdated";
                    }
                }
                
                if (updatesFound > 0)
                {
                    SetStatus(string.Format(LanguageManager.Instance.GetCodeString("msg_218"), updatesFound));
                    MessageBox.Show(string.Format(LanguageManager.Instance.GetCodeString("msg_219").Replace("\\n", "\n"), updatesFound), LanguageManager.Instance.GetCodeString("msg_220"),
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    SetStatus(LanguageManager.Instance.GetCodeString("msg_221"));
                    MessageBox.Show(LanguageManager.Instance.GetCodeString("msg_230"), LanguageManager.Instance.GetCodeString("No_Updates"),
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                SetStatus(string.Format(LanguageManager.Instance.GetCodeString("msg_231"), ex.Message));
                MessageBox.Show(string.Format(LanguageManager.Instance.GetCodeString("msg_232"), ex.Message), LanguageManager.Instance.GetCodeString("Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Find and download missing dependencies from Hub
        /// </summary>
        private async void HubMissingDeps_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get missing dependencies from the current view
                var missingDeps = Dependencies?
                    .Where(d => d.Status == LanguageManager.Instance.GetCodeString("Missing") || d.Status == LanguageManager.Instance.GetCodeString("Not Found"))
                    .Select(d => d.DisplayName)
                    .Distinct()
                    .ToList();
                
                if (missingDeps == null || !missingDeps.Any())
                {
                    MessageBox.Show(LanguageManager.Instance.GetCodeString("msg_233").Replace("\\n","\n"), LanguageManager.Instance.GetCodeString("title_8"),
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                SetStatus(string.Format(LanguageManager.Instance.GetCodeString("msg_179"), missingDeps.Count));
                
                using var hubService = new Services.HubService();
                
                // Find packages on Hub
                var foundPackages = await hubService.FindPackagesAsync(missingDeps);
                
                var downloadable = foundPackages.Values.Where(p => !p.NotOnHub).ToList();
                var notFound = missingDeps.Count - downloadable.Count;
                
                if (downloadable.Any())
                {
                    var message = string.Format(LanguageManager.Instance.GetCodeString("msg_234"), downloadable.Count, missingDeps.Count);
                    if (notFound > 0)
                    {
                        message += string.Format(LanguageManager.Instance.GetCodeString("msg_235"), notFound).Replace("\\n","\n");
                    }
                    message += LanguageManager.Instance.GetCodeString("msg_236");
                    
                    var result = MessageBox.Show(message, LanguageManager.Instance.GetCodeString("msg_237"),
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        HubBrowser_Click(sender, e);
                    }
                }
                else
                {
                    MessageBox.Show(string.Format(LanguageManager.Instance.GetCodeString("msg_238"), missingDeps.Count).Replace("\\n","\n"), LanguageManager.Instance.GetCodeString("msg_239"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                
                SetStatus(LanguageManager.Instance.GetCodeString("StatusReady"));
            }
            catch (Exception ex)
            {
                SetStatus(string.Format(LanguageManager.Instance.GetCodeString("msg_240"), ex.Message));
                MessageBox.Show(string.Format(LanguageManager.Instance.GetCodeString("msg_241"), ex.Message), LanguageManager.Instance.GetCodeString("Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Get the folder where Hub downloads should be saved
        /// </summary>
        private string GetHubDownloadFolder()
        {
            // Use AddonPackages folder if available, otherwise use AllPackages
            if (!string.IsNullOrEmpty(_settingsManager?.Settings?.SelectedFolder))
            {
                var addonPackages = System.IO.Path.Combine(_settingsManager.Settings.SelectedFolder, "AddonPackages");
                if (System.IO.Directory.Exists(addonPackages))
                {
                    return addonPackages;
                }
                
                // Try AllPackages as fallback
                var allPackages = System.IO.Path.Combine(_settingsManager.Settings.SelectedFolder, "AllPackages");
                if (System.IO.Directory.Exists(allPackages))
                {
                    return allPackages;
                }
                
                // Create AddonPackages if neither exists
                System.IO.Directory.CreateDirectory(addonPackages);
                return addonPackages;
            }
            
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
        }

        /// <summary>
        /// Refresh packages after downloading from Hub
        /// </summary>
        private void RefreshPackagesAfterHubDownload()
        {
            // Trigger a refresh to pick up newly downloaded packages
            try
            {
                SetStatus(LanguageManager.Instance.GetCodeString("msg_242"));
                RefreshPackages();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow.Hub] Error refreshing after Hub download: {ex.Message}");
                SetStatus(LanguageManager.Instance.GetCodeString("msg_243"));
            }
        }

        /// <summary>
        /// Extract version number from package name
        /// </summary>
        private static int ExtractVersion(string packageName)
        {
            var name = packageName?.Replace(".var", "") ?? "";
            
            for (int i = name.Length - 1; i >= 0; i--)
            {
                if (name[i] == '.')
                {
                    if (i + 1 < name.Length)
                    {
                        var afterDot = name.Substring(i + 1);
                        if (int.TryParse(afterDot, out var version))
                        {
                            return version;
                        }
                    }
                }
            }
            
            return -1;
        }

        /// <summary>
        /// Get package group name (without version)
        /// </summary>
        private static string GetPackageGroupName(string packageName)
        {
            var name = packageName?.Replace(".var", "") ?? "";
            
            for (int i = name.Length - 1; i >= 0; i--)
            {
                if (name[i] == '.')
                {
                    if (i + 1 < name.Length)
                    {
                        var afterDot = name.Substring(i + 1);
                        if (int.TryParse(afterDot, out _))
                        {
                            return name.Substring(0, i);
                        }
                    }
                }
            }
            
            return name;
        }
    }
}
