using System;
using System.IO;
using ProtonVpnGenerator.Models;
using ProtonVpnGenerator.Services;
using Xunit;

namespace ProtonVpnGenerator.Tests
{
    public class SettingsManagerTests : IDisposable
    {
        private readonly string _testDir;

        public SettingsManagerTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "ProtonSettingsTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                {
                    Directory.Delete(_testDir, true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }

        [Fact]
        public void Load_FileDoesNotExist_ReturnsDefaultSettings()
        {
            string filePath = Path.Combine(_testDir, "nonexistent.json");
            var manager = new SettingsManager(filePath);

            AppSettings settings = manager.Load();

            Assert.NotNull(settings);
            Assert.Equal("AmneziaWG", settings.SelectedClient);
            Assert.Equal("51820", settings.SelectedPort);
            Assert.Equal("all", settings.SelectedCountryFilter);
            Assert.False(settings.ExcludeLan);
        }

        [Fact]
        public void SaveAndLoad_PersistsCustomValues()
        {
            string filePath = Path.Combine(_testDir, "settings.json");
            var manager = new SettingsManager(filePath);

            var original = new AppSettings
            {
                SelectedClient = "WireSock",
                SelectedPort = "443",
                Mtu = "1360",
                ExcludeLan = true,
                EnableIpv6 = true,
                SelectedCountryFilter = "NL",
                SelectedServerId = "NL#42"
            };

            manager.Save(original);
            Assert.True(File.Exists(filePath));

            AppSettings loaded = manager.Load();
            Assert.NotNull(loaded);
            Assert.Equal("WireSock", loaded.SelectedClient);
            Assert.Equal("443", loaded.SelectedPort);
            Assert.Equal("1360", loaded.Mtu);
            Assert.True(loaded.ExcludeLan);
            Assert.True(loaded.EnableIpv6);
            Assert.Equal("NL", loaded.SelectedCountryFilter);
            Assert.Equal("NL#42", loaded.SelectedServerId);
        }

        [Fact]
        public void Load_CorruptedJson_FallsBackToDefaultSettingsWithoutThrowing()
        {
            string filePath = Path.Combine(_testDir, "corrupted.json");
            File.WriteAllText(filePath, "{ invalid json content ... ");

            var manager = new SettingsManager(filePath);
            AppSettings settings = manager.Load();

            Assert.NotNull(settings);
            Assert.Equal("AmneziaWG", settings.SelectedClient);
        }
    }
}
