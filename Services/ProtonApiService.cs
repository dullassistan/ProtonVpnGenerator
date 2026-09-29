using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ProtonVpnGenerator.Models;

namespace ProtonVpnGenerator.Services
{
    public class ProtonApiService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://proton-api.vercel.app";

        public ProtonApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public async Task<JsonElement> CreateSessionAsync()
        {
            var res = await _httpClient.PostAsJsonAsync("/api/proton/session", new { });
            var content = await res.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("ok", out var okProp) || !okProp.GetBoolean())
            {
                string error = doc.RootElement.TryGetProperty("error", out var errProp)
                    ? errProp.GetString() ?? "Ошибка создания сессии"
                    : "Неизвестная ошибка создания сессии";
                throw new Exception(error);
            }

            if (!doc.RootElement.TryGetProperty("session", out var sessionProp))
            {
                throw new Exception("Ответ API не содержит объекта session");
            }

            return sessionProp.Clone();
        }

        public async Task<List<ProtonServer>> GetServersAsync(JsonElement session)
        {
            var res = await _httpClient.PostAsJsonAsync("/api/proton/servers", new { session });
            var content = await res.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("ok", out var okProp) || !okProp.GetBoolean())
            {
                string error = doc.RootElement.TryGetProperty("error", out var errProp)
                    ? errProp.GetString() ?? "Ошибка загрузки серверов"
                    : "Неизвестная ошибка при загрузке серверов";
                throw new Exception(error);
            }

            if (!doc.RootElement.TryGetProperty("servers", out var serversProp))
            {
                throw new Exception("Ответ API не содержит списка серверов");
            }

            var servers = JsonSerializer.Deserialize<List<ProtonServer>>(serversProp.GetRawText());
            return servers ?? new List<ProtonServer>();
        }

        public async Task<JsonElement> RegisterCertificateAsync(JsonElement session, string pemPublicKey)
        {
            var res = await _httpClient.PostAsJsonAsync("/api/proton/certificate", new
            {
                session,
                clientPublicKey = pemPublicKey,
                persistent = true
            });
            var content = await res.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("ok", out var okProp) || !okProp.GetBoolean())
            {
                string error = doc.RootElement.TryGetProperty("error", out var errProp)
                    ? errProp.GetString() ?? "Ошибка регистрации сертификата"
                    : "Неизвестная ошибка при регистрации сертификата";
                throw new Exception(error);
            }

            return doc.RootElement.Clone();
        }
    }
}
