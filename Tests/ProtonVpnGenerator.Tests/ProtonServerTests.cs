using ProtonVpnGenerator.Models;
using Xunit;

namespace ProtonVpnGenerator.Tests
{
    public class ProtonServerTests
    {
        [Theory]
        [InlineData("", "Все")]
        [InlineData("   ", "Все")]
        [InlineData("US", "США")]
        [InlineData("us", "США")]      // case-insensitive lookup
        [InlineData("DE", "Германия")]
        [InlineData("XX", "XX")]        // unknown -> upper-cased code
        [InlineData("zz", "ZZ")]
        public void GetCountryName_MapsKnownCodesAndFallsBack(string code, string expected)
            => Assert.Equal(expected, ProtonServer.GetCountryName(code));

        [Theory]
        [InlineData("", "Все страны")]
        [InlineData("all", "Все страны")]
        [InlineData("ALL", "Все страны")]
        [InlineData("US", "US США")]
        [InlineData("de", "DE Германия")]
        [InlineData("xx", "XX")]
        public void GetCountryChipLabel_FormatsCodeAndName(string code, string expected)
            => Assert.Equal(expected, ProtonServer.GetCountryChipLabel(code));

        [Theory]
        [InlineData("all", "Все страны")]
        [InlineData("US", "US — США")]
        [InlineData("fr", "FR — Франция")]
        [InlineData("zz", "ZZ")]
        public void GetCountryFullLabel_UsesEmDashSeparator(string code, string expected)
            => Assert.Equal(expected, ProtonServer.GetCountryFullLabel(code));

        [Theory]
        [InlineData("US")]
        [InlineData("")]
        [InlineData("DE")]
        public void GetFlagEmoji_AlwaysEmpty_ToAvoidTofuBoxes(string code)
            => Assert.Equal(string.Empty, ProtonServer.GetFlagEmoji(code));

        [Fact]
        public void CleanName_ReplacesFreeMarkerWithUnderscore()
        {
            var s = new ProtonServer { Name = "US-FREE#3" };
            Assert.Equal("US_3", s.CleanName);
        }

        [Theory]
        [InlineData(0, "🟢")]
        [InlineData(29, "🟢")]
        [InlineData(30, "🟡")]  // boundary: not < 30
        [InlineData(59, "🟡")]
        [InlineData(60, "🟠")]
        [InlineData(89, "🟠")]
        [InlineData(90, "🔴")]
        [InlineData(100, "🔴")]
        public void LoadIndicator_BucketsByLoad(int load, string expected)
        {
            var s = new ProtonServer { Load = load };
            Assert.Equal(expected, s.LoadIndicator);
        }

        [Theory]
        [InlineData(10, "#10B981")]
        [InlineData(45, "#F59E0B")]
        [InlineData(75, "#F97316")]
        [InlineData(95, "#EF4444")]
        public void LoadColor_MatchesIndicatorBuckets(int load, string expected)
        {
            var s = new ProtonServer { Load = load };
            Assert.Equal(expected, s.LoadColor);
        }

        [Fact]
        public void CountryTitle_DelegatesToGetCountryName()
        {
            var s = new ProtonServer { ExitCountry = "FR" };
            Assert.Equal("Франция", s.CountryTitle);
        }

        [Fact]
        public void DisplayText_CombinesCleanNameCityAndLoad()
        {
            var s = new ProtonServer { Name = "NL-FREE#1", City = "Amsterdam", Load = 10 };
            Assert.Equal("NL_1 (Amsterdam) [10%]", s.DisplayText);
        }
    }
}
