using System;
using ProtonVpnGenerator.Services;
using Xunit;

namespace ProtonVpnGenerator.Tests
{
    public class QuicPacketGeneratorTests
    {
        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(63, 1)]
        [InlineData(64, 2)]
        [InlineData(16383, 2)]
        [InlineData(16384, 4)]
        [InlineData(1073741823, 4)]
        [InlineData(1073741824, 8)]
        public void QuicVarintLength_ReturnsCorrectByteLength(long input, int expectedLength)
        {
            int len = QuicPacketGenerator.QuicVarintLength(input);
            Assert.Equal(expectedLength, len);
        }

        [Theory]
        [InlineData(0, new byte[] { 0x00 })]
        [InlineData(37, new byte[] { 0x25 })]
        [InlineData(15293, new byte[] { 0x7b, 0xbd })] // (15293 >> 8) | 0x40 = 0x7b, 15293 & 0xFF = 0xbd
        [InlineData(494878333, new byte[] { 0x9d, 0x7f, 0x3e, 0x7d })] // RFC 9000 example
        public void QuicVarint_EncodesAccordingToRfc9000(long input, byte[] expectedBytes)
        {
            byte[] encoded = QuicPacketGenerator.QuicVarint(input);
            Assert.Equal(expectedBytes, encoded);
        }

        [Fact]
        public void GenerateI1FromDomain_ValidDomain_ReturnsFormattedHexByteString()
        {
            string domain = "example.com";
            string i1 = QuicPacketGenerator.GenerateI1FromDomain(domain);

            Assert.False(string.IsNullOrWhiteSpace(i1));
            Assert.StartsWith("<b 0x", i1);
            Assert.EndsWith(">", i1);

            // Extract hex content
            string hex = i1.Substring(5, i1.Length - 6);
            Assert.True(hex.Length > 100, "Generated encrypted QUIC initial packet hex must have substantial length");

            // Convert hex to bytes to verify it's valid hex encoding
            byte[] packetBytes = Convert.FromHexString(hex);
            Assert.True(packetBytes.Length >= 50);

            // QUIC Long Header Initial packet begins with flags (top bit 1: Long Header)
            Assert.True((packetBytes[0] & 0x80) != 0, "QUIC Initial packet must have Long Header bit set");
        }

        [Theory]
        [InlineData("protonvpn.com")]
        [InlineData("cloudflare.com")]
        [InlineData("gosuslugi.ru")]
        public void GenerateI1FromDomain_VariousDomains_Succeeds(string domain)
        {
            string i1 = QuicPacketGenerator.GenerateI1FromDomain(domain);
            Assert.StartsWith("<b 0x", i1);
            Assert.EndsWith(">", i1);
        }
    }
}
