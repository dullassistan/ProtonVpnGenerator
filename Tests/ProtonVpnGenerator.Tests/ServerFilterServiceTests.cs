using System;
using System.Collections.Generic;
using System.Linq;
using ProtonVpnGenerator.Models;
using ProtonVpnGenerator.Services;
using Xunit;

namespace ProtonVpnGenerator.Tests
{
    public class ServerFilterServiceTests
    {
        private static ProtonServer CreateServer(string id, string name, string? country, int load)
        {
            return new ProtonServer
            {
                Id = id,
                Name = name,
                ExitCountry = country ?? string.Empty,
                EntryIp = "1.2.3.4",
                City = "TestCity",
                Domain = $"{name.ToLowerInvariant()}.protonvpn.net",
                Load = load,
                PublicKey = "testKey"
            };
        }

        [Fact]
        public void GroupByCountry_GroupsAndSortsByLoadThenName()
        {
            var servers = new List<ProtonServer>
            {
                CreateServer("1", "US-FREE#2", "US", 60),
                CreateServer("2", "US-FREE#1", "US", 20),
                CreateServer("3", "US-FREE#3", "US", 20),
                CreateServer("4", "NL-FREE#1", "NL", 15),
                CreateServer("5", "XX-UNKNOWN", null, 10),
                CreateServer("6", "YY-EMPTY", "  ", 50)
            };

            var groups = ServerFilterService.GroupByCountry(servers);

            Assert.Equal(3, groups.Count);
            Assert.True(groups.ContainsKey("US"));
            Assert.True(groups.ContainsKey("NL"));
            Assert.True(groups.ContainsKey("UNKNOWN"));

            // Check sorting in US: load 20 (US-FREE#1, then US-FREE#3), then load 60
            var usServers = groups["US"];
            Assert.Equal(3, usServers.Count);
            Assert.Equal("1", usServers[2].Id);
            Assert.Equal("US-FREE#1", usServers[0].Name);
            Assert.Equal("US-FREE#3", usServers[1].Name);
        }

        [Fact]
        public void GroupByCountry_HandlesNullListGracefully()
        {
            var groups = ServerFilterService.GroupByCountry(null!);
            Assert.NotNull(groups);
            Assert.Empty(groups);
        }

        [Fact]
        public void GetOrderedCountryCodes_PutsPreferredFirstThenAlphabetical()
        {
            var codes = new[] { "DE", "FR", "NL", "JP", "US", "AU", "PL" };
            var ordered = ServerFilterService.GetOrderedCountryCodes(codes);

            // Preferred order: US, PL, JP, CA, NO, RO, MX, SG, CH, NL
            // In our list: US (idx 0), PL (idx 1), JP (idx 2), NL (idx 9)
            // Followed by alphabetical: AU, DE, FR
            Assert.Equal(new[] { "US", "PL", "JP", "NL", "AU", "DE", "FR" }, ordered);
        }

        [Fact]
        public void GetOrderedCountryCodes_HandlesNullAndEmpty()
        {
            Assert.Empty(ServerFilterService.GetOrderedCountryCodes(null!));
            Assert.Empty(ServerFilterService.GetOrderedCountryCodes(Array.Empty<string>()));
        }

        [Fact]
        public void FilterServers_AllReturnsSortedList()
        {
            var servers = new List<ProtonServer>
            {
                CreateServer("1", "B", "US", 50),
                CreateServer("2", "A", "PL", 10),
                CreateServer("3", "C", "JP", 10)
            };
            var groups = ServerFilterService.GroupByCountry(servers);

            var filtered = ServerFilterService.FilterServers(servers, groups, "all");
            Assert.Equal(3, filtered.Count);
            Assert.Equal("A", filtered[0].Name);
            Assert.Equal("C", filtered[1].Name);
            Assert.Equal("B", filtered[2].Name);
        }

        [Fact]
        public void FilterServers_SpecificCountryReturnsOnlyThatCountry()
        {
            var servers = new List<ProtonServer>
            {
                CreateServer("1", "US-1", "US", 50),
                CreateServer("2", "PL-1", "PL", 10)
            };
            var groups = ServerFilterService.GroupByCountry(servers);

            var filtered = ServerFilterService.FilterServers(servers, groups, "PL");
            Assert.Single(filtered);
            Assert.Equal("PL-1", filtered[0].Name);
        }

        [Fact]
        public void FilterServers_NonExistentCountryReturnsEmpty()
        {
            var servers = new List<ProtonServer>
            {
                CreateServer("1", "US-1", "US", 50)
            };
            var groups = ServerFilterService.GroupByCountry(servers);

            var filtered = ServerFilterService.FilterServers(servers, groups, "ZZ");
            Assert.NotNull(filtered);
            Assert.Empty(filtered);
        }

        [Fact]
        public void GetPreferredInitialServer_SelectsMatchingIdOrFirst()
        {
            var servers = new List<ProtonServer>
            {
                CreateServer("1", "S1", "US", 10),
                CreateServer("2", "S2", "US", 20),
                CreateServer("3", "S3", "US", 30)
            };

            // Matching ID
            var preferred = ServerFilterService.GetPreferredInitialServer(servers, "2");
            Assert.NotNull(preferred);
            Assert.Equal("2", preferred.Id);

            // Non-matching ID -> fallback to first
            var fallback = ServerFilterService.GetPreferredInitialServer(servers, "non-existent");
            Assert.NotNull(fallback);
            Assert.Equal("1", fallback.Id);

            // Null/empty list
            Assert.Null(ServerFilterService.GetPreferredInitialServer(null, "1"));
            Assert.Null(ServerFilterService.GetPreferredInitialServer(new List<ProtonServer>(), "1"));
        }
    }
}
