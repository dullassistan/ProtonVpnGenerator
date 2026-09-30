using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ProtonVpnGenerator.Models;
using ProtonVpnGenerator.Services;

namespace ProtonVpnGenerator
{
    public partial class MainWindow : Window
    {
        private readonly ProtonApiService _apiService = new();
        private readonly ConfigBuilderService _configBuilder = new();
        private readonly SettingsManager _settingsManager = new();

        private AppSettings _settings = new();
        private JsonElement? _currentSession;
        private DateTimeOffset? _sessionExpires;
        private DispatcherTimer? _timer;

        private List<ProtonServer> _serversList = new();
        private Dictionary<string, List<ProtonServer>> _serversByCountry = new();
        private bool _isInitializing = true;
        private string? _currentCountryFilter = "all";
        private bool _isUpdatingFilterUi = false;
        private string _clashMode = "awg"; // "awg", "masque", "hybrid"
        private readonly Dictionary<string, int> _serverDownloadCounts = new(StringComparer.OrdinalIgnoreCase);

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _settings = _settingsManager.Load();
            InitPortPills();
            ApplySettingsToUi();

            _isInitializing = false;
            await CheckCachedSessionAsync();
        }

        #region Port & UI Initialization

        private void InitPortPills()
        {
            string targetPort = _settings.SelectedPort ?? "51820";
            foreach (var child in PnlPorts.Children)
            {
                if (child is RadioButton rb && rb.Tag is string p)
                {
                    rb.IsChecked = p == targetPort;
                }
            }
        }

        private void Port_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            if (sender is RadioButton { Tag: string port })
            {
                _settings.SelectedPort = port;
                _settingsManager.Save(_settings);
                TriggerConfigRegeneration();
            }
        }

        private void ApplySettingsToUi()
        {
            _currentCountryFilter = string.IsNullOrWhiteSpace(_settings.SelectedCountryFilter) ? "all" : _settings.SelectedCountryFilter;

            // Client
            if (_settings.SelectedClient == "WireSock") RbClientWiresock.IsChecked = true;
            else if (_settings.SelectedClient == "Clash") RbClientClash.IsChecked = true;
            else RbClientAwg.IsChecked = true;

            // MTU
            TxtMtu.Text = string.IsNullOrWhiteSpace(_settings.Mtu) ? "1420" : _settings.Mtu;

            // AWG 1.0
            SwAwg1.IsChecked = false;
            PnlAwg1Options.Visibility = Visibility.Collapsed;
            if (_settings.Awg1JunkPreset == 2) RbJunk2.IsChecked = true;
            else if (_settings.Awg1JunkPreset == 3) RbJunk3.IsChecked = true;
            else RbJunk1.IsChecked = true;

            if (_settings.Awg1JunkPreset == 3)
            {
                PnlAwg1Custom.Opacity = 1.0;
                PnlAwg1Custom.IsEnabled = true;
                TxtJc.Text = _settings.Awg1Jc;
                TxtJmin.Text = _settings.Awg1Jmin;
                TxtJmax.Text = _settings.Awg1Jmax;
            }
            else
            {
                PnlAwg1Custom.Opacity = 0.8;
                PnlAwg1Custom.IsEnabled = false;
                TxtJc.Text = "";
                TxtJmin.Text = "";
                TxtJmax.Text = "";
            }

            _clashMode = _settings.ClashMode ?? "awg";
            UpdateClashButtonsVisual();

            // AWG 2.0 (QUIC) - default enabled on startup
            SwAwg2.IsChecked = true;
            TxtI1.Text = string.IsNullOrWhiteSpace(_settings.Awg2I1) ? CryptoService.GetRandomAwg2I1() : _settings.Awg2I1;
            TxtI2.Text = _settings.Awg2I2;
            TxtI3.Text = _settings.Awg2I3;
            TxtI4.Text = _settings.Awg2I4;
            TxtI5.Text = _settings.Awg2I5;

            TxtWireSockId.Text = string.IsNullOrWhiteSpace(_settings.WireSockId) ? "apteka.ru" : _settings.WireSockId;
            SetComboValue(CmbWireSockIp, _settings.WireSockIp);
            SetComboValue(CmbWireSockIb, _settings.WireSockIb);

            // AWG 3.0
            SwAwg3.IsChecked = false;
            PnlAwg3Options.Visibility = Visibility.Collapsed;
            TxtCpa.Text = _settings.Awg3Cpa;
            TxtMha.Text = _settings.Awg3Mha;
            TxtKt.Text = _settings.Awg3Kt;
            TxtRat.Text = _settings.Awg3Rat;
            TxtRkat.Text = _settings.Awg3Rkat;
            TxtRt.Text = _settings.Awg3Rt;

            // AWG 3.1
            SwAwg31.IsChecked = false;
            SwRandomTrailers.IsChecked = _settings.Awg31RandomTrailers;
            SwDisableCookies.IsChecked = _settings.Awg31DisableCookies;

            // Enforce toggle rules on initial state:
            // 1) "Во всех случаях выключаем остальные тумблеры при выборе 3.0 или 3.1."
            if (SwAwg3.IsChecked == true)
            {
                SwAwg1.IsChecked = false;
                SwAwg2.IsChecked = false;
                SwAwg31.IsChecked = false;
            }
            else if (SwAwg31.IsChecked == true)
            {
                SwAwg1.IsChecked = false;
                SwAwg2.IsChecked = false;
                SwAwg3.IsChecked = false;
            }

            // LAN & Keepalive & IPv6
            SwExcludeLan.IsChecked = _settings.ExcludeLan;
            SwIpv6.IsChecked = _settings.EnableIpv6;
            SwKeepalive.IsChecked = _settings.PersistentKeepaliveEnabled;
            TxtKeepalive.Text = string.IsNullOrWhiteSpace(_settings.PersistentKeepaliveValue) ? "25" : _settings.PersistentKeepaliveValue;

            UpdateClientUiVisibility();
            UpdateAwgPanelsVisibility();
        }

        private void SetComboValue(ComboBox combo, string value)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Content?.ToString()?.Equals(value, StringComparison.OrdinalIgnoreCase) == true)
                {
                    combo.SelectedItem = item;
                    break;
                }
            }
        }

        #endregion

        #region Session Management

        private async Task CheckCachedSessionAsync()
        {
            if (!string.IsNullOrWhiteSpace(_settings.CachedSessionJson) && _settings.SessionExpiresMs > 0)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (now < _settings.SessionExpiresMs)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(_settings.CachedSessionJson);
                        _currentSession = doc.RootElement.Clone();
                        _sessionExpires = DateTimeOffset.FromUnixTimeMilliseconds(_settings.SessionExpiresMs);

                        StartSessionTimer();
                        await FetchAndRenderServersAsync();
                        ShowAlert("Сессия восстановлена. Серверы загружены!", false);
                        return;
                    }
                    catch (Exception ex)
                    {
                        ShowAlert($"Ошибка восстановления сессии: {ex.Message}", true);
                        ClearSession();
                    }
                }
                else
                {
                    ClearSession();
                }
            }
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            BtnConnect.IsEnabled = false;
            BtnConnect.Content = "⏳ Подключение...";
            if (PnlArrowConnect != null) PnlArrowConnect.Visibility = Visibility.Collapsed;

            try
            {
                var session = await _apiService.CreateSessionAsync();
                _currentSession = session;
                _sessionExpires = DateTimeOffset.UtcNow.AddHours(24);

                _settings.CachedSessionJson = session.GetRawText();
                _settings.SessionExpiresMs = _sessionExpires.Value.ToUnixTimeMilliseconds();
                _settingsManager.Save(_settings);

                StartSessionTimer();
                await FetchAndRenderServersAsync();
                ShowAlert("Успешно подключено. Серверы загружены!", false);
            }
            catch (Exception ex)
            {
                ShowAlert($"Ошибка подключения: {ex.Message}", true);
                BtnConnect.Visibility = Visibility.Visible;
                if (PnlArrowConnect != null) PnlArrowConnect.Visibility = Visibility.Visible;
                BtnConnect.IsEnabled = true;
                BtnConnect.Content = "⚡ Получить сессию и серверы";
            }
        }

        private void BtnResetSession_Click(object sender, RoutedEventArgs e)
        {
            ClearSession();
            ShowAlert("Сессия сброшена.", false);
        }

        private void ClearSession()
        {
            _timer?.Stop();
            _timer = null;

            _currentSession = null;
            _sessionExpires = null;

            _settings.CachedSessionJson = null;
            _settings.SessionExpiresMs = 0;
            _settings.CachedWgPrivateKeyBase64 = null;
            _settings.CachedCertJson = null;
            _settingsManager.Save(_settings);

            PnlSessionTimer.Visibility = Visibility.Collapsed;
            BtnConnect.Visibility = Visibility.Visible;
            if (PnlArrowConnect != null) PnlArrowConnect.Visibility = Visibility.Visible;
            BtnConnect.IsEnabled = true;
            BtnConnect.Content = "⚡ Получить сессию и серверы";

            CmbServers.ItemsSource = null;
            BtnFilterAll.Visibility = Visibility.Collapsed;
            BtnFilterAll.IsChecked = false;
            GridCountryFilters.Children.Clear();
            TxtConfig.Text = "";
        }

        private void StartSessionTimer()
        {
            BtnConnect.Visibility = Visibility.Collapsed;
            if (PnlArrowConnect != null) PnlArrowConnect.Visibility = Visibility.Collapsed;
            PnlSessionTimer.Visibility = Visibility.Visible;

            _timer?.Stop();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, ev) =>
            {
                if (!_sessionExpires.HasValue) return;

                var remaining = _sessionExpires.Value - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    ClearSession();
                    ShowAlert("Срок действия сессии истек. Пожалуйста, подключитесь заново.", true);
                    return;
                }

                TxtTimer.Text = $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            };
            _timer.Start();

            // Run first tick immediately
            if (_sessionExpires.HasValue)
            {
                var remaining = _sessionExpires.Value - DateTimeOffset.UtcNow;
                TxtTimer.Text = $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            }
        }

        #endregion

        #region Server Retrieval and Country Filtering

        private async Task FetchAndRenderServersAsync()
        {
            if (!_currentSession.HasValue) return;

            _serversList = await _apiService.GetServersAsync(_currentSession.Value);

            // Group by country via ServerFilterService
            _serversByCountry = ServerFilterService.GroupByCountry(_serversList);

            // Update download button counts
            int countryCount = _serversByCountry.Count;
            int totalCount = _serversList.Count;
            BtnDownloadCountries.Content = $"📦 По странам ({countryCount})";
            BtnDownloadAll.Content = $"📁 Все ({totalCount})";

            RenderCountryFilters();
            ApplyCountryFilter(_currentCountryFilter ?? "all");
        }

        private void RenderCountryFilters()
        {
            _isUpdatingFilterUi = true;
            try
            {
                GridCountryFilters.Children.Clear();

                var orderedKeys = ServerFilterService.GetOrderedCountryCodes(_serversByCountry.Keys);

                BtnFilterAll.Visibility = Visibility.Visible;
                BtnFilterAll.IsChecked = string.IsNullOrWhiteSpace(_currentCountryFilter) || _currentCountryFilter.Equals("all", StringComparison.OrdinalIgnoreCase);

                foreach (var code in orderedKeys)
                {
                    string label = ProtonServer.GetCountryChipLabel(code);
                    var chip = new RadioButton
                    {
                        Content = label,
                        ToolTip = ProtonServer.GetCountryFullLabel(code),
                        Style = (Style)FindResource("FilterChip"),
                        GroupName = "CountryFilterGroup",
                        Tag = code,
                        IsChecked = code.Equals(_currentCountryFilter, StringComparison.OrdinalIgnoreCase)
                    };

                    chip.Checked += (s, ev) =>
                    {
                        if (_isUpdatingFilterUi) return;
                        if (chip.Tag is string c)
                        {
                            _currentCountryFilter = c;
                            _settings.SelectedCountryFilter = c;
                            _settingsManager.Save(_settings);
                            ApplyCountryFilter(c);
                        }
                    };

                    GridCountryFilters.Children.Add(chip);
                }
            }
            finally
            {
                _isUpdatingFilterUi = false;
            }
        }

        private void BtnFilterAll_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingFilterUi) return;
            _currentCountryFilter = "all";
            _settings.SelectedCountryFilter = "all";
            _settingsManager.Save(_settings);
            ApplyCountryFilter("all");
        }

        private void ApplyCountryFilter(string countryCode)
        {
            var filtered = ServerFilterService.FilterServers(_serversList, _serversByCountry, countryCode);
            CmbServers.ItemsSource = filtered;

            var initialServer = ServerFilterService.GetPreferredInitialServer(filtered, _settings.SelectedServerId);
            if (initialServer != null)
            {
                CmbServers.SelectedItem = initialServer;
            }
        }

        private void CmbServers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbServers.SelectedItem is ProtonServer server)
            {
                _settings.SelectedServerId = server.Id;
                _settingsManager.Save(_settings);
                TriggerConfigRegeneration();
            }
        }

        #endregion

        #region Keys & Config Generation

        private async Task<string> GetOrGeneratePrivateKeyAsync()
        {
            if (!string.IsNullOrWhiteSpace(_settings.CachedWgPrivateKeyBase64) &&
                !string.IsNullOrWhiteSpace(_settings.CachedCertJson))
            {
                return _settings.CachedWgPrivateKeyBase64;
            }

            if (!_currentSession.HasValue)
            {
                throw new Exception("Сначала подключитесь и создайте сессию.");
            }

            var (wgPrivKeyBase64, pemPublicKey) = CryptoService.GenerateKeys();
            var cert = await _apiService.RegisterCertificateAsync(_currentSession.Value, pemPublicKey);

            _settings.CachedWgPrivateKeyBase64 = wgPrivKeyBase64;
            _settings.CachedCertJson = cert.GetRawText();
            _settingsManager.Save(_settings);

            return wgPrivKeyBase64;
        }

        private async Task GenerateConfigInternalAsync()
        {
            if (_isInitializing || CmbServers.SelectedItem is not ProtonServer server)
                return;

            try
            {
                SyncUiToSettings();
                string privKey = await GetOrGeneratePrivateKeyAsync();
                string configText = _configBuilder.BuildConfigString(server, _settings, privKey);
                TxtConfig.Text = configText;
            }
            catch (Exception ex)
            {
                ShowAlert(ex.Message, true);
            }
        }

        private void TriggerConfigRegeneration()
        {
            if (_isInitializing) return;
            _ = GenerateConfigInternalAsync();
        }

        private void SyncUiToSettings()
        {
            _settings.SelectedClient = RbClientClash.IsChecked == true ? "Clash" :
                                      RbClientWiresock.IsChecked == true ? "WireSock" : "AmneziaWG";

            _settings.ClashMode = _clashMode;
            _settings.Mtu = TxtMtu.Text.Trim();

            // AWG 1.0
            _settings.IsAwg1 = SwAwg1.IsChecked == true;
            _settings.Awg1JunkPreset = RbJunk2.IsChecked == true ? 2 :
                                       RbJunk3.IsChecked == true ? 3 : 1;
            _settings.Awg1Jc = string.IsNullOrWhiteSpace(TxtJc.Text) ? "128" : TxtJc.Text.Trim();
            _settings.Awg1Jmin = string.IsNullOrWhiteSpace(TxtJmin.Text) ? "1279" : TxtJmin.Text.Trim();
            _settings.Awg1Jmax = string.IsNullOrWhiteSpace(TxtJmax.Text) ? "1280" : TxtJmax.Text.Trim();

            // AWG 2.0
            _settings.IsAwg2 = SwAwg2.IsChecked == true;
            _settings.Awg2I1 = TxtI1.Text.Trim();
            _settings.Awg2I2 = TxtI2.Text.Trim();
            _settings.Awg2I3 = TxtI3.Text.Trim();
            _settings.Awg2I4 = TxtI4.Text.Trim();
            _settings.Awg2I5 = TxtI5.Text.Trim();

            _settings.WireSockId = TxtWireSockId.Text.Trim();
            _settings.WireSockIp = (CmbWireSockIp.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "quic";
            _settings.WireSockIb = (CmbWireSockIb.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "curl";

            // AWG 3.0
            _settings.IsAwg3 = SwAwg3.IsChecked == true;
            _settings.Awg3Cpa = TxtCpa.Text.Trim();
            _settings.Awg3Mha = TxtMha.Text.Trim();
            _settings.Awg3Kt = TxtKt.Text.Trim();
            _settings.Awg3Rat = TxtRat.Text.Trim();
            _settings.Awg3Rkat = TxtRkat.Text.Trim();
            _settings.Awg3Rt = TxtRt.Text.Trim();

            // AWG 3.1
            _settings.IsAwg31 = SwAwg31.IsChecked == true;
            _settings.Awg31RandomTrailers = SwRandomTrailers.IsChecked == true;
            _settings.Awg31DisableCookies = SwDisableCookies.IsChecked == true;

            // LAN & Keepalive & IPv6
            _settings.ExcludeLan = SwExcludeLan.IsChecked == true;
            _settings.EnableIpv6 = SwIpv6.IsChecked == true;
            _settings.PersistentKeepaliveEnabled = SwKeepalive.IsChecked == true;
            _settings.PersistentKeepaliveValue = TxtKeepalive.Text.Trim();

            _settingsManager.Save(_settings);
        }

        #endregion

        #region Event Handlers for UI Changes

        private void Client_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            bool isClash = RbClientClash.IsChecked == true;
            if (!isClash)
            {
                _clashMode = "awg";
            }

            UpdateClashButtonsVisual();
            UpdateClientUiVisibility();
            UpdateAwgPanelsVisibility();
            TriggerConfigRegeneration();
        }

        private void UpdateClientUiVisibility()
        {
            bool isClash = RbClientClash.IsChecked == true;
            bool isWiresock = RbClientWiresock.IsChecked == true;

            // WireSock vs AWG fields
            PnlWireSockFields.Visibility = isWiresock ? Visibility.Visible : Visibility.Collapsed;
            PnlAwg2Fields.Visibility = (!isWiresock && SwAwg2?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            if (PnlAwg2ExtraFields != null)
                PnlAwg2ExtraFields.Visibility = (!isWiresock && SwAwg2?.IsChecked == true && SwAwg2Custom?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            BtnAwg2Domain.Visibility = isWiresock ? Visibility.Collapsed : Visibility.Visible;

            // Clash disables Exclude LAN and Keepalive
            if (isClash)
            {
                SwExcludeLan.IsEnabled = false;
                SwExcludeLan.IsChecked = false;
                SwKeepalive.IsEnabled = false;
                SwKeepalive.IsChecked = false;
                TxtKeepalive.IsEnabled = false;
            }
            else
            {
                SwExcludeLan.IsEnabled = true;
                SwKeepalive.IsEnabled = true;
                TxtKeepalive.IsEnabled = SwKeepalive.IsChecked == true;
            }
        }

        private void ApplyAwgToggleRules(CheckBox trigger)
        {
            bool isAmnezia = RbClientAwg?.IsChecked == true;

            if (trigger == SwAwg3 && SwAwg3?.IsChecked == true)
            {
                // Во всех случаях выключаем остальные тумблеры при выборе 3.0
                if (SwAwg1 != null) SwAwg1.IsChecked = false;
                if (SwAwg2 != null) SwAwg2.IsChecked = false;
                if (SwAwg31 != null) SwAwg31.IsChecked = false;
            }
            else if (trigger == SwAwg31 && SwAwg31?.IsChecked == true)
            {
                // Во всех случаях выключаем остальные тумблеры при выборе 3.1
                if (SwAwg1 != null) SwAwg1.IsChecked = false;
                if (SwAwg2 != null) SwAwg2.IsChecked = false;
                if (SwAwg3 != null) SwAwg3.IsChecked = false;
            }
            else if (trigger == SwAwg2 && SwAwg2?.IsChecked == true)
            {
                // При включении AWG 2.0 выключаем 3.0 и 3.1
                if (SwAwg3 != null) SwAwg3.IsChecked = false;
                if (SwAwg31 != null) SwAwg31.IsChecked = false;
            }
            else if (trigger == SwAwg1 && SwAwg1?.IsChecked == true)
            {
                // При включении AWG 1.0 выключаем 3.0 и 3.1
                if (SwAwg3 != null) SwAwg3.IsChecked = false;
                if (SwAwg31 != null) SwAwg31.IsChecked = false;
            }

            UpdateAwgPanelsVisibility();
        }

        private void UpdateAwgPanelsVisibility()
        {
            bool isWiresock = RbClientWiresock?.IsChecked == true;

            if (PnlAwg1Options != null)
                PnlAwg1Options.Visibility = SwAwg1?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            if (PnlWireSockFields != null)
                PnlWireSockFields.Visibility = (isWiresock && SwAwg2?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            if (PnlAwg2Fields != null)
                PnlAwg2Fields.Visibility = (!isWiresock && SwAwg2?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            if (PnlAwg2ExtraFields != null)
                PnlAwg2ExtraFields.Visibility = (!isWiresock && SwAwg2?.IsChecked == true && SwAwg2Custom?.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            if (PnlAwg3Options != null)
                PnlAwg3Options.Visibility = SwAwg3?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            if (PnlAwg31Options != null)
                PnlAwg31Options.Visibility = SwAwg31?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SwAwg1_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            ApplyAwgToggleRules(SwAwg1);
            TriggerConfigRegeneration();
        }

        private void SwAwg2_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            ApplyAwgToggleRules(SwAwg2);
            TriggerConfigRegeneration();
        }

        private void SwAwg2Custom_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            UpdateAwgPanelsVisibility();
        }

        private void SwAwg3_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            ApplyAwgToggleRules(SwAwg3);
            TriggerConfigRegeneration();
        }

        private void SwAwg31_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            ApplyAwgToggleRules(SwAwg31);
            TriggerConfigRegeneration();
        }

        private void SwKeepalive_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            if (TxtKeepalive != null)
                TxtKeepalive.IsEnabled = SwKeepalive?.IsChecked == true;
            TriggerConfigRegeneration();
        }

        private void ConfigSetting_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            if (PnlAwg1Custom != null && (sender == RbJunk1 || sender == RbJunk2 || sender == RbJunk3))
            {
                if (RbJunk3?.IsChecked == true)
                {
                    PnlAwg1Custom.Opacity = 1.0;
                    PnlAwg1Custom.IsEnabled = true;
                    if (string.IsNullOrWhiteSpace(TxtJc.Text)) TxtJc.Text = "128";
                    if (string.IsNullOrWhiteSpace(TxtJmin.Text)) TxtJmin.Text = "1279";
                    if (string.IsNullOrWhiteSpace(TxtJmax.Text)) TxtJmax.Text = "1280";
                }
                else
                {
                    TxtJc.Text = "";
                    TxtJmin.Text = "";
                    TxtJmax.Text = "";
                    PnlAwg1Custom.Opacity = 0.8;
                    PnlAwg1Custom.IsEnabled = false;
                }
            }

            TriggerConfigRegeneration();
        }

        private void BtnAwg1Random_Click(object sender, RoutedEventArgs e)
        {
            RbJunk3.IsChecked = true;
            if (PnlAwg1Custom != null)
            {
                PnlAwg1Custom.Opacity = 1.0;
                PnlAwg1Custom.IsEnabled = true;
            }
            var (jc, jmin, jmax) = CryptoService.GenerateRandomAwg1();
            TxtJc.Text = jc.ToString();
            TxtJmin.Text = jmin.ToString();
            TxtJmax.Text = jmax.ToString();
            if (SwAwg1 != null)
            {
                SwAwg1.IsChecked = true;
                ApplyAwgToggleRules(SwAwg1);
            }
            TriggerConfigRegeneration();
        }

        private void UpdateClashButtonsVisual()
        {
            if (BtnMasque == null || BtnAwgMasque == null) return;

            bool isClash = RbClientClash?.IsChecked == true;
            if (!isClash)
            {
                BtnMasque.Opacity = 0.55;
                BtnAwgMasque.Opacity = 0.55;
                return;
            }

            if (_clashMode == "masque")
            {
                BtnMasque.Opacity = 1.0;
                BtnAwgMasque.Opacity = 0.45;
            }
            else if (_clashMode == "hybrid")
            {
                BtnMasque.Opacity = 0.45;
                BtnAwgMasque.Opacity = 1.0;
            }
            else
            {
                BtnMasque.Opacity = 0.55;
                BtnAwgMasque.Opacity = 0.55;
            }
        }

        private void BtnMasque_Click(object sender, RoutedEventArgs e)
        {
            if (RbClientClash?.IsChecked == true && _clashMode == "masque")
            {
                _clashMode = "awg";
                ShowAlert("Режим Clash: стандартный AWG", false);
            }
            else
            {
                _clashMode = "masque";
                if (RbClientClash != null) RbClientClash.IsChecked = true;
                ShowAlert("Режим Clash: Masque (HTTP/3)", false);
            }

            UpdateClashButtonsVisual();
            TriggerConfigRegeneration();
        }

        private void BtnAwgMasque_Click(object sender, RoutedEventArgs e)
        {
            if (RbClientClash?.IsChecked == true && _clashMode == "hybrid")
            {
                _clashMode = "awg";
                ShowAlert("Режим Clash: стандартный AWG", false);
            }
            else
            {
                _clashMode = "hybrid";
                if (RbClientClash != null) RbClientClash.IsChecked = true;
                ShowAlert("Режим Clash: Awg+Masque (Гибрид)", false);
            }

            UpdateClashButtonsVisual();
            TriggerConfigRegeneration();
        }

        private void BtnAwg2Random_Click(object sender, RoutedEventArgs e)
        {
            if (RbClientWiresock.IsChecked == true)
            {
                TxtWireSockId.Text = CryptoService.GetRandomWireSockDomain();
            }
            else
            {
                TxtI1.Text = CryptoService.GetRandomAwg2I1();
            }
            if (SwAwg2 != null)
            {
                SwAwg2.IsChecked = true;
                ApplyAwgToggleRules(SwAwg2);
            }
            TriggerConfigRegeneration();
        }

        private void BtnAwg2Domain_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DomainInputDialog(TxtWireSockId.Text)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                string i1 = QuicPacketGenerator.GenerateI1FromDomain(dialog.Domain);
                if (!string.IsNullOrEmpty(i1))
                {
                    TxtI1.Text = i1;
                    if (SwAwg2 != null)
                    {
                        SwAwg2.IsChecked = true;
                        ApplyAwgToggleRules(SwAwg2);
                    }
                    ShowAlert($"I1 успешно сгенерирован из домена {dialog.Domain}", false);
                    TriggerConfigRegeneration();
                }
                else
                {
                    ShowAlert("Не удалось сгенерировать I1 из домена", true);
                }
            }
        }

        private void BtnAwg3Random_Click(object sender, RoutedEventArgs e)
        {
            var (cpa, mha, kt, rat, rkat, rt) = CryptoService.GenerateRandomAwg3();
            TxtCpa.Text = cpa;
            TxtMha.Text = mha;
            TxtKt.Text = kt;
            TxtRat.Text = rat;
            TxtRkat.Text = rkat;
            TxtRt.Text = rt;
            if (SwAwg3 != null)
            {
                SwAwg3.IsChecked = true;
                ApplyAwgToggleRules(SwAwg3);
            }
            TriggerConfigRegeneration();
        }

        #endregion

        #region Actions & Exports

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (CmbServers.SelectedItem is not ProtonServer)
            {
                ShowAlert("Сначала подключитесь и выберите сервер", true);
                return;
            }

            TriggerConfigRegeneration();
            ShowAlert("Конфигурация успешно сгенерирована", false);
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtConfig.Text))
            {
                ShowAlert("Конфигурация еще не создана", true);
                return;
            }

            Clipboard.SetText(TxtConfig.Text.TrimStart('\uFEFF'));
            ShowAlert("Конфигурация скопирована в буфер обмена!", false);
        }

        private void BtnDownloadSingle_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtConfig.Text) || CmbServers.SelectedItem is not ProtonServer server)
            {
                ShowAlert("Конфигурация еще не создана", true);
                return;
            }

            bool isClash = RbClientClash.IsChecked == true;
            string ext = isClash ? "yaml" : "conf";
            string filter = isClash ? "Clash YAML (*.yaml)|*.yaml" : "WireGuard Config (*.conf)|*.conf";

            int count = _serverDownloadCounts.TryGetValue(server.CleanName, out int currentCount) ? currentCount : 0;
            string initialName = count == 0
                ? $"Proton_{server.CleanName}.{ext}"
                : $"Proton_{server.CleanName}_{count}.{ext}";

            var sfd = new SaveFileDialog
            {
                FileName = initialName,
                Filter = filter,
                DefaultExt = ext
            };

            if (sfd.ShowDialog() == true)
            {
                var utf8NoBom = new UTF8Encoding(false);
                File.WriteAllText(sfd.FileName, TxtConfig.Text.TrimStart('\uFEFF'), utf8NoBom);
                _serverDownloadCounts[server.CleanName] = count + 1;
                ShowAlert($"Файл сохранен: {Path.GetFileName(sfd.FileName)}", false);
            }
        }

        private async void BtnDownloadCountries_Click(object sender, RoutedEventArgs e)
        {
            if (_serversList.Count == 0)
            {
                ShowAlert("Серверы еще не загружены", true);
                return;
            }

            try
            {
                string privKey = await GetOrGeneratePrivateKeyAsync();
                SyncUiToSettings();

                var sfd = new SaveFileDialog
                {
                    FileName = "ProtonVPN_Countries.zip",
                    Filter = "ZIP архив (*.zip)|*.zip",
                    DefaultExt = "zip"
                };

                if (sfd.ShowDialog() == true)
                {
                    byte[] zipBytes = _configBuilder.CreateCountriesZip(_serversList, _settings, privKey);
                    await File.WriteAllBytesAsync(sfd.FileName, zipBytes);
                    ShowAlert($"Архив по странам сохранен: {Path.GetFileName(sfd.FileName)}", false);
                }
            }
            catch (Exception ex)
            {
                ShowAlert($"Ошибка при создании архива: {ex.Message}", true);
            }
        }

        private async void BtnDownloadAll_Click(object sender, RoutedEventArgs e)
        {
            if (_serversList.Count == 0)
            {
                ShowAlert("Серверы еще не загружены", true);
                return;
            }

            try
            {
                string privKey = await GetOrGeneratePrivateKeyAsync();
                SyncUiToSettings();

                var sfd = new SaveFileDialog
                {
                    FileName = "ProtonVPN_All.zip",
                    Filter = "ZIP архив (*.zip)|*.zip",
                    DefaultExt = "zip"
                };

                if (sfd.ShowDialog() == true)
                {
                    byte[] zipBytes = _configBuilder.CreateAllZip(_serversList, _settings, privKey);
                    await File.WriteAllBytesAsync(sfd.FileName, zipBytes);
                    ShowAlert($"Архив всех серверов сохранен: {Path.GetFileName(sfd.FileName)}", false);
                }
            }
            catch (Exception ex)
            {
                ShowAlert($"Ошибка при создании архива: {ex.Message}", true);
            }
        }

        #endregion

        #region Helpers & Navigation

        private void ShowAlert(string message, bool isError)
        {
            TxtAlert.Text = message;
            TxtAlert.Foreground = new SolidColorBrush(isError ? Color.FromRgb(185, 28, 28) : Color.FromRgb(21, 128, 61));
            AlertBox.Background = new SolidColorBrush(isError ? Color.FromRgb(254, 226, 226) : Color.FromRgb(220, 252, 231));
            AlertBox.BorderBrush = new SolidColorBrush(isError ? Color.FromRgb(252, 165, 165) : Color.FromRgb(187, 247, 208));
            AlertBox.BorderThickness = new Thickness(1);
            AlertBox.Visibility = Visibility.Visible;
            TxtCredits.Visibility = Visibility.Collapsed;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, ev) =>
            {
                timer.Stop();
                AlertBox.Visibility = Visibility.Collapsed;
                TxtCredits.Visibility = Visibility.Visible;
            };
            timer.Start();
        }

        private void BtnOpenWebsite_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://proton-generation.github.io");
        }

        private void BtnOpenTelegram_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://t.me/warp_1_1_1_1");
        }

        private void BtnOpenProjects_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://my-other-projects.vercel.app/");
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowAlert($"Не удалось открыть ссылку: {ex.Message}", true);
            }
        }

        #endregion
    }
}