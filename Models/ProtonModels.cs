using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProtonVpnGenerator.Models
{
    public class ProtonApiResponse<T>
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("session")]
        public JsonElement? SessionRaw { get; set; }

        [JsonPropertyName("servers")]
        public List<ProtonServer>? Servers { get; set; }

        [JsonPropertyName("certificate")]
        public ProtonCertificate? Certificate { get; set; }
    }

    public class ProtonCertificate
    {
        [JsonPropertyName("value")]
        public string? Value { get; set; }

        [JsonPropertyName("expirationTime")]
        public long ExpirationTime { get; set; }

        [JsonPropertyName("refreshTime")]
        public long RefreshTime { get; set; }
    }

    public class ProtonServer
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("entryIp")]
        public string EntryIp { get; set; } = string.Empty;

        [JsonPropertyName("exitCountry")]
        public string ExitCountry { get; set; } = string.Empty;

        [JsonPropertyName("city")]
        public string City { get; set; } = string.Empty;

        [JsonPropertyName("load")]
        public int Load { get; set; }

        [JsonPropertyName("publicKey")]
        public string PublicKey { get; set; } = string.Empty;

        [JsonPropertyName("domain")]
        public string? Domain { get; set; }

        // Display properties
        [JsonIgnore]
        public string CleanName => Name.Replace("-FREE#", "_");

        [JsonIgnore]
        public string LoadIndicator => Load switch
        {
            < 30 => "🟢",
            < 60 => "🟡",
            < 90 => "🟠",
            _ => "🔴"
        };

        [JsonIgnore]
        public string LoadColor => Load switch
        {
            < 30 => "#10B981",
            < 60 => "#F59E0B",
            < 90 => "#F97316",
            _ => "#EF4444"
        };

        [JsonIgnore]
        public string CountryTitle => GetCountryName(ExitCountry);

        [JsonIgnore]
        public string DisplayText => $"{CleanName} ({City}) [{Load}%]";

        private static readonly Dictionary<string, string> CountryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "CA", "Канада" },
            { "CH", "Швейцария" },
            { "JP", "Япония" },
            { "MX", "Мексика" },
            { "NL", "Нидерланды" },
            { "NO", "Норвегия" },
            { "PL", "Польша" },
            { "RO", "Румыния" },
            { "SG", "Сингапур" },
            { "US", "США" },
            { "DE", "Германия" },
            { "FR", "Франция" },
            { "GB", "Великобритания" },
            { "SE", "Швеция" },
            { "FI", "Финляндия" },
            { "IT", "Италия" },
            { "ES", "Испания" },
            { "BR", "Бразилия" },
            { "AU", "Австралия" }
        };

        public static string GetCountryName(string countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode)) return "Все";
            if (CountryNames.TryGetValue(countryCode, out var name))
                return name;
            return countryCode.ToUpperInvariant();
        }

        public static string GetCountryChipLabel(string countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Equals("all", StringComparison.OrdinalIgnoreCase))
                return "Все страны";
            string code = countryCode.ToUpperInvariant();
            if (CountryNames.TryGetValue(countryCode, out var name))
                return $"{code} {name}";
            return code;
        }

        public static string GetCountryFullLabel(string countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Equals("all", StringComparison.OrdinalIgnoreCase))
                return "Все страны";
            string code = countryCode.ToUpperInvariant();
            if (CountryNames.TryGetValue(countryCode, out var name))
                return $"{code} — {name}";
            return code;
        }

        public static string GetFlagEmoji(string countryCode)
        {
            // Return empty string to prevent Windows font renderer showing square boxes []
            return string.Empty;
        }
    }
}
