using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AuditCkDayo.Models;
using Microsoft.Extensions.Configuration;

namespace AuditCkDayo.Services
{
    public interface ITreasuryAudioExportService
    {
        Task<byte[]> GenerateSpeechAsync(TreasuryCashFlow flow, CancellationToken cancellationToken = default);
        Task<byte[]> GenerateSpeechFromTextAsync(string spokenText, CancellationToken cancellationToken = default);
    }

    public sealed class UnconfiguredTreasuryAudioExportService : ITreasuryAudioExportService
    {
        public Task<byte[]> GenerateSpeechAsync(TreasuryCashFlow flow, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Treasury audio export service is not configured.");
        }

        public Task<byte[]> GenerateSpeechFromTextAsync(string spokenText, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Treasury audio export service is not configured.");
        }
    }

    public class TreasuryAudioExportService : ITreasuryAudioExportService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public TreasuryAudioExportService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        public async Task<byte[]> GenerateSpeechAsync(TreasuryCashFlow flow, CancellationToken cancellationToken = default)
        {
            var spokenText = BuildSpokenSummary(flow);
            return await GenerateSpeechFromTextAsync(spokenText, cancellationToken);
        }

        public async Task<byte[]> GenerateSpeechFromTextAsync(string spokenText, CancellationToken cancellationToken = default)
        {
            var customSpeechUrl = _configuration["GroqSettings:SpeechUrl"];
            if (!string.IsNullOrWhiteSpace(customSpeechUrl))
            {
                var groqKey = _configuration["GroqSettings:ApiKey"] ?? string.Empty;
                return await GenerateSpeechWithGroqAsync(spokenText, groqKey, cancellationToken);
            }

            try
            {
                return await GenerateSpeechFromGoogleTranslateTtsAsync(spokenText, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TTS] Google Translate TTS failed: {ex.Message}");
                var groqKey = _configuration["GroqSettings:ApiKey"] ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(groqKey))
                {
                    return await GenerateSpeechWithGroqAsync(spokenText, groqKey, cancellationToken);
                }
                throw;
            }
        }

        private async Task<byte[]> GenerateSpeechFromGoogleTranslateTtsAsync(string text, CancellationToken cancellationToken)
        {
            // Normalize numbers: "1,000.00 pesos" -> "1,000 pesos", "118,570.25 pesos" -> "118,570 pesos and 25 centavos"
            var sanitized = System.Text.RegularExpressions.Regex.Replace(text, @"\.00\s+pesos", " pesos", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\.(\d{2})\s+pesos", " pesos and $1 centavos", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            sanitized = sanitized.Replace("₱", " pesos ");

            // Split into sentence chunks under 170 characters
            var sentences = System.Text.RegularExpressions.Regex.Split(sanitized, @"(?<=[.?!;])\s+")
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            var chunks = new List<string>();
            var currentChunk = new StringBuilder();

            foreach (var sentence in sentences)
            {
                if (currentChunk.Length + sentence.Length + 1 < 170)
                {
                    if (currentChunk.Length > 0) currentChunk.Append(' ');
                    currentChunk.Append(sentence);
                }
                else
                {
                    if (currentChunk.Length > 0) chunks.Add(currentChunk.ToString());
                    currentChunk.Clear();
                    currentChunk.Append(sentence);
                }
            }
            if (currentChunk.Length > 0) chunks.Add(currentChunk.ToString());

            using var memoryStream = new MemoryStream();

            foreach (var chunk in chunks)
            {
                var encoded = Uri.EscapeDataString(chunk);
                var url = $"https://translate.google.com/translate_tts?ie=UTF-8&client=tw-ob&tl=en-ph&q={encoded}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var fallbackUrl = $"https://translate.google.com/translate_tts?ie=UTF-8&client=tw-ob&tl=en&q={encoded}";
                    using var fallbackReq = new HttpRequestMessage(HttpMethod.Get, fallbackUrl);
                    fallbackReq.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                    using var fallbackResp = await _httpClient.SendAsync(fallbackReq, cancellationToken);
                    fallbackResp.EnsureSuccessStatusCode();
                    var chunkBytes = await fallbackResp.Content.ReadAsByteArrayAsync(cancellationToken);
                    await memoryStream.WriteAsync(chunkBytes, 0, chunkBytes.Length, cancellationToken);
                }
                else
                {
                    var chunkBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    await memoryStream.WriteAsync(chunkBytes, 0, chunkBytes.Length, cancellationToken);
                }
            }

            return memoryStream.ToArray();
        }

        private async Task<byte[]> GenerateSpeechWithGroqAsync(string text, string apiKey, CancellationToken cancellationToken)
        {
            var speechUrl = _configuration["GroqSettings:SpeechUrl"] ?? "https://api.groq.com/openai/v1/audio/speech";
            var payload = new
            {
                model = _configuration["GroqSettings:SpeechModel"] ?? "canopylabs/orpheus-v1-english",
                voice = _configuration["GroqSettings:SpeechVoice"] ?? "troy",
                input = text,
                response_format = "mp3"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, speechUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Groq TTS failed ({response.StatusCode}): {error}");
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        public static byte[] PcmToWav(byte[] pcmData, int sampleRate = 24000, short channels = 1, short bitsPerSample = 16)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            int byteRate = sampleRate * channels * (bitsPerSample / 8);
            short blockAlign = (short)(channels * (bitsPerSample / 8));

            // RIFF chunk descriptor
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + pcmData.Length);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));

            // "fmt " sub-chunk
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // Subchunk1Size (16 for PCM)
            writer.Write((short)1); // AudioFormat (1 = PCM)
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            // "data" sub-chunk
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(pcmData.Length);
            writer.Write(pcmData);

            return stream.ToArray();
        }

        public static string BuildSpokenSummary(TreasuryCashFlow flow)
        {
            flow.RecomputeTotals();

            var cashIn = FormatGroups(flow, CashFlowDirection.In);
            var cashOut = FormatGroups(flow, CashFlowDirection.Out);

            var items = new List<string>
            {
                $"Treasury cash flow summary for {flow.CashFlowDate:MMMM dd, yyyy}.",
                $"Starting balance is {FormatAmount(flow.StartingBalance)} pesos.",
                $"Total cash in is {FormatAmount(flow.TotalCashIn)} pesos.",
                $"Total cash out is {FormatAmount(flow.TotalCashOut)} pesos.",
                $"Net cash flow is {FormatAmount(flow.NetCashFlow)} pesos.",
                $"Closing balance is {FormatAmount(flow.ClosingBalance)} pesos.",
                $"Cash in by group: {cashIn}.",
                $"Cash out by group: {cashOut}."
            };

            var inEntries = flow.Entries
                .Where(e => e.Direction == CashFlowDirection.In)
                .OrderBy(e => e.Id)
                .ToList();

            if (inEntries.Count > 0)
            {
                items.Add("Cash in entries one by one:");
                for (var i = 0; i < inEntries.Count; i++)
                {
                    items.Add(FormatSingleEntry(inEntries[i], i + 1));
                }
            }

            var outEntries = flow.Entries
                .Where(e => e.Direction == CashFlowDirection.Out)
                .OrderBy(e => e.Id)
                .ToList();

            if (outEntries.Count > 0)
            {
                items.Add("Cash out entries one by one:");
                for (var i = 0; i < outEntries.Count; i++)
                {
                    items.Add(FormatSingleEntry(outEntries[i], i + 1));
                }
            }

            return string.Join(" ", items);
        }

        private static string FormatSingleEntry(CashFlowEntry entry, int index)
        {
            var parts = new List<string>();
            parts.Add($"Item {index}: {SplitPascalCase(entry.Category.ToString())}");

            if (entry.Establishment != null && !string.IsNullOrWhiteSpace(entry.Establishment.Name))
            {
                parts.Add($"at {entry.Establishment.Name}");
            }

            if (entry.RelatedUser != null && !string.IsNullOrWhiteSpace(entry.RelatedUser.Name))
            {
                parts.Add($"for {entry.RelatedUser.Name}");
            }

            parts.Add($"{FormatAmount(entry.Amount)} pesos");

            if (!string.IsNullOrWhiteSpace(entry.Notes))
            {
                parts.Add($"remarks {entry.Notes.Trim()}");
            }

            return string.Join(", ", parts) + ".";
        }

        private static string FormatGroups(TreasuryCashFlow flow, CashFlowDirection direction)
        {
            var groups = flow.Entries
                .Where(e => e.Direction == direction)
                .GroupBy(e => e.Category)
                .OrderBy(g => (int)g.Key)
                .Select(g => $"{SplitPascalCase(g.Key.ToString())} {FormatAmount(g.Sum(e => e.Amount))} pesos")
                .ToList();

            return groups.Count == 0 ? "none" : string.Join("; ", groups);
        }

        private static string FormatAmount(decimal amount) => amount.ToString("N2");

        private static string SplitPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var builder = new StringBuilder(value.Length + 4);
            builder.Append(value[0]);
            for (var i = 1; i < value.Length; i++)
            {
                if (char.IsUpper(value[i]) && !char.IsWhiteSpace(value[i - 1]))
                {
                    builder.Append(' ');
                }
                builder.Append(value[i]);
            }

            return builder.ToString();
        }
    }
}
