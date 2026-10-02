using System;
using System.Security.Cryptography;
using System.Text;

namespace ProtonVpnGenerator.Services
{
    public static class QuicPacketGenerator
    {
        private static readonly byte[] QuicSalt = new byte[]
        {
            0x38, 0x76, 0x2c, 0xf7, 0xf5, 0x59, 0x34, 0xb3, 0x4d, 0x17,
            0x9a, 0xe6, 0xa4, 0xc8, 0x0c, 0xad, 0xcc, 0xbb, 0x7f, 0x0a
        };

        public static string GenerateI1FromDomain(string domain)
        {
            try
            {
                byte[] dcid = new byte[1];
                RandomNumberGenerator.Fill(dcid);
                byte[] scid = Array.Empty<byte>();
                byte[] token = Array.Empty<byte>();
                byte[] pkn = new byte[] { 0 };

                byte[] clientHello = BuildTlsClientHelloSniOnly(domain);
                byte[] cryptoFrame = BuildCryptoFrame(clientHello, 0);

                byte[] packet = BuildQuicInitial(dcid, scid, token, pkn, cryptoFrame, 0);
                return $"<b 0x{Convert.ToHexString(packet).ToLowerInvariant()}>";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating I1 from domain: {ex.Message}");
                return string.Empty;
            }
        }

        private static byte[] BuildTlsClientHelloSniOnly(string sni)
        {
            byte[] randomBytes = new byte[32];
            RandomNumberGenerator.Fill(randomBytes);

            byte[] sniBytes = Encoding.UTF8.GetBytes(sni);
            byte[] sniStr16 = new byte[2 + sniBytes.Length];
            sniStr16[0] = (byte)(sniBytes.Length >> 8);
            sniStr16[1] = (byte)(sniBytes.Length & 0xFF);
            Buffer.BlockCopy(sniBytes, 0, sniStr16, 2, sniBytes.Length);

            int extInnerLen = sniStr16.Length + 1;
            byte[] extBuffer = new byte[3 + sniStr16.Length];
            extBuffer[0] = (byte)(extInnerLen >> 8);
            extBuffer[1] = (byte)(extInnerLen & 0xFF);
            extBuffer[2] = 0x00; // host_name type
            Buffer.BlockCopy(sniStr16, 0, extBuffer, 3, sniStr16.Length);

            byte[] sniExt = new byte[4 + extBuffer.Length];
            sniExt[0] = 0; // code 0 (SNI)
            sniExt[1] = 0;
            sniExt[2] = (byte)(extBuffer.Length >> 8);
            sniExt[3] = (byte)(extBuffer.Length & 0xFF);
            Buffer.BlockCopy(extBuffer, 0, sniExt, 4, extBuffer.Length);

            byte[] extensionsBlock = new byte[2 + sniExt.Length];
            extensionsBlock[0] = (byte)(sniExt.Length >> 8);
            extensionsBlock[1] = (byte)(sniExt.Length & 0xFF);
            Buffer.BlockCopy(sniExt, 0, extensionsBlock, 2, sniExt.Length);

            byte[] payload = new byte[4 + 2 + 32 + 4 + extensionsBlock.Length];
            int offset = 4;
            payload[offset++] = 0x03;
            payload[offset++] = 0x03;
            Buffer.BlockCopy(randomBytes, 0, payload, offset, 32);
            offset += 32;
            payload[offset++] = 0; // legacy_session_id length
            payload[offset++] = 0; // cipher_suites length hi
            payload[offset++] = 0; // cipher_suites length lo
            payload[offset++] = 0; // legacy_compression_methods length
            Buffer.BlockCopy(extensionsBlock, 0, payload, offset, extensionsBlock.Length);

            int bodyLength = payload.Length - 4;
            payload[0] = 0x01; // ClientHello type
            payload[1] = (byte)((bodyLength >> 16) & 0xFF);
            payload[2] = (byte)((bodyLength >> 8) & 0xFF);
            payload[3] = (byte)(bodyLength & 0xFF);

            return payload;
        }

        private static byte[] BuildCryptoFrame(byte[] data, int offset)
        {
            byte[] offsetVarint = QuicVarint(offset);
            byte[] lengthVarint = QuicVarint(data.Length);

            byte[] frame = new byte[1 + offsetVarint.Length + lengthVarint.Length + data.Length];
            frame[0] = 0x06; // CRYPTO frame type
            int pos = 1;
            Buffer.BlockCopy(offsetVarint, 0, frame, pos, offsetVarint.Length);
            pos += offsetVarint.Length;
            Buffer.BlockCopy(lengthVarint, 0, frame, pos, lengthVarint.Length);
            pos += lengthVarint.Length;
            Buffer.BlockCopy(data, 0, frame, pos, data.Length);
            return frame;
        }

        private static byte[] BuildQuicInitial(byte[] dcid, byte[] scid, byte[] token, byte[] pkn, byte[] payload, int padto)
        {
            int pknLen = pkn.Length;
            int tagLen = 16;
            int baseHeaderLen = 8 + dcid.Length + scid.Length + token.Length + pknLen;
            int paddingLength = 0;

            int GetLengthByteSize() => QuicVarintLength(pknLen + payload.Length + paddingLength + tagLen);
            int GetOverallLength() => baseHeaderLen + GetLengthByteSize() + payload.Length + paddingLength + tagLen;

            int overallLength = GetOverallLength();
            if (overallLength < padto)
            {
                paddingLength = padto - overallLength;
                while (paddingLength > 0 && GetOverallLength() > padto) paddingLength--;
                if (GetOverallLength() < padto) paddingLength++;
                overallLength = GetOverallLength();
            }
            if (pknLen + payload.Length + paddingLength + tagLen < 20)
            {
                paddingLength = 20 - pknLen - payload.Length - tagLen;
                overallLength = GetOverallLength();
            }

            byte[] lengthVarint = QuicVarint(pknLen + payload.Length + paddingLength + tagLen);
            byte[] dcidStr = QuicStr8(dcid);
            byte[] scidStr = QuicStr8(scid);
            byte[] tokenStr = QuicStr8(token);

            byte[] headerPrefix = new byte[] { (byte)(0xC0 | (pknLen - 1)), 0, 0, 0, 1 };
            byte[] header = Concat(headerPrefix, dcidStr, scidStr, tokenStr, lengthVarint, pkn);

            byte[] initSecret = HMACSHA256.HashData(QuicSalt, dcid);
            byte[] clientSecret = QuicDeriveSecret(initSecret, 32, "client in", "");
            byte[] quicKey = QuicDeriveSecret(clientSecret, 16, "quic key", "");
            byte[] quicIv = QuicDeriveSecret(clientSecret, 12, "quic iv", "");
            byte[] quicHp = QuicDeriveSecret(clientSecret, 16, "quic hp", "");

            byte[] nonce = (byte[])quicIv.Clone();
            for (int i = 0; i < pknLen; i++)
            {
                nonce[12 - pknLen + i] ^= pkn[i];
            }

            byte[] paddedPayload = new byte[payload.Length + paddingLength];
            Buffer.BlockCopy(payload, 0, paddedPayload, 0, payload.Length);

            byte[] ciphertext = new byte[paddedPayload.Length];
            byte[] tag = new byte[16];
            using (var aesGcm = new AesGcm(quicKey, 16))
            {
                aesGcm.Encrypt(nonce, paddedPayload, ciphertext, tag, header);
            }
            byte[] encryptedPayload = Concat(ciphertext, tag);

            byte[] sample = new byte[16];
            Buffer.BlockCopy(encryptedPayload, 4 - pknLen, sample, 0, 16);

            byte[] mask = new byte[16];
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.Key = quicHp;
                using var encryptor = aes.CreateEncryptor();
                encryptor.TransformBlock(sample, 0, 16, mask, 0);
            }

            header[0] ^= (byte)(mask[0] & 0x0F);
            for (int i = 0; i < pknLen; i++)
            {
                header[header.Length - pknLen + i] ^= mask[1 + i];
            }

            return Concat(header, encryptedPayload);
        }

        private static byte[] QuicDeriveSecret(byte[] key, int length, string label, string context)
        {
            byte[] labelBytes = QuicStr8("tls13 " + label);
            byte[] contextBytes = QuicStr8(context);

            byte[] info = new byte[2 + labelBytes.Length + contextBytes.Length + 1];
            info[0] = (byte)(length >> 8);
            info[1] = (byte)(length & 0xFF);
            Buffer.BlockCopy(labelBytes, 0, info, 2, labelBytes.Length);
            Buffer.BlockCopy(contextBytes, 0, info, 2 + labelBytes.Length, contextBytes.Length);
            info[info.Length - 1] = 0x01;

            byte[] hmac = HMACSHA256.HashData(key, info);
            byte[] result = new byte[length];
            Buffer.BlockCopy(hmac, 0, result, 0, length);
            return result;
        }

        private static byte[] QuicStr8(string s)
        {
            if (string.IsNullOrEmpty(s)) return new byte[] { 0 };
            byte[] b = Encoding.UTF8.GetBytes(s);
            byte[] res = new byte[b.Length + 1];
            res[0] = (byte)b.Length;
            Buffer.BlockCopy(b, 0, res, 1, b.Length);
            return res;
        }

        private static byte[] QuicStr8(byte[] b)
        {
            if (b == null || b.Length == 0) return new byte[] { 0 };
            byte[] res = new byte[b.Length + 1];
            res[0] = (byte)b.Length;
            Buffer.BlockCopy(b, 0, res, 1, b.Length);
            return res;
        }

        internal static byte[] QuicVarint(long x)
        {
            if (x < 0x40)
            {
                return new byte[] { (byte)x };
            }
            else if (x < 0x4000)
            {
                return new byte[] { (byte)((x >> 8) | 0x40), (byte)(x & 0xFF) };
            }
            else if (x < 0x40000000)
            {
                return new byte[]
                {
                    (byte)((x >> 24) | 0x80),
                    (byte)((x >> 16) & 0xFF),
                    (byte)((x >> 8) & 0xFF),
                    (byte)(x & 0xFF)
                };
            }
            else
            {
                byte[] res = new byte[8];
                for (int i = 7; i >= 0; i--)
                {
                    res[i] = (byte)(x & 0xFF);
                    x >>= 8;
                }
                res[0] |= 0xC0;
                return res;
            }
        }

        internal static int QuicVarintLength(long x)
        {
            if (x < 0x40) return 1;
            if (x < 0x4000) return 2;
            if (x < 0x40000000) return 4;
            return 8;
        }

        private static byte[] Concat(params byte[][] arrays)
        {
            int total = 0;
            foreach (var a in arrays) total += a.Length;
            byte[] res = new byte[total];
            int pos = 0;
            foreach (var a in arrays)
            {
                Buffer.BlockCopy(a, 0, res, pos, a.Length);
                pos += a.Length;
            }
            return res;
        }
    }
}
