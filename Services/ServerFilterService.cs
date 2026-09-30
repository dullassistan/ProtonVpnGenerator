using System;
using System.Collections.Generic;
using System.Linq;
using ProtonVpnGenerator.Models;

namespace ProtonVpnGenerator.Services
{
    /// <summary>
    /// Сервис группировки, фильтрации и приоритизации серверов ProtonVPN.
    /// </summary>
    public static class ServerFilterService
    {
        public static readonly IReadOnlyList<string> PreferredCountryOrder = new[]
        {
            "US", "PL", "JP", "CA", "NO", "RO", "MX", "SG", "CH", "NL"
        };

        /// <summary>
        /// Группирует серверы по коду страны, сортируя серверы внутри каждой группы по возрастанию нагрузки (Load), затем по имени.
        /// </summary>
        public static Dictionary<string, List<ProtonServer>> GroupByCountry(IEnumerable<ProtonServer> servers)
        {
            if (servers == null) return new Dictionary<string, List<ProtonServer>>(StringComparer.OrdinalIgnoreCase);

            return servers
                .GroupBy(s => string.IsNullOrWhiteSpace(s.ExitCountry) ? "Unknown" : s.ExitCountry.Trim().ToUpperInvariant())
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(s => s.Load).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Сортирует список кодов стран: приоритетные страны в начале, остальные — по алфавиту.
        /// </summary>
        public static List<string> GetOrderedCountryCodes(IEnumerable<string> countryCodes)
        {
            if (countryCodes == null) return new List<string>();

            return countryCodes
                .OrderBy(c =>
                {
                    for (int i = 0; i < PreferredCountryOrder.Count; i++)
                    {
                        if (string.Equals(PreferredCountryOrder[i], c, StringComparison.OrdinalIgnoreCase))
                            return i;
                    }
                    return 999;
                })
                .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Фильтрует серверы по выбранной стране ("all" или двухбуквенный код страны).
        /// Результат всегда отсортирован по возрастанию нагрузки (Load), затем по имени.
        /// </summary>
        public static List<ProtonServer> FilterServers(
            IEnumerable<ProtonServer> allServers,
            IReadOnlyDictionary<string, List<ProtonServer>> serversByCountry,
            string? countryFilter)
        {
            if (string.IsNullOrWhiteSpace(countryFilter) ||
                string.Equals(countryFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                return (allServers ?? Enumerable.Empty<ProtonServer>())
                    .OrderBy(s => s.Load)
                    .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (serversByCountry != null && serversByCountry.TryGetValue(countryFilter, out var list) && list != null)
            {
                return list;
            }

            return new List<ProtonServer>();
        }

        /// <summary>
        /// Выбирает сервер для автовыделения: сохранённый ранее (если есть в отфильтрованном списке)
        /// либо наименее загруженный (первый в списке).
        /// </summary>
        public static ProtonServer? GetPreferredInitialServer(IReadOnlyList<ProtonServer>? servers, string? preferredServerId = null)
        {
            if (servers == null || servers.Count == 0) return null;

            if (!string.IsNullOrWhiteSpace(preferredServerId))
            {
                var match = servers.FirstOrDefault(s => string.Equals(s.Id, preferredServerId, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            return servers[0];
        }
    }
}
