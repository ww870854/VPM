using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using VPM.Models;
using VPM.Services;
using VPM.Language;

namespace VPM.Windows
{
    /// <summary>
    /// Detail view for a Hub resource with download functionality
    /// </summary>
    public partial class HubResourceDetailWindow : Window
    {
        private readonly HubResourceDetail _resource;
        private readonly HubService _hubService;
        private readonly SettingsManager _settingsManager;
        private readonly string _destinationFolder;
        private ObservableCollection<HubFileViewModel> _files;
        private bool _hideInstalled;

        public HubResourceDetailWindow(HubResourceDetail resource, HubService hubService, SettingsManager settingsManager, string destinationFolder)
        {
            InitializeComponent();

            _resource = resource;
            _hubService = hubService;
            _settingsManager = settingsManager;
            _destinationFolder = destinationFolder;

            _hideInstalled = _settingsManager?.GetSetting("HubBrowserHideInstalled", false) ?? false;
            if (HideInstalledCheck != null)
                HideInstalledCheck.IsChecked = _hideInstalled;

            LoadResourceDetails();
        }

        private void LoadResourceDetails()
        {
            // Set header info
            TitleText.Text = _resource.Title ?? "Unknown";
            CreatorText.Text = $"by {_resource.Creator ?? "Unknown"}";
            TagLineText.Text = _resource.TagLine ?? "";
            TypeText.Text = _resource.Type ?? "Unknown";
            var category = _resource.Category ?? "Free";
            CategoryText.Text = category switch
            {
                "Free" => LanguageManager.Instance.GetCodeString("text_145"),
                "Paid" => LanguageManager.Instance.GetCodeString("text_146"),
                _ => category
            };
            DownloadCountRun.Text = _resource.DownloadCount.ToString("N0");
            RatingRun.Text = _resource.RatingAvg.ToString("F1");

            // Load thumbnail
            if (!string.IsNullOrEmpty(_resource.ImageUrl))
            {
                _ = LoadThumbnailAsync(_resource.ImageUrl);
            }

            // Build files list
            _files = new ObservableCollection<HubFileViewModel>();

            if (_resource.HubFiles != null)
            {
                foreach (var file in _resource.HubFiles)
                {
                    _files.Add(CreateFileViewModel(file, isDependency: false));
                }
            }

            // Add dependencies
            if (_resource.Dependencies != null)
            {
                foreach (var depGroup in _resource.Dependencies)
                {
                    foreach (var depFile in depGroup.Value)
                    {
                        _files.Add(CreateFileViewModel(depFile, isDependency: true));
                    }
                }
            }

            var view = CollectionViewSource.GetDefaultView(_files);
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(nameof(HubFileViewModel.StatusPriority), ListSortDirection.Ascending));
            view.SortDescriptions.Add(new SortDescription(nameof(HubFileViewModel.Filename), ListSortDirection.Ascending));
            
            view.Filter = (item) =>
            {
                if (!_hideInstalled) return true;
                if (item is HubFileViewModel vm)
                {
                    return !vm.AlreadyHave && !vm.IsInstalled;
                }
                return true;
            };

            if (view is ICollectionViewLiveShaping liveView)
            {
                if (liveView.CanChangeLiveSorting)
                {
                    liveView.LiveSortingProperties.Add(nameof(HubFileViewModel.StatusPriority));
                    liveView.IsLiveSorting = true;
                }
                if (liveView.CanChangeLiveFiltering)
                {
                    liveView.LiveFilteringProperties.Add(nameof(HubFileViewModel.AlreadyHave));
                    liveView.LiveFilteringProperties.Add(nameof(HubFileViewModel.IsInstalled));
                    liveView.IsLiveFiltering = true;
                }
            }

            FilesItemsControl.ItemsSource = view;
            UpdateDownloadAllButton();
        }

        private async Task LoadThumbnailAsync(string imageUrl)
        {
            try
            {
                // Use cached image service to support binary caching and offline access
                var bitmap = await _hubService.GetCachedImageAsync(imageUrl);
                if (bitmap != null)
                {
                    ThumbnailImage.Source = bitmap;
                }
            }
            catch (Exception ex)
            {
                // Ignore thumbnail loading errors but log debug
                System.Diagnostics.Debug.WriteLine($"[HubResourceDetailWindow] Failed to load thumbnail: {ex}");
            }
        }

        private HubFileViewModel CreateFileViewModel(HubFile file, bool isDependency)
        {
            var vm = new HubFileViewModel
            {
                Filename = file.Filename ?? "Unknown",
                FileSize = file.FileSize,
                FileSizeFormatted = HubService.FormatFileSize(file.FileSize),
                LicenseType = file.LicenseType ?? "",
                DownloadUrl = file.EffectiveDownloadUrl,
                LatestUrl = file.LatestUrl,
                IsDependency = isDependency,
                NotOnHub = string.IsNullOrEmpty(file.EffectiveDownloadUrl) || file.EffectiveDownloadUrl == "null"
            };

            // Check if already in library
            var packageName = file.PackageName;
            var localPath = Path.Combine(_destinationFolder, file.Filename ?? "");
            vm.AlreadyHave = File.Exists(localPath);

            vm.CanDownload = !vm.AlreadyHave && !vm.NotOnHub;

            return vm;
        }

        private void UpdateDownloadAllButton()
        {
            var downloadableCount = _files?.Count(f => f.CanDownload) ?? 0;
            DownloadAllButton.IsEnabled = downloadableCount > 0;
            DownloadAllButton.Content = downloadableCount > 0 
                ? string.Format(LanguageManager.Instance.GetCodeString("msg_154"), downloadableCount) 
                : LanguageManager.Instance.GetCodeString("text_122");
        }

        private async void DownloadFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is HubFileViewModel file)
            {
                await DownloadFileAsync(file);
            }
        }

        private async void DownloadAll_Click(object sender, RoutedEventArgs e)
        {
            var filesToDownload = _files.Where(f => f.CanDownload).ToList();
            
            foreach (var file in filesToDownload)
            {
                await DownloadFileAsync(file);
            }
        }

        private async Task DownloadFileAsync(HubFileViewModel file)
        {
            if (!file.CanDownload)
                return;

            try
            {
                file.IsDownloading = true;
                StatusText.Text = string.Format(LanguageManager.Instance.GetCodeString("text_156"), file.Filename);

                var progress = new Progress<HubDownloadProgress>(p =>
                {
                    file.Progress = p.Progress;
                    StatusText.Text = string.Format(LanguageManager.Instance.GetCodeString("text_157"), file.Filename, p.Progress);
                });

                var downloadedPath = await _hubService.DownloadPackageAsync(
                    file.DownloadUrl,
                    _destinationFolder,
                    file.Filename.Replace(".var", ""),
                    progress);

                if (!string.IsNullOrEmpty(downloadedPath))
                {
                    file.AlreadyHave = true;
                    file.CanDownload = false;
                    file.IsDownloading = false;
                    StatusText.Text = string.Format(LanguageManager.Instance.GetCodeString("text_158"), file.Filename);
                }
                else
                {
                    file.IsDownloading = false;
                    StatusText.Text = string.Format(LanguageManager.Instance.GetCodeString("msg_211"), file.Filename);
                }

                // Refresh the list
                FilesItemsControl.Items.Refresh();
                UpdateDownloadAllButton();
            }
            catch (Exception ex)
            {
                file.IsDownloading = false;
                StatusText.Text = string.Format(LanguageManager.Instance.GetCodeString("err_1"), ex.Message);
                MessageBox.Show(string.Format(LanguageManager.Instance.GetCodeString("msg_212"), file.Filename, ex.Message), LanguageManager.Instance.GetCodeString("title_9"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void HideInstalledCheck_Changed(object sender, RoutedEventArgs e)
        {
            _hideInstalled = HideInstalledCheck?.IsChecked == true;
            _settingsManager?.UpdateSetting("HubBrowserHideInstalled", _hideInstalled);
            CollectionViewSource.GetDefaultView(_files)?.Refresh();
        }
    }
}
