using System.IO;
using System.IO.Compression;
using System.Linq;
using ProtonVpnGenerator.Models;
using ProtonVpnGenerator.Services;
using Xunit;

namespace ProtonVpnGenerator.Tests
{
    public class ConfigBuilderServiceTests
    {
        private const string PrivKey = "cAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

        private static ProtonServer SampleServer(string name = "NL-FREE#1", string country = "NL")
            => new()
            {
                Id = "srv-1",
                Name = name,
                EntryIp = "185.159.157.1",
                ExitCountry = country,
                City = "Amsterdam",
                Load = 10,
                PublicKey = "SERVERPUBKEY0000000000000000000000000000000=",
                Domain = "node-nl-01.protonvpn.net",
            };

        private static AppSettings DefaultAwg() => new()
        {
            SelectedClient = "AmneziaWG",
            IsAwg2 = true,
        };

        [Fact]
        public void BuildConfig_AmneziaWg_ProducesWellFormedConfFile()
        {
            var cfg = new ConfigBuilderService()
                .BuildConfigString(SampleServer(), DefaultAwg(), PrivKey);

            Assert.Contains("[Interface]", cfg);
            Assert.Contains($"PrivateKey = {PrivKey}", cfg);
            Assert.Contains("Address = 10.2.0.2/32", cfg);
            Assert.Contains("DNS = 10.2.0.1", cfg);
            Assert.Contains("MTU = 1420", cfg);
            Assert.Contains("[Peer]", cfg);
            Assert.Contains("PublicKey = SERVERPUBKEY0000000000000000000000000000000=", cfg);
            Assert.Contains("Endpoint = 185.159.157.1:51820", cfg);
            Assert.Contains("AllowedIPs = 0.0.0.0/0", cfg);
            // IsAwg2 defaults on, so the first predefined I1 blob is injected.
            Assert.Contains("I1 = ", cfg);
        }

        [Fact]
        public void BuildConfig_ExcludeLan_UsesLanExclusionSubnets()
        {
            var settings = DefaultAwg();
            settings.ExcludeLan = true;

            var cfg = new ConfigBuilderService()
                .BuildConfigString(SampleServer(), settings, PrivKey);

            Assert.Contains($"AllowedIPs = {ConfigBuilderService.ExcludeLanSubnets}", cfg);
            Assert.DoesNotContain("AllowedIPs = 0.0.0.0/0\n", cfg);
        }

        [Fact]
        public void BuildConfig_EnableIpv6_AddsIpv6AddressDnsAndAllowedIPs()
        {
            var settings = DefaultAwg();
            settings.EnableIpv6 = true;

            var cfg = new ConfigBuilderService()
                .BuildConfigString(SampleServer(), settings, PrivKey);

            Assert.Contains("Address = 10.2.0.2/32, 2a07:b944::2:2/128", cfg);
            Assert.Contains("DNS = 10.2.0.1, 2a07:b944::2:1", cfg);
            Assert.Contains("AllowedIPs = 0.0.0.0/0, ::/0", cfg);
        }

        [Fact]
        public void BuildConfig_WireSock_EmitsIdIpIbAndOmitsI1()
        {
            var settings = DefaultAwg();
            settings.SelectedClient = "WireSock";

            var cfg = new ConfigBuilderService()
                .BuildConfigString(SampleServer(), settings, PrivKey);

            Assert.Contains("Id = apteka.ru", cfg);
            Assert.Contains("Ip = quic", cfg);
            Assert.Contains("Ib = curl", cfg);
            Assert.DoesNotContain("I1 = ", cfg);
        }

        [Fact]
        public void BuildConfig_CustomPortAndMtu_AreHonored()
        {
            var settings = DefaultAwg();
            settings.SelectedPort = "1234";
            settings.Mtu = "1500";

            var cfg = new ConfigBuilderService()
                .BuildConfigString(SampleServer(), settings, PrivKey);

            Assert.Contains("Endpoint = 185.159.157.1:1234", cfg);
            Assert.Contains("MTU = 1500", cfg);
        }

        [Fact]
        public void BuildConfig_PersistentKeepalive_IsAppendedToPeer()
        {
            var settings = DefaultAwg();
            settings.PersistentKeepaliveEnabled = true;

            var cfg = new ConfigBuilderService()
                .BuildConfigString(SampleServer(), settings, PrivKey);

            Assert.Contains("PersistentKeepalive = 25", cfg);
        }

        [Fact]
        public void BuildConfig_ClashDefault_ProducesWireguardYaml()
        {
            var settings = DefaultAwg();
            settings.SelectedClient = "Clash";
            settings.ClashMode = "awg";

            var cfg = new ConfigBuilderService()
                .BuildConfigString(SampleServer(), settings, PrivKey);

            Assert.Contains("type: wireguard", cfg);
            Assert.Contains("port: 51820", cfg);
            Assert.Contains("amnezia-wg-option:", cfg);
            Assert.Contains("i1: ", cfg);
            Assert.Contains("rules:", cfg);
            Assert.Contains("MATCH,ProtonVPN", cfg);
        }

        [Fact]
        public void BuildConfig_ClashMasque_ProducesMasqueYaml()
        {
            var settings = DefaultAwg();
            settings.SelectedClient = "Clash";
            settings.ClashMode = "masque";

            var cfg = new ConfigBuilderService()
                .BuildConfigString(SampleServer(), settings, PrivKey);

            Assert.Contains("type: masque", cfg);
            Assert.Contains("port: 443", cfg);
            Assert.Contains("sni: node-nl-01.protonvpn.net", cfg);
        }

        [Fact]
        public void BuildConfig_ClashHybrid_ContainsBothAwgAndMasqueProxies()
        {
            var settings = DefaultAwg();
            settings.SelectedClient = "Clash";
            settings.ClashMode = "hybrid";

            var cfg = new ConfigBuilderService()
                .BuildConfigString(SampleServer(), settings, PrivKey);

            Assert.Contains("(AWG)", cfg);
            Assert.Contains("(MASQUE)", cfg);
            Assert.Contains("type: fallback", cfg);
            Assert.Contains("type: masque", cfg);
        }

        [Fact]
        public void CreateAllZip_WritesOneConfEntryPerServer()
        {
            var servers = new[]
            {
                SampleServer("NL-FREE#1", "NL"),
                SampleServer("US-FREE#5", "US"),
            };

            byte[] zip = new ConfigBuilderService().CreateAllZip(servers, DefaultAwg(), PrivKey);

            using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
            Assert.Equal(2, archive.Entries.Count);
            Assert.All(archive.Entries, e => Assert.EndsWith(".conf", e.FullName));
            Assert.Contains(archive.Entries, e => e.FullName == "NL_1.conf");
            Assert.Contains(archive.Entries, e => e.FullName == "US_5.conf");
        }

        [Fact]
        public void CreateCountriesZip_KeepsOneEntryPerCountry_LowestLoadWins()
        {
            var servers = new[]
            {
                SampleServer("NL-FREE#1", "NL"),
                new ConfigBuilderTestServer("NL-FREE#2", "NL", load: 5).Server,
                SampleServer("US-FREE#5", "US"),
            };

            byte[] zip = new ConfigBuilderService().CreateCountriesZip(servers, DefaultAwg(), PrivKey);

            using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
            Assert.Equal(2, archive.Entries.Count);
            // Lower load (5) NL server should be the one kept.
            Assert.Contains(archive.Entries, e => e.FullName == "NL_2.conf");
            Assert.Contains(archive.Entries, e => e.FullName == "US_5.conf");
        }

        [Fact]
        public void CreateAllZip_ClashClient_WritesYamlEntries()
        {
            var settings = DefaultAwg();
            settings.SelectedClient = "Clash";

            byte[] zip = new ConfigBuilderService()
                .CreateAllZip(new[] { SampleServer() }, settings, PrivKey);

            using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
            Assert.All(archive.Entries, e => Assert.EndsWith(".yaml", e.FullName));
        }

        private sealed class ConfigBuilderTestServer
        {
            public ProtonServer Server { get; }

            public ConfigBuilderTestServer(string name, string country, int load)
            {
                Server = new ProtonServer
                {
                    Id = name,
                    Name = name,
                    EntryIp = "185.159.157.2",
                    ExitCountry = country,
                    City = "Amsterdam",
                    Load = load,
                    PublicKey = "SERVERPUBKEY0000000000000000000000000000000=",
                    Domain = "node-nl-02.protonvpn.net",
                };
            }
        }
    }
}
