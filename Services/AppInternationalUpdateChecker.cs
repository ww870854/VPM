using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace VPM.Services
{
    public class AppInternationalUpdateChecker
    {
        // Using GitHub API ensures we only see published releases, ignoring "next" versions in version.txt on the main branch
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/ww870854/VPM/releases/latest";
        private const string DefaultReleasePageUrl = "https://github.com/w870854/VPM/releases";

        public class AppInternationalUpdateInfo
        {
            public bool IsUpdateAvailable { get; set; }
            public string CurrentVersion { get; set; }
            public string LatestVersion { get; set; }
            public string ReleaseUrl { get; set; }
        }

        public async Task<AppInternationalUpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // GitHub API requires a User-Agent header
                    client.DefaultRequestHeaders.Add("User-Agent", "VPM-AppInternationalUpdateChecker");
                    
                    var json = await client.GetStringAsync(LatestReleaseApiUrl);
                    
                    using (var doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        
                        // Extract version from tag_name (e.g. "v0.4.21" -> "0.4.21")
                        if (root.TryGetProperty("tag_name", out var tagNameElement))
                        {
                            string tagName = tagNameElement.GetString();
                            string remoteVersionString = tagName;
                            
                            if (!string.IsNullOrEmpty(tagName) && tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                            {
                                remoteVersionString = tagName.Substring(1);
                            }
                            
                            // Get the specific release URL if available
                            string releaseUrl = DefaultReleasePageUrl;
                            if (root.TryGetProperty("html_url", out var htmlUrlElement))
                            {
                                releaseUrl = htmlUrlElement.GetString();
                            }

                            // Current version logic
                            var currentVersionString = VersionInfo.ShortVersion;

                            if (Version.TryParse(currentVersionString, out var currentVersion) &&
                                Version.TryParse(remoteVersionString, out var remoteVersion))
                            {
                                 if (remoteVersion > currentVersion)
                                 {
                                     return new AppInternationalUpdateInfo
                                     {
                                         IsUpdateAvailable = true,
                                         CurrentVersion = currentVersionString,
                                         LatestVersion = remoteVersionString,
                                         ReleaseUrl = releaseUrl
                                     };
                                 }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash app (e.g. rate limiting, network error)
                System.Diagnostics.Debug.WriteLine($"App update check failed: {ex.Message}");
            }

            return new AppInternationalUpdateInfo
            {
                IsUpdateAvailable = false,
                CurrentVersion = VersionInfo.ShortVersion,
                LatestVersion = VersionInfo.ShortVersion, // Fallback
                ReleaseUrl = DefaultReleasePageUrl
            };
        }
    }
}
