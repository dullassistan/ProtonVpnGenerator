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
            JsonElement root = await PostAndValidateAsync("/api/proton/session", new { }, "Создание сессии");

            if (!root.TryGetProperty("session", out var sessionProp))
            {
                throw new Exception("Ответ API не содержит объекта session");
            }

            return sessionProp.Clone();
        }

        public async Task<List<ProtonServer>> GetServersAsync(JsonElement session)
        {
            JsonElement root = await PostAndValidateAsync("/api/proton/servers", new { session }, "Загрузка серверов");

            if (!root.TryGetProperty("servers", out var serversProp))
            {
                throw new Exception("Ответ API не содержит списка серверов");
            }

            var servers = JsonSerializer.Deserialize<List<ProtonServer>>(serversProp.GetRawText());
            return servers ?? new List<ProtonServer>();
        }

        public async Task<JsonElement> RegisterCertificateAsync(JsonElement session, string pemPublicKey)
        {
            return await PostAndValidateAsync("/api/proton/certificate", new
            {
                session,
                clientPublicKey = pemPublicKey,
                persistent = true
            }, "Регистрация сертификата");
        }

        /// <summary>
        /// Отправляет POST-запрос, устойчиво разбирает ответ и проверяет флаг <c>ok</c>.
        /// Возвращает клон корневого JSON-элемента (безопасен после освобождения документа).
        /// Транслирует таймаут, сетевые сбои и не-JSON ответы в понятные сообщения.
        /// </summary>
        private async Task<JsonElement> PostAndValidateAsync(string path, object body, string operation)
        {
            HttpResponseMessage response;
            string content;
            try
            {
                response = await _httpClient.PostAsJsonAsync(path, body);
                content = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException)
            {
                throw new Exception($"{operation}: превышено время ожидания ответа сервера ({_httpClient.Timeout.TotalSeconds:0} с). Проверьте подключение к интернету и повторите попытку.");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"{operation}: не удалось соединиться с API ({BaseUrl}). {ex.Message}");
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(content);
            }
            catch (JsonException)
            {
                throw new Exception($"{operation}: сервер вернул неожиданный ответ (HTTP {(int)response.StatusCode} {response.StatusCode}). {DescribeBody(content)}");
            }

            using (doc)
            {
                JsonElement root = doc.RootElement;

                bool ok = root.TryGetProperty("ok", out var okProp) && okProp.ValueKind == JsonValueKind.True;
                if (!ok)
                {
                    string error = root.TryGetProperty("error", out var errProp) && errProp.ValueKind == JsonValueKind.String
                        ? errProp.GetString() ?? $"{operation}: ошибка"
                        : $"{operation}: неизвестная ошибка (HTTP {(int)response.StatusCode} {response.StatusCode}).";
                    throw new Exception(error);
                }

                return root.Clone();
            }
        }

        /// <summary>Формирует короткое безопасное описание тела ответа для сообщения об ошибке.</summary>
        private static string DescribeBody(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return "Пустое тело ответа.";
            }

            string snippet = content.Trim().Replace("\r", " ").Replace("\n", " ");
            const int maxLength = 200;
            if (snippet.Length > maxLength)
            {
                snippet = snippet.Substring(0, maxLength) + "…";
            }

            return $"Начало ответа: {snippet}";
        }
    }
}
