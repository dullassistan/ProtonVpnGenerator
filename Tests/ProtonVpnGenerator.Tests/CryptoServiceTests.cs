using System;
using ProtonVpnGenerator.Services;
using Xunit;

namespace ProtonVpnGenerator.Tests
{
    public class CryptoServiceTests
    {
        [Fact]
        public void GenerateKeys_WgPrivateKey_Is32BytesAndClamped()
        {
            var (wgPrivKeyBase64, _) = CryptoService.GenerateKeys();

            byte[] key = Convert.FromBase64String(wgPrivKeyBase64);

            Assert.Equal(32, key.Length);
            // Curve25519 clamping: low 3 bits of byte[0] cleared,
            // high bit of byte[31] cleared, bit 6 of byte[31] set.
            Assert.Equal(0, key[0] & 0b0000_0111);
            Assert.Equal(0, key[31] & 0b1000_0000);
            Assert.Equal(0b0100_0000, key[31] & 0b0100_0000);
        }

        [Fact]
        public void GenerateKeys_PublicKey_HasPemEnvelopeAndEd25519Spki()
        {
            var (_, pem) = CryptoService.GenerateKeys();

            Assert.StartsWith("-----BEGIN PUBLIC KEY-----\n", pem);
            Assert.EndsWith("-----END PUBLIC KEY-----\n", pem);

            // Body decodes to a 44-byte Ed25519 SubjectPublicKeyInfo
            // (12-byte DER header + 32-byte key).
            string body = pem
                .Replace("-----BEGIN PUBLIC KEY-----\n", string.Empty)
                .Replace("\n-----END PUBLIC KEY-----\n", string.Empty)
                .Trim();
            byte[] spki = Convert.FromBase64String(body);
            Assert.Equal(44, spki.Length);
        }

        [Fact]
        public void GenerateKeys_ProducesDistinctKeysAcrossCalls()
        {
            var (priv1, pub1) = CryptoService.GenerateKeys();
            var (priv2, pub2) = CryptoService.GenerateKeys();

            Assert.NotEqual(priv1, priv2);
            Assert.NotEqual(pub1, pub2);
        }

        [Fact]
        public void GetRandomAwg2I1_AlwaysReturnsAPredefinedValue()
        {
            for (int i = 0; i < 200; i++)
            {
                string i1 = CryptoService.GetRandomAwg2I1();
                Assert.Contains(i1, CryptoService.PredefinedAwg2I1);
            }
        }

        [Fact]
        public void GetRandomWireSockDomain_AlwaysReturnsAKnownDomain()
        {
            for (int i = 0; i < 200; i++)
            {
                string domain = CryptoService.GetRandomWireSockDomain();
                Assert.Contains(domain, CryptoService.WireSockDomains);
            }
        }

        [Fact]
        public void GenerateRandomAwg1_ValuesAreWithinBoundsAndJmaxGreaterThanJmin()
        {
            for (int i = 0; i < 1000; i++)
            {
                var (jc, jmin, jmax) = CryptoService.GenerateRandomAwg1();

                Assert.InRange(jc, 1, 100);
                Assert.InRange(jmin, 1, 200);
                // jmax = Random.Next(jmin + 1, 202) => (jmin+1) .. 201
                Assert.InRange(jmax, jmin + 1, 201);
                Assert.True(jmax > jmin, $"jmax ({jmax}) must be greater than jmin ({jmin})");
            }
        }

        [Theory]
        [InlineData(0)] // cpa: min[5..49]  max[50..110]
        [InlineData(1)] // mha: min[5..24]  max[25..40]
        [InlineData(2)] // kt:  min[5..10]  max[11..25]
        [InlineData(3)] // rat: min[50..99] max[100..200]
        [InlineData(4)] // rkat:min[50..99] max[100..150]
        [InlineData(5)] // rt:  min[3..9]   max[10..15]
        public void GenerateRandomAwg3_EachRangeIsWellFormed(int fieldIndex)
        {
            (int minLow, int minHigh, int maxLow, int maxHigh) = fieldIndex switch
            {
                0 => (5, 49, 50, 110),
                1 => (5, 24, 25, 40),
                2 => (5, 10, 11, 25),
                3 => (50, 99, 100, 200),
                4 => (50, 99, 100, 150),
                _ => (3, 9, 10, 15),
            };

            for (int i = 0; i < 1000; i++)
            {
                var t = CryptoService.GenerateRandomAwg3();
                string raw = fieldIndex switch
                {
                    0 => t.cpa,
                    1 => t.mha,
                    2 => t.kt,
                    3 => t.rat,
                    4 => t.rkat,
                    _ => t.rt,
                };

                string[] parts = raw.Split('-');
                Assert.Equal(2, parts.Length);

                int min = int.Parse(parts[0]);
                int max = int.Parse(parts[1]);

                Assert.InRange(min, minLow, minHigh);
                Assert.InRange(max, maxLow, maxHigh);
                Assert.True(min < max, $"min ({min}) must be < max ({max}) for '{raw}'");
            }
        }
    }
}
