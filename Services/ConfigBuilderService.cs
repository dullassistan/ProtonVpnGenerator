using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using ProtonVpnGenerator.Models;

namespace ProtonVpnGenerator.Services
{
    public class ConfigBuilderService
    {
        public const string ExcludeLanSubnets =
            "1.0.0.0/8, 2.0.0.0/7, 4.0.0.0/6, 8.0.0.0/7, 11.0.0.0/8, 12.0.0.0/6, 16.0.0.0/4, 32.0.0.0/3, 64.0.0.0/3, 96.0.0.0/4, 112.0.0.0/5, 120.0.0.0/6, 124.0.0.0/7, 126.0.0.0/8, 128.0.0.0/3, 160.0.0.0/5, 168.0.0.0/8, 169.0.0.0/9, 169.128.0.0/10, 169.192.0.0/11, 169.224.0.0/12, 169.240.0.0/13, 169.248.0.0/14, 169.252.0.0/15, 169.255.0.0/16, 170.0.0.0/7, 172.0.0.0/12, 172.32.0.0/11, 172.64.0.0/10, 172.128.0.0/9, 173.0.0.0/8, 174.0.0.0/7, 176.0.0.0/4, 192.0.0.0/9, 192.128.0.0/11, 192.160.0.0/13, 192.169.0.0/16, 192.170.0.0/15, 192.172.0.0/14, 192.176.0.0/12, 192.192.0.0/10, 193.0.0.0/8, 194.0.0.0/7, 196.0.0.0/6, 200.0.0.0/5, 208.0.0.0/4, 224.0.0.0/4, ::/1, 8000::/2, c000::/3, e000::/4, f000::/5, f800::/6, fe00::/9, fec0::/10, ff00::/8";

        public string BuildConfigString(ProtonServer server, AppSettings settings, string wgPrivKeyBase64)
        {
            string selectedPort = string.IsNullOrWhiteSpace(settings.SelectedPort) ? "51820" : settings.SelectedPort;
            bool isClash = settings.SelectedClient.Equals("Clash", StringComparison.OrdinalIgnoreCase);
            bool isWiresock = settings.SelectedClient.Equals("WireSock", StringComparison.OrdinalIgnoreCase);
            string mtuVal = string.IsNullOrWhiteSpace(settings.Mtu) ? "1420" : settings.Mtu.Trim();

            // --- AWG 1.0 ---
            string jc = "3", jmin = "1", jmax = "3";
            var interfaceOptions = new StringBuilder();

            if (settings.IsAwg1)
            {
                if (settings.Awg1JunkPreset == 2)
                {
                    jc = "30"; jmin = "10"; jmax = "30";
                }
                else if (settings.Awg1JunkPreset == 3)
                {
                    jc = string.IsNullOrWhiteSpace(settings.Awg1Jc) ? "128" : settings.Awg1Jc.Trim();
                    jmin = string.IsNullOrWhiteSpace(settings.Awg1Jmin) ? "1279" : settings.Awg1Jmin.Trim();
                    jmax = string.IsNullOrWhiteSpace(settings.Awg1Jmax) ? "1280" : settings.Awg1Jmax.Trim();
                }

                interfaceOptions.Append($"\nS1 = 0\nS2 = 0\nS3 = 0\nS4 = 0\nJc = {jc}\nJmin = {jmin}\nJmax = {jmax}\nH1 = 1\nH2 = 2\nH3 = 3\nH4 = 4");
            }

            // --- AWG 2.0 ---
            string i1Val = "";
            if (settings.IsAwg2)
            {
                if (!isWiresock) // AWG or Clash
                {
                    i1Val = string.IsNullOrWhiteSpace(settings.Awg2I1)
                        ? CryptoService.PredefinedAwg2I1[0]
                        : settings.Awg2I1.Trim();

                    if (!string.IsNullOrEmpty(i1Val))
                        interfaceOptions.Append($"\nI1 = {i1Val}");

                    if (!string.IsNullOrWhiteSpace(settings.Awg2I2)) interfaceOptions.Append($"\nI2 = {settings.Awg2I2.Trim()}");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I3)) interfaceOptions.Append($"\nI3 = {settings.Awg2I3.Trim()}");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I4)) interfaceOptions.Append($"\nI4 = {settings.Awg2I4.Trim()}");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I5)) interfaceOptions.Append($"\nI5 = {settings.Awg2I5.Trim()}");
                }
                else // WireSock
                {
                    string idVal = string.IsNullOrWhiteSpace(settings.WireSockId) ? "apteka.ru" : settings.WireSockId.Trim();
                    string ipVal = string.IsNullOrWhiteSpace(settings.WireSockIp) ? "quic" : settings.WireSockIp.Trim();
                    string ibVal = string.IsNullOrWhiteSpace(settings.WireSockIb) ? "curl" : settings.WireSockIb.Trim();

                    interfaceOptions.Append($"\nId = {idVal}");
                    interfaceOptions.Append($"\nIp = {ipVal}");
                    interfaceOptions.Append($"\nIb = {ibVal}");
                }
            }

            // --- AWG 3.0 ---
            string cpa = settings.Awg3Cpa?.Trim() ?? "";
            string rkat = settings.Awg3Rkat?.Trim() ?? "";
            string rt = settings.Awg3Rt?.Trim() ?? "";
            string rat = settings.Awg3Rat?.Trim() ?? "";
            string kt = settings.Awg3Kt?.Trim() ?? "";
            string mha = settings.Awg3Mha?.Trim() ?? "";

            if (settings.IsAwg3)
            {
                if (!string.IsNullOrEmpty(cpa)) interfaceOptions.Append($"\nContentPaddingAddition = {cpa}");
                if (!string.IsNullOrEmpty(rkat)) interfaceOptions.Append($"\nRekeyAfterTime = {rkat}");
                if (!string.IsNullOrEmpty(rt)) interfaceOptions.Append($"\nRekeyTimeout = {rt}");
                if (!string.IsNullOrEmpty(rat)) interfaceOptions.Append($"\nRejectAfterTime = {rat}");
                if (!string.IsNullOrEmpty(kt)) interfaceOptions.Append($"\nKeepaliveTimeout = {kt}");
                if (!string.IsNullOrEmpty(mha)) interfaceOptions.Append($"\nMaxHandshakeAttempts = {mha}");
            }

            // --- AWG 3.1 ---
            if (settings.IsAwg31)
            {
                if (settings.Awg31RandomTrailers) interfaceOptions.Append("\nRandomTrailers = on");
                if (settings.Awg31DisableCookies) interfaceOptions.Append("\nDisableCookies = on");
            }

            string cleanName = server.Name.Replace("-FREE#", " ").Replace("_", " ").Trim();
            string flag = ProtonServer.GetFlagEmoji(server.ExitCountry);
            if (!string.IsNullOrEmpty(flag))
            {
                cleanName = $"{flag} {cleanName}";
            }

            // --- CLASH YAML FORMAT ---
            if (isClash)
            {
                var awgOptionsYaml = new StringBuilder();
                if (settings.IsAwg1)
                {
                    awgOptionsYaml.Append($"\n    jc: {jc}\n    jmin: {jmin}\n    jmax: {jmax}\n    s1: 0\n    s2: 0\n    h1: 1\n    h2: 2\n    h3: 3\n    h4: 4");
                }
                if (settings.IsAwg2)
                {
                    if (!string.IsNullOrEmpty(i1Val)) awgOptionsYaml.Append($"\n    i1: {i1Val}");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I2)) awgOptionsYaml.Append($"\n    i2: {settings.Awg2I2.Trim()}");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I3)) awgOptionsYaml.Append($"\n    i3: {settings.Awg2I3.Trim()}");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I4)) awgOptionsYaml.Append($"\n    i4: {settings.Awg2I4.Trim()}");
                    if (!string.IsNullOrWhiteSpace(settings.Awg2I5)) awgOptionsYaml.Append($"\n    i5: {settings.Awg2I5.Trim()}");
                }
                if (settings.IsAwg3)
                {
                    if (!string.IsNullOrEmpty(cpa)) awgOptionsYaml.Append($"\n    content-padding-addition: {cpa}");
                    if (!string.IsNullOrEmpty(rkat)) awgOptionsYaml.Append($"\n    rekey-after-time: {rkat}");
                    if (!string.IsNullOrEmpty(rt)) awgOptionsYaml.Append($"\n    rekey-timeout: {rt}");
                    if (!string.IsNullOrEmpty(rat)) awgOptionsYaml.Append($"\n    reject-after-time: {rat}");
                    if (!string.IsNullOrEmpty(kt)) awgOptionsYaml.Append($"\n    keepalive-timeout: {kt}");
                    if (!string.IsNullOrEmpty(mha)) awgOptionsYaml.Append($"\n    max-handshake-attempts: {mha}");
                }
                if (settings.IsAwg31)
                {
                    if (settings.Awg31RandomTrailers) awgOptionsYaml.Append("\n    random-trailers: true");
                    if (settings.Awg31DisableCookies) awgOptionsYaml.Append("\n    disable-cookies: true");
                }

                string amneziaBlock = awgOptionsYaml.Length > 0 ? $"\n  amnezia-wg-option:{awgOptionsYaml}" : "";
                string ipv6Line = settings.EnableIpv6 ? "\n  ipv6: 2a07:b944::2:2" : "";
                string dnsList = settings.EnableIpv6 ? "10.2.0.1, 2a07:b944::2:1" : "10.2.0.1";

                if (settings.ClashMode == "masque")
                {
                    return $@"proxies:
- name: ""{cleanName} (MASQUE)""
  type: masque
  server: {server.EntryIp}
  port: 443
  sni: {server.Domain ?? "protonvpn.net"}
  ip: 10.2.0.2{ipv6Line}
  private-key: {wgPrivKeyBase64}
  public-key: {server.PublicKey}
  udp: true
  mtu: {mtuVal}
  remote-dns-resolve: true
  dns: [{dnsList}]

proxy-groups:
- name: ProtonVPN
  type: select
  icon: https://res.cloudinary.com/dbulfrlrz/image/upload/v1703162849/static/logos/icons/vpn_f9embt.svg
  proxies:
    - ""{cleanName} (MASQUE)""
  url: 'http://speed.cloudflare.com/'
  interval: 300
rules:
- MATCH,ProtonVPN";
                }

                if (settings.ClashMode == "hybrid")
                {
                    return $@"proton-common: &proton-common
  type: wireguard
  ip: 10.2.0.2{ipv6Line}
  private-key: {wgPrivKeyBase64}
  udp: true
  mtu: {mtuVal}
  remote-dns-resolve: true
  dns: [{dnsList}]
  port: {selectedPort}{amneziaBlock}

proxies:
- name: ""{cleanName} (AWG)""
  <<: *proton-common
  server: {server.EntryIp}
  public-key: {server.PublicKey}

- name: ""{cleanName} (MASQUE)""
  type: masque
  server: {server.EntryIp}
  port: 443
  sni: {server.Domain ?? "protonvpn.net"}
  ip: 10.2.0.2{ipv6Line}
  private-key: {wgPrivKeyBase64}
  public-key: {server.PublicKey}
  udp: true
  mtu: {mtuVal}
  remote-dns-resolve: true
  dns: [{dnsList}]

proxy-groups:
- name: ProtonVPN
  type: fallback
  icon: https://res.cloudinary.com/dbulfrlrz/image/upload/v1703162849/static/logos/icons/vpn_f9embt.svg
  proxies:
    - ""{cleanName} (AWG)""
    - ""{cleanName} (MASQUE)""
  url: 'http://speed.cloudflare.com/'
  interval: 300
rules:
- MATCH,ProtonVPN";
                }

                return $@"proton: &proton
  type: wireguard
  ip: 10.2.0.2{ipv6Line}
  private-key: {wgPrivKeyBase64}
  udp: true
  mtu: {mtuVal}
  remote-dns-resolve: true
  dns: [{dnsList}]
  port: {selectedPort}{amneziaBlock}

proxies:
- name: ""{cleanName}""
  <<: *proton
  server: {server.EntryIp}
  public-key: {server.PublicKey}
    
proxy-groups:
- name: ProtonVPN
  type: select
  icon: https://res.cloudinary.com/dbulfrlrz/image/upload/v1703162849/static/logos/icons/vpn_f9embt.svg
  proxies:
    - ""{cleanName}""
  url: 'http://speed.cloudflare.com/'
  interval: 300
rules:
- MATCH,ProtonVPN";
            }

            // --- STANDARD WIREGUARD / AMNEZIAWG (.conf) ---
            string allowedIPs;
            if (settings.ExcludeLan)
            {
                allowedIPs = ExcludeLanSubnets;
            }
            else
            {
                allowedIPs = settings.EnableIpv6 ? "0.0.0.0/0, ::/0" : "0.0.0.0/0";
            }

            string address = settings.EnableIpv6 ? "10.2.0.2/32, 2a07:b944::2:2/128" : "10.2.0.2/32";
            string dns = settings.EnableIpv6 ? "10.2.0.1, 2a07:b944::2:1" : "10.2.0.1";

            string peerOptions = "";
            if (settings.PersistentKeepaliveEnabled)
            {
                string pkVal = string.IsNullOrWhiteSpace(settings.PersistentKeepaliveValue) ? "25" : settings.PersistentKeepaliveValue.Trim();
                peerOptions = $"\nPersistentKeepalive = {pkVal}";
            }

            return $@"[Interface]
PrivateKey = {wgPrivKeyBase64}
Address = {address}
DNS = {dns}
MTU = {mtuVal}{interfaceOptions}

[Peer]
# Server: {server.Name}
PublicKey = {server.PublicKey}
Endpoint = {server.EntryIp}:{selectedPort}
AllowedIPs = {allowedIPs}{peerOptions}";
        }

        public byte[] CreateCountriesZip(IEnumerable<ProtonServer> servers, AppSettings settings, string privKey)
        {
            var utf8WithoutBom = new UTF8Encoding(false);
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true, utf8WithoutBom))
            {
                bool isClash = settings.SelectedClient.Equals("Clash", StringComparison.OrdinalIgnoreCase);
                string ext = isClash ? "yaml" : "conf";

                var byCountry = servers.GroupBy(s => s.ExitCountry ?? "Unknown");
                foreach (var group in byCountry)
                {
                    var bestServer = group.OrderBy(s => s.Load).ThenBy(s => s.Name).FirstOrDefault();
                    if (bestServer != null)
                    {
                        string fileName = $"{bestServer.Name.Replace("-FREE#", "_")}.{ext}";
                        string configText = BuildConfigString(bestServer, settings, privKey).TrimStart('\uFEFF');

                        var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                        using var entryStream = entry.Open();
                        using var writer = new StreamWriter(entryStream, utf8WithoutBom);
                        writer.Write(configText);
                    }
                }
            }

            return memoryStream.ToArray();
        }

        public byte[] CreateAllZip(IEnumerable<ProtonServer> servers, AppSettings settings, string privKey)
        {
            var utf8WithoutBom = new UTF8Encoding(false);
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true, utf8WithoutBom))
            {
                bool isClash = settings.SelectedClient.Equals("Clash", StringComparison.OrdinalIgnoreCase);
                string ext = isClash ? "yaml" : "conf";

                foreach (var server in servers)
                {
                    string fileName = $"{server.Name.Replace("-FREE#", "_")}.{ext}";
                    string configText = BuildConfigString(server, settings, privKey).TrimStart('\uFEFF');

                    var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var writer = new StreamWriter(entryStream, utf8WithoutBom);
                    writer.Write(configText);
                }
            }

            return memoryStream.ToArray();
        }

        public static (string Extension, string Filter) GetConfigFileFormat(string? client)
        {
            bool isClash = client != null && client.Equals("Clash", StringComparison.OrdinalIgnoreCase);
            return isClash
                ? ("yaml", "Clash YAML (*.yaml)|*.yaml")
                : ("conf", "WireGuard Config (*.conf)|*.conf");
        }

        public static string GetConfigFileName(string cleanServerName, string? client, int downloadIndex = 0)
        {
            string ext = client != null && client.Equals("Clash", StringComparison.OrdinalIgnoreCase) ? "yaml" : "conf";
            return downloadIndex <= 0
                ? $"Proton_{cleanServerName}.{ext}"
                : $"Proton_{cleanServerName}_{downloadIndex}.{ext}";
        }
    }
}
