using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using VPM.Language;
using VPM.Models;
using VPM.Services;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace VPM.Windows
{
    public sealed class HubBrowserViewModel : ViewModelBase, IDisposable
    {
        private readonly HubService _hubService;
        private readonly SettingsManager _settingsManager;
        private readonly Dictionary<string, string> _localPackagePaths;
        private readonly Dispatcher _uiDispatcher;
        private readonly HubLibraryStatusEvaluator _libraryStatusEvaluator;

        private CancellationTokenSource _searchCts;
        private readonly DispatcherTimer _searchDebounceTimer;
        private bool _suppressAutoSearch;
        private bool _suppressStateSave = true;

        // Pre-computed lookups for fast library status checking
        private HashSet<string> _localPackageNames;
        private Dictionary<string, HashSet<int>> _localPackageVersions;

        private readonly ConcurrentDictionary<string, HubResourceDetail> _resourceDetailCache = new(StringComparer.OrdinalIgnoreCase);

        private readonly object _searchDedupLock = new object();
        private string _activeSearchKey;
        private string _lastCompletedSearchKey;
        private DateTime _lastCompletedSearchUtc = DateTime.MinValue;

        public ObservableCollection<HubResource> Results { get; } = new ObservableCollection<HubResource>();
        public class I18nFilterOption
        {
            public string InternalKey { get; set; }
            public string DisplayText => LanguageManager.Instance[$"{InternalKey}"];
            public override string ToString() => DisplayText;
        }
        public ObservableCollection<I18nFilterOption> ScopeOptions { get; } = new ObservableCollection<I18nFilterOption>
        {
            new I18nFilterOption{ InternalKey = "Hub And Dependencies" },
            new I18nFilterOption{ InternalKey = "Hub Only" },
            new I18nFilterOption{ InternalKey = "All" }
        };
        public ObservableCollection<I18nFilterOption> PayTypeOptions { get; } = new ObservableCollection<I18nFilterOption>
        {
            new I18nFilterOption{ InternalKey = "All" },
            new I18nFilterOption{ InternalKey = "Free" },
            new I18nFilterOption{ InternalKey = "Paid" }
        };
        public ObservableCollection<I18nFilterOption> CategoryOptions { get; } = new ObservableCollection<I18nFilterOption>
        {
            new I18nFilterOption{ InternalKey = "All" },
            new I18nFilterOption{ InternalKey = "Assets + Accessories" },
            new I18nFilterOption{ InternalKey = "Audio" },
            new I18nFilterOption{ InternalKey = "Blend Shapes" },
            new I18nFilterOption{ InternalKey = "Clothing" },
            new I18nFilterOption{ InternalKey = "Comics + Storytelling" },
            new I18nFilterOption{ InternalKey = "Demo + Lite" },
            new I18nFilterOption{ InternalKey = "Environments" },
            new I18nFilterOption{ InternalKey = "Guides" },
            new I18nFilterOption{ InternalKey = "Hairstyles" },
            new I18nFilterOption{ InternalKey = "Lighting + HDRI" },
            new I18nFilterOption{ InternalKey = "Looks" },
            new I18nFilterOption{ InternalKey = "Mocap + Animation" },
            new I18nFilterOption{ InternalKey = "Morphs" },
            new I18nFilterOption{ InternalKey = "Other" },
            new I18nFilterOption{ InternalKey = "Plugins + Scripts" },
            new I18nFilterOption{ InternalKey = "Poses" },
            new I18nFilterOption{ InternalKey = "Scenes" },
            new I18nFilterOption{ InternalKey = "Skins" },
            new I18nFilterOption{ InternalKey = "Textures" },
            new I18nFilterOption{ InternalKey = "Toolkits + Templates" },
            new I18nFilterOption{ InternalKey = "Voxta Content" }
        };
        public ObservableCollection<I18nFilterOption> SortsOptions { get; } = new ObservableCollection<I18nFilterOption>
        {
            new I18nFilterOption{ InternalKey = "Latest Update" },
            new I18nFilterOption{ InternalKey = "Hot Picks" },
            new I18nFilterOption{ InternalKey = "Rating" },
            new I18nFilterOption{ InternalKey = "Reaction Score" },
            new I18nFilterOption{ InternalKey = "Trending Downloads" },
            new I18nFilterOption{ InternalKey = "Trending Positives" },
            new I18nFilterOption{ InternalKey = "Downloads" },
            new I18nFilterOption{ InternalKey = "Most Reviewed" },
            new I18nFilterOption{ InternalKey = "A-Z" }
        };
        public ObservableCollection<I18nFilterOption> Sort2Options { get; } = new ObservableCollection<I18nFilterOption>
        {
            new I18nFilterOption{ InternalKey = "None" },
            new I18nFilterOption{ InternalKey = "Latest Update" },
            new I18nFilterOption{ InternalKey = "Hot Picks" },
            new I18nFilterOption{ InternalKey = "Rating" },
            new I18nFilterOption{ InternalKey = "Reaction Score" },
            new I18nFilterOption{ InternalKey = "Trending Downloads" },
            new I18nFilterOption{ InternalKey = "Trending Positives" },
            new I18nFilterOption{ InternalKey = "Downloads" },
            new I18nFilterOption{ InternalKey = "Most Reviewed" },
            new I18nFilterOption{ InternalKey = "A-Z" }
        };
        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SortOptions { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SortSecondaryOptions { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Creators { get; } = new ObservableCollection<string>();

        private HubResource _selectedResource;
        public HubResource SelectedResource
        {
            get => _selectedResource;
            set => SetProperty(ref _selectedResource, value);
        }

        private string _statusText = LanguageManager.Instance.GetCodeString("StatusReady");
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(IsNotLoading));
                    OnPropertyChanged(nameof(LoadingVisibility));
                    _searchCommand?.RaiseCanExecuteChanged();
                    _refreshCommand?.RaiseCanExecuteChanged();
                    _nextPageCommand?.RaiseCanExecuteChanged();
                    _prevPageCommand?.RaiseCanExecuteChanged();
                    _clearAllFiltersCommand?.RaiseCanExecuteChanged();
                    _clearSearchCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public async Task RefreshLibraryStatusesAsync(CancellationToken cancellationToken = default)
        {
            BuildLocalPackageLookups();

            if (Results == null || Results.Count == 0)
                return;

            await EvaluateStatusesAsync(Results.ToList(), cancellationToken);
        }

        /// <summary>
        /// Re-evaluate library badges for one resource (e.g. after its detail panel loads).
        /// </summary>
        public async Task ApplyResourceLibraryStatusAsync(
            HubResource resource,
            HubResourceDetail knownDetail = null,
            CancellationToken cancellationToken = default)
        {
            if (resource == null)
                return;

            BuildLocalPackageLookups();

            if (resource.IsExternallyHosted)
            {
                await _uiDispatcher.InvokeAsync(new Action(() =>
                {
                    resource.InLibrary = false;
                    resource.UpdateAvailable = false;
                    resource.UpdateMessage = null;
                    resource.DependencyStatusResolved = false;
                    resource.InstalledDependencyCount = 0;
                    resource.MissingDependencyCount = 0;
                    resource.CardDownloadStatusReady = true;
                }), DispatcherPriority.Normal).Task.ConfigureAwait(false);
                return;
            }

            if (knownDetail != null && !string.IsNullOrEmpty(knownDetail.ResourceId))
                _resourceDetailCache[knownDetail.ResourceId] = knownDetail;

            var status = await _libraryStatusEvaluator.EvaluateAsync(
                resource,
                _localPackageVersions,
                _localPackageNames,
                LoadResourceDetailForStatusAsync,
                cancellationToken,
                knownDetail).ConfigureAwait(false);

            var depStatus = await _libraryStatusEvaluator.EvaluateDependencyStatusAsync(
                resource,
                _localPackageVersions,
                _localPackageNames,
                LoadResourceDetailForStatusAsync,
                cancellationToken,
                knownDetail).ConfigureAwait(false);

            await _uiDispatcher.InvokeAsync(new Action(() =>
            {
                resource.InLibrary = status.InLibrary;
                resource.UpdateAvailable = status.UpdateAvailable;
                resource.UpdateMessage = status.UpdateAvailable ? "Update available" : null;
                ApplyDependencyStatus(resource, depStatus);
                resource.CardDownloadStatusReady = true;
            }), DispatcherPriority.Normal).Task.ConfigureAwait(false);
        }

        public bool IsNotLoading => !IsLoading;

        public System.Windows.Visibility LoadingVisibility => IsLoading ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        private bool _hasLoadedResultsOnce;

        private string _errorText;
        public string ErrorText
        {
            get => _errorText;
            set
            {
                if (SetProperty(ref _errorText, value))
                {
                    OnPropertyChanged(nameof(HasError));
                    OnPropertyChanged(nameof(ErrorVisibility));
                }
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

        public System.Windows.Visibility ErrorVisibility => HasError ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        private bool _hasEmptyResults;
        public bool HasEmptyResults
        {
            get => _hasEmptyResults;
            set
            {
                if (SetProperty(ref _hasEmptyResults, value))
                    OnPropertyChanged(nameof(EmptyResultsVisibility));
            }
        }

        public System.Windows.Visibility EmptyResultsVisibility => HasEmptyResults ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    OnPropertyChanged(nameof(PageDisplay));
                    _nextPageCommand?.RaiseCanExecuteChanged();
                    _prevPageCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        private int _totalPages = 1;
        public int TotalPages
        {
            get => _totalPages;
            set
            {
                if (SetProperty(ref _totalPages, value))
                {
                    OnPropertyChanged(nameof(PageDisplay));
                    _nextPageCommand?.RaiseCanExecuteChanged();
                    _prevPageCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        private int _totalResources;
        public int TotalResources
        {
            get => _totalResources;
            set
            {
                if (SetProperty(ref _totalResources, value))
                    OnPropertyChanged(nameof(TotalCountText));
            }
        }

        public string PageDisplay => $"{CurrentPage} / {TotalPages}";
        public string TotalCountText => string.Format(LanguageManager.Instance.GetCodeString("msg_138"), TotalResources);

        // Filters / query
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    OnPropertyChanged(nameof(HasSearchText));
                    _clearSearchCommand?.RaiseCanExecuteChanged();
                    SaveState();
                    if (!_suppressAutoSearch)
                        DebounceSearch();
                }
            }
        }

        public bool HasSearchText => !string.IsNullOrEmpty(SearchText);

        public System.Windows.Visibility ClearSearchVisibility => HasSearchText ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        private string _scope = "All";
        public string Scope
        {
            get => _scope;
            set
            {
                if (SetProperty(ref _scope, value))
                {
                    SaveState();
                    if (!_suppressAutoSearch)
                        _ = SearchAsync(explicitSearch: true);
                }
            }
        }

        private string _payType = "All";
        public string PayType
        {
            get => _payType;
            set
            {
                if (SetProperty(ref _payType, value))
                {
                    SaveState();
                    try { _settingsManager.SaveSettingsImmediate(); } catch { }
                    try { _ = _settingsManager.SaveSettingsAsync(); } catch { }
                    if (!_suppressAutoSearch)
                        _ = SearchAsync(explicitSearch: true);
                }
            }
        }

        private string _category = "All";
        public string Category
        {
            get => _category;
            set
            {
                if (SetProperty(ref _category, value))
                {
                    SaveState();
                    if (!_suppressAutoSearch)
                        _ = SearchAsync(explicitSearch: true);
                }
            }
        }

        private string _creator = "All";
        public string Creator
        {
            get => _creator;
            set
            {
                if (SetProperty(ref _creator, value))
                {
                    SaveState();
                    if (!_suppressAutoSearch)
                        _ = SearchAsync(explicitSearch: true);
                }
            }
        }

        private string _sort = "Latest Update";
        public string Sort
        {
            get => _sort;
            set
            {
                if (SetProperty(ref _sort, value))
                {
                    SaveState();
                    if (!_suppressAutoSearch)
                        _ = SearchAsync(explicitSearch: true);
                }
            }
        }

        private string _sortSecondary = "None";
        public string SortSecondary
        {
            get => _sortSecondary;
            set
            {
                if (SetProperty(ref _sortSecondary, value))
                {
                    SaveState();
                    if (!_suppressAutoSearch)
                        _ = SearchAsync(explicitSearch: true);
                }
            }
        }

        private bool _onlyDownloadable;
        public bool OnlyDownloadable
        {
            get => _onlyDownloadable;
            set
            {
                if (SetProperty(ref _onlyDownloadable, value))
                {
                    SaveState();
                    if (!_suppressAutoSearch)
                        _ = SearchAsync(explicitSearch: true);
                }
            }
        }

        private string _tags = "All";
        public string Tags
        {
            get => _tags;
            set
            {
                SetProperty(ref _tags, value ?? "All");
            }
        }

        // Commands
        private readonly RelayCommand _searchCommand;
        public RelayCommand SearchCommand => _searchCommand;

        private readonly RelayCommand _refreshCommand;
        public RelayCommand RefreshCommand => _refreshCommand;

        private readonly RelayCommand _nextPageCommand;
        public RelayCommand NextPageCommand => _nextPageCommand;

        private readonly RelayCommand _prevPageCommand;
        public RelayCommand PrevPageCommand => _prevPageCommand;

        private readonly RelayCommand _clearAllFiltersCommand;
        public RelayCommand ClearAllFiltersCommand => _clearAllFiltersCommand;

        private readonly RelayCommand _clearSearchCommand;
        public RelayCommand ClearSearchCommand => _clearSearchCommand;

        public HubBrowserViewModel(HubService hubService, SettingsManager settingsManager, Dictionary<string, string> localPackagePaths)
        {
            _hubService = hubService ?? throw new ArgumentNullException(nameof(hubService));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _localPackagePaths = localPackagePaths ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _libraryStatusEvaluator = new HubLibraryStatusEvaluator(_hubService);

            _uiDispatcher = Dispatcher.CurrentDispatcher;

            _hubService.StatusChanged += (s, status) => StatusText = status;

            _searchCommand = new RelayCommand(() => _ = SearchAsync(explicitSearch: true), () => !IsLoading);
            _refreshCommand = new RelayCommand(() => _ = RefreshAsync(), () => !IsLoading);
            _nextPageCommand = new RelayCommand(() => { if (CurrentPage < TotalPages) { CurrentPage++; _ = SearchAsync(explicitSearch: true); } }, () => !IsLoading && CurrentPage < TotalPages);
            _prevPageCommand = new RelayCommand(() => { if (CurrentPage > 1) { CurrentPage--; _ = SearchAsync(explicitSearch: true); } }, () => !IsLoading && CurrentPage > 1);
            _clearAllFiltersCommand = new RelayCommand(() => _ = ClearAllFiltersAsync(), () => !IsLoading);
            _clearSearchCommand = new RelayCommand(() => SearchText = string.Empty, () => !IsLoading && HasSearchText);

            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _searchDebounceTimer.Tick += (s, e) =>
            {
                _searchDebounceTimer.Stop();
                CurrentPage = 1;
                _ = SearchAsync(explicitSearch: false);
            };
        }

        public async Task InitializeAsync()
        {
            IsLoading = true;
            ErrorText = null;
            try
            {
                _suppressStateSave = true;

                // Start loading packages in background
                var packagesTask = _hubService.LoadPackagesJsonAsync();

                // Chain actions to run when packages are loaded
                _ = packagesTask.ContinueWith(async t =>
                {
                    if (t.Status == TaskStatus.RanToCompletion && t.Result)
                    {
                        await _uiDispatcher.InvokeAsync(async () =>
                        {
                            LoadCreatorListFromPackages();
                            // Re-evaluate statuses for currently visible items
                            if (Results != null && Results.Count > 0)
                            {
                                await RefreshLibraryStatusesAsync();
                            }
                        });
                    }
                }, TaskScheduler.Default);

                var filterTask = LoadFilterOptionsAsync();
                
                // Try to load stale cache first for instant UI
                RestoreState();
                var searchParams = BuildSearchParams();
                var cachedResponse = _hubService.TryGetCachedSearch(searchParams, ignoreExpiration: true);
                if (cachedResponse?.IsSuccess == true)
                {
                    TotalResources = cachedResponse.Pagination?.TotalFound ?? 0;
                    TotalPages = cachedResponse.Pagination?.TotalPages ?? 1;
                    
                    Results.Clear();
                    if (cachedResponse.Resources != null)
                    {
                        foreach (var r in cachedResponse.Resources)
                            Results.Add(r);
                    }
                    
                    _hasLoadedResultsOnce = true;
                    HasEmptyResults = TotalResources == 0;
                    
                    // Show as "Updating..." instead of "Loading..."
                    IsLoading = false; 
                    StatusText = LanguageManager.Instance.GetCodeString("msg_162");
                }

                await filterTask;
                
                CurrentPage = Math.Max(1, CurrentPage);
                await SearchAsync(explicitSearch: true, suppressOverlay: _hasLoadedResultsOnce);
            }
            finally
            {
                _suppressStateSave = false;
                IsLoading = false;
            }
        }

        private void DebounceSearch()
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private async Task RefreshAsync()
        {
            _hubService.ClearSearchCache();
            await _hubService.LoadPackagesJsonAsync(forceRefresh: true);
            LoadCreatorListFromPackages();
            CurrentPage = 1;
            await SearchAsync(explicitSearch: true);
        }

        private async Task ClearAllFiltersAsync()
        {
            try
            {
                _suppressAutoSearch = true;

                SearchText = string.Empty;
                Scope = "All";
                Category = "All";
                PayType = "All";
                Sort = SortOptions.Count > 0 ? SortOptions[0] : "Latest Update";
                SortSecondary = "None";
                Creator = "All";
                OnlyDownloadable = false;
            }
            finally
            {
                _suppressAutoSearch = false;
            }

            CurrentPage = 1;
            await SearchAsync(explicitSearch: true);
        }

        private async Task LoadFilterOptionsAsync()
        {
            try
            {
                StatusText = LanguageManager.Instance.GetCodeString("msg_213");
                var result = await _hubService.GetFilterOptionsResultAsync();

                if (!result.Success)
                {
                    StatusText = result.ErrorMessage ?? LanguageManager.Instance.GetCodeString("msg_214");
                }

                var options = result.Success ? result.Value : await _hubService.GetFilterOptionsAsync();

                Categories.Clear();
                Categories.Add("All");
                if (options?.Types != null)
                {
                    foreach (var t in options.Types)
                        Categories.Add(t);
                }

                SortOptions.Clear();
                if (options?.SortOptions != null && options.SortOptions.Count > 0)
                {
                    foreach (var s in options.SortOptions)
                        SortOptions.Add(s);
                }
                else
                {
                    SortOptions.Add("Latest Update");
                }

                SortSecondaryOptions.Clear();
                SortSecondaryOptions.Add("None");
                foreach (var s in SortOptions)
                    SortSecondaryOptions.Add(s);

                if (result.Success)
                    StatusText = LanguageManager.Instance.GetCodeString("StatusReady");
            }
            catch (Exception ex)
            {
                StatusText = string.Format(LanguageManager.Instance.GetCodeString("msg_210"), ex.Message);
            }
        }

        private void LoadCreatorListFromPackages()
        {
            var creators = _hubService.GetAllCreators();
            App.Current.Dispatcher.InvokeAsync(() =>
            {
                Creators.Clear();
                Creators.Add("All");
                foreach (var c in creators)
                    Creators.Add(c);

                var tempSelected = Creator;
                Creator = null;
                Creator = tempSelected;
            });
        }

        private HubSearchParams BuildSearchParams()
        {
            return new HubSearchParams
            {
                Page = CurrentPage,
                PerPage = 48,
                Search = SearchText?.Trim(),
                Location = string.IsNullOrWhiteSpace(Scope) ? "All" : Scope,
                Category = string.IsNullOrWhiteSpace(Category) ? "All" : Category,
                Creator = string.IsNullOrWhiteSpace(Creator) ? "All" : Creator,
                PayType = string.IsNullOrWhiteSpace(PayType) ? "All" : PayType,
                Tags = string.IsNullOrWhiteSpace(Tags) ? "All" : Tags,
                Sort = string.IsNullOrWhiteSpace(Sort) ? "Latest Update" : Sort,
                SortSecondary = string.IsNullOrWhiteSpace(SortSecondary) ? "None" : SortSecondary,
                OnlyDownloadable = OnlyDownloadable
            };
        }

        private static string BuildSearchKey(HubSearchParams p)
        {
            return $"{p?.Page}|{p?.PerPage}|{p?.Location}|{p?.Search}|{p?.PayType}|{p?.Category}|{p?.Creator}|{p?.Tags}|{p?.Sort}|{p?.SortSecondary}|{p?.OnlyDownloadable}";
        }

        public async Task SearchAsync(bool explicitSearch, bool suppressOverlay = false)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                ErrorText = null;
                HasEmptyResults = false;

                // Show overlay if it is an explicit search and not suppressed.
                // ALSO show overlay if we have never loaded results (unless suppressed, but usually we need to show something).
                // If we rely on suppressOverlay from InitializeAsync, it is true only if we found cached data.
                // So if we didn't find cached data, suppressOverlay is false, so showOverlay is true.
                // If we found cached data, suppressOverlay is true. showOverlay is false.
                // For Pagination (explicitSearch=true, suppressOverlay=false), showOverlay is true.
                var showOverlay = (explicitSearch && !suppressOverlay) || (!_hasLoadedResultsOnce && !suppressOverlay) || HasError;
                
                IsLoading = showOverlay;
                StatusText = showOverlay ? LanguageManager.Instance.GetCodeString("text_7") : LanguageManager.Instance.GetCodeString("msg_162");

                var searchParams = BuildSearchParams();

                var searchKey = BuildSearchKey(searchParams);
                lock (_searchDedupLock)
                {
                    // If the same search is already running, don't start it again.
                    if (!string.IsNullOrEmpty(_activeSearchKey) && string.Equals(_activeSearchKey, searchKey, StringComparison.Ordinal))
                    {
                        return;
                    }

                    // If the same search just completed very recently, skip accidental double-invocation.
                    if (!string.IsNullOrEmpty(_lastCompletedSearchKey) && string.Equals(_lastCompletedSearchKey, searchKey, StringComparison.Ordinal) &&
                        (DateTime.UtcNow - _lastCompletedSearchUtc) < TimeSpan.FromSeconds(2))
                    {
                        return;
                    }

                    _activeSearchKey = searchKey;
                }

                var response = await _hubService.SearchResourcesAsync(searchParams, token);

                token.ThrowIfCancellationRequested();

                if (response?.IsSuccess == true)
                {
                    TotalResources = response.Pagination?.TotalFound ?? 0;
                    TotalPages = response.Pagination?.TotalPages ?? 1;

                    var list = response.Resources ?? new List<HubResource>();
                    Results.Clear();
                    foreach (var r in list)
                        Results.Add(r);

                    _hasLoadedResultsOnce = true;

                    // Don't block the page render on expensive detail/status evaluation.
                    // Compute flags in the background and apply them progressively.
                    _ = EvaluateStatusesAsync(list, token);

                    HasEmptyResults = TotalResources == 0;
                    
                    if (!IsLoading)
                    {
                        StatusText = LanguageManager.Instance.GetCodeString("text_17");
                    }
                }
                else
                {
                    var error = response?.Error ?? "Unknown error";
                    ErrorText = error;
                    StatusText = string.Format(LanguageManager.Instance.GetCodeString("err_1"), error);
                }
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            catch (Exception ex)
            {
                ErrorText = ex.Message;
                StatusText = string.Format(LanguageManager.Instance.GetCodeString("err_1"), ex.Message);
            }
            finally
            {
                lock (_searchDedupLock)
                {
                    _lastCompletedSearchKey = _activeSearchKey;
                    _lastCompletedSearchUtc = DateTime.UtcNow;
                    _activeSearchKey = null;
                }
                IsLoading = false;
            }
        }

        private async Task EvaluateStatusesAsync(IReadOnlyList<HubResource> resources, CancellationToken token)
        {
            if (resources == null || resources.Count == 0)
                return;

            BuildLocalPackageLookups();

            // Delay to allow initial UI rendering and high-priority image loading to start/finish
            try 
            {
                await Task.Delay(1500, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Reduced concurrency to avoid saturating the HubService throttle (limit 4)
            // leaving slots for user actions like image loading or new searches.
            var maxConcurrency = 2;
            try
            {
                using (var gate = new SemaphoreSlim(maxConcurrency))
                {
                    var tasks = new List<Task>(resources.Count);
                    foreach (var resource in resources)
                    {
                        tasks.Add(Task.Run(async () =>
                        {
                            await gate.WaitAsync(token).ConfigureAwait(false);
                            try
                            {
                                token.ThrowIfCancellationRequested();
                                var status = await EvaluateLibraryStatusAsync(resource, token).ConfigureAwait(false);
                                var depStatus = await _libraryStatusEvaluator.EvaluateDependencyStatusAsync(
                                    resource,
                                    _localPackageVersions,
                                    _localPackageNames,
                                    LoadResourceDetailForStatusAsync,
                                    token).ConfigureAwait(false);

                                // Apply UI-bound updates on the dispatcher thread
                                await _uiDispatcher.InvokeAsync(new Action(() =>
                                {
                                    resource.InLibrary = status.InLibrary;
                                    resource.UpdateAvailable = status.UpdateAvailable;
                                    resource.UpdateMessage = status.UpdateAvailable ? "Update available" : null;
                                    ApplyDependencyStatus(resource, depStatus);
                                    resource.CardDownloadStatusReady = true;
                                }), DispatcherPriority.Background).Task.ConfigureAwait(false);
                            }
                            finally
                            {
                                gate.Release();
                            }
                        }, token));
                    }

                    await Task.WhenAll(tasks);
                }
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
            catch (Exception)
            {
            }
        }

        private static void ApplyDependencyStatus(
            HubResource resource,
            HubLibraryStatusEvaluator.DependencyStatusResult depStatus)
        {
            if (resource == null)
                return;

            if (!depStatus.Resolved)
            {
                resource.InstalledDependencyCount = 0;
                resource.MissingDependencyCount = 0;
                resource.DependencyStatusResolved = false;
                return;
            }

            resource.InstalledDependencyCount = depStatus.Installed;
            resource.MissingDependencyCount = depStatus.Missing;
            resource.DependencyStatusResolved = true;
        }

        private async Task<HubLibraryStatusEvaluator.StatusResult> EvaluateLibraryStatusAsync(
            HubResource resource,
            CancellationToken cancellationToken)
        {
            return await _libraryStatusEvaluator.EvaluateAsync(
                resource,
                _localPackageVersions,
                _localPackageNames,
                LoadResourceDetailForStatusAsync,
                cancellationToken,
                knownDetail: null).ConfigureAwait(false);
        }

        private async Task<HubResourceDetail> LoadResourceDetailForStatusAsync(string resourceId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(resourceId))
                return null;

            if (_resourceDetailCache.TryGetValue(resourceId, out var cached) && cached != null)
                return cached;

            var detail = await _hubService.GetResourceDetailAsync(resourceId, isPackageName: false, cancellationToken).ConfigureAwait(false);
            if (detail != null)
                _resourceDetailCache[resourceId] = detail;

            return detail;
        }

        private void BuildLocalPackageLookups()
        {
            _localPackageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _localPackageVersions = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in _localPackagePaths)
            {
                var pkg = kvp.Key;
                var path = kvp.Value;

                if (string.IsNullOrWhiteSpace(pkg) || string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                    continue;

                // Prefer on-disk filename (.latest queue keys may point at renamed integer .var)
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(name))
                    name = pkg.Replace(".var", "");
                if (string.IsNullOrEmpty(name))
                    continue;

                _localPackageNames.Add(name);

                var groupName = HubLibraryStatusEvaluator.GetPackageGroupName(name);
                var lastDot = name.LastIndexOf('.');
                if (lastDot > 0 && int.TryParse(name.Substring(lastDot + 1), out var version))
                {
                    if (!_localPackageVersions.TryGetValue(groupName, out var versions))
                    {
                        versions = new HashSet<int>();
                        _localPackageVersions[groupName] = versions;
                    }
                    versions.Add(version);
                }
            }
        }

        private void SaveState()
        {
            try
            {
                if (_suppressStateSave)
                    return;

                _settingsManager.UpdateSetting("HubBrowserSearchText", SearchText?.Trim() ?? "");
                _settingsManager.UpdateSetting("HubBrowserSource", Scope ?? "All");
                _settingsManager.UpdateSetting("HubBrowserCategory", Category ?? "All");
                if (_settingsManager?.Settings != null)
                    _settingsManager.Settings.HubBrowserPayType = PayType ?? "All";
                _settingsManager.UpdateSetting("HubBrowserSort", Sort ?? "Latest Update");
                _settingsManager.UpdateSetting("HubBrowserSortSecondary", SortSecondary ?? "None");
                _settingsManager.UpdateSetting("HubBrowserCreator", Creator ?? "All");
            }
            catch (Exception)
            {
            }
        }

        private void RestoreState()
        {
            try
            {
                _suppressAutoSearch = true;

                SearchText = _settingsManager.GetSetting("HubBrowserSearchText", "") ?? "";
                Scope = _settingsManager.GetSetting("HubBrowserSource", "All") ?? "All";
                Category = _settingsManager.GetSetting("HubBrowserCategory", "All") ?? "All";
                var savedPayType = _settingsManager?.Settings?.HubBrowserPayType ?? "All";
                PayType = savedPayType;
                Sort = _settingsManager.GetSetting("HubBrowserSort", SortOptions.Count > 0 ? SortOptions[0] : "Latest Update") ?? (SortOptions.Count > 0 ? SortOptions[0] : "Latest Update");
                SortSecondary = _settingsManager.GetSetting("HubBrowserSortSecondary", "None") ?? "None";
                Creator = _settingsManager.GetSetting("HubBrowserCreator", "All") ?? "All";
            }
            catch (Exception)
            {
            }
            finally
            {
                _suppressAutoSearch = false;
            }
        }

        public void Dispose()
        {
            try { _searchCts?.Cancel(); } catch { }
            try { _searchCts?.Dispose(); } catch { }
        }
    }
}
