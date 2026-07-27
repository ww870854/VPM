using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VPM.Language;

namespace VPM.Services
{
    public class SupporterItem
    {
        public string Name { get; set; }
        public string Since { get; set; }
        public string Link { get; set; }

        public string InfoText => string.Format(LanguageManager.Instance.GetCodeString("text_9"), Since);
    }

    public class SupportInfo
    {
        public string PatreonLink { get; set; }
        public List<SupporterItem> Supporters { get; set; } = new List<SupporterItem>();
    }
    public class SupportInfo1
    {
        public string PatreonLink { get; set; }
        public List<SupporterItem> Supporters1 { get; set; } = new List<SupporterItem>();
    }

    public class SupporterJson
    {
        public string name { get; set; }
        public string since { get; set; }
        public string link { get; set; }
    }

    public class SupportDataJson
    {
        public string patreonLink { get; set; }
        public List<SupporterJson> supporters { get; set; }
    }

    public static class SupportService
    {
        private const string DATA_URL = "https://raw.githubusercontent.com/gicstin/VPM/main/support_data.json";
        private static SupportInfo _cachedInfo;
        private static SupportInfo1 _cachedInfo1;
        private static readonly HttpClient _httpClient;

        static SupportService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VPM/1.0");
            _httpClient.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue 
            { 
                NoCache = true,
                NoStore = true
            };
        }

        public static async Task<SupportInfo> GetSupportInfoAsync()
        {
            if (_cachedInfo != null)
                return _cachedInfo;

            try
            {
                // Add a random query parameter to bypass GitHub raw caching
                string urlWithCacheBuster = $"{DATA_URL}?t={DateTime.UtcNow.Ticks}";
                
                var json = await _httpClient.GetStringAsync(urlWithCacheBuster);
                var data = JsonSerializer.Deserialize<SupportDataJson>(json);

                _cachedInfo = new SupportInfo
                {
                    PatreonLink = Decode(data.patreonLink),
                    Supporters = new List<SupporterItem>()
                };

                if (data.supporters != null)
                {
                    foreach (var s in data.supporters)
                    {
                        _cachedInfo.Supporters.Add(new SupporterItem
                        {
                            Name = Decode(s.name),
                            Since = Decode(s.since),
                            Link = Decode(s.link)
                        });
                    }
                }

                return _cachedInfo;
            }
            catch (Exception ex)
            {
                // Fallback or rethrow
                System.Diagnostics.Debug.WriteLine($"Error fetching support data: {ex.Message}");
                return new SupportInfo
                {
                    PatreonLink = "https://www.patreon.com/gicstin", // Fallback
                    Supporters = new List<SupporterItem> 
                    { 
                        new SupporterItem { Name = LanguageManager.Instance.GetCodeString("msg_108"), Since = "Now" } 
                    }
                };
            }
        }

        private static string Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return string.Empty;
            try
            {
                byte[] data = Convert.FromBase64String(encoded);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return encoded;
            }
        }
    }
}
