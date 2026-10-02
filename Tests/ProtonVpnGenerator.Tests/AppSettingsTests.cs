using System;
using ProtonVpnGenerator.Models;
using Xunit;

namespace ProtonVpnGenerator.Tests
{
    public class AppSettingsTests
    {
        [Fact]
        public void HasValidSession_EmptyOrNullSessionJson_ReturnsFalse()
        {
            var settings = new AppSettings
            {
                CachedSessionJson = null,
                SessionExpiresMs = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds()
            };

            Assert.False(settings.HasValidSession());

            settings.CachedSessionJson = "   ";
            Assert.False(settings.HasValidSession());
        }

        [Fact]
        public void HasValidSession_ExpiredSession_ReturnsFalse()
        {
            long pastMs = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeMilliseconds();
            var settings = new AppSettings
            {
                CachedSessionJson = "{\"token\":\"abc\"}",
                SessionExpiresMs = pastMs
            };

            Assert.False(settings.HasValidSession());
        }

        [Fact]
        public void HasValidSession_ValidFutureSession_ReturnsTrue()
        {
            long futureMs = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeMilliseconds();
            var settings = new AppSettings
            {
                CachedSessionJson = "{\"token\":\"xyz\"}",
                SessionExpiresMs = futureMs
            };

            Assert.True(settings.HasValidSession());
            Assert.True(settings.HasValidSession(nowMs: futureMs - 1000));
            Assert.False(settings.HasValidSession(nowMs: futureMs + 1000));
        }
    }
}
