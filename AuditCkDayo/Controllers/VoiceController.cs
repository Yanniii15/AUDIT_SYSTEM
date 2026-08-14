using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AuditCkDayo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace AuditCkDayo.Controllers
{
    [Authorize(Roles = "Owner")]
    public class VoiceController : Controller
    {
        private readonly VoiceBiService _biService;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public VoiceController(VoiceBiService biService, IConfiguration configuration, HttpClient httpClient)
        {
            _biService = biService;
            _configuration = configuration;
            _httpClient = httpClient;
        }

        [HttpGet]
        public IActionResult Query()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadQuery(IFormFile audioFile)
        {
            if (audioFile == null || audioFile.Length == 0)
            {
                return BadRequest(new { error = "No audio file provided." });
            }

            var apiKey = _configuration["GroqSettings:ApiKey"] ?? "";
            var whisperUrl = _configuration["GroqSettings:WhisperUrl"] ?? "";
            var chatUrl = _configuration["GroqSettings:ChatUrl"] ?? "";

            if (string.IsNullOrWhiteSpace(whisperUrl) || !whisperUrl.StartsWith("http"))
            {
                whisperUrl = "https://api.groq.com/openai/v1/audio/transcriptions";
            }
            if (string.IsNullOrWhiteSpace(chatUrl) || !chatUrl.StartsWith("http"))
            {
                chatUrl = "https://api.groq.com/openai/v1/chat/completions";
            }

            // 1. Transcribe the audio file via Groq Whisper API
            string transcribedText = "";
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, whisperUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                using var content = new MultipartFormDataContent();
                
                using var fileStream = audioFile.OpenReadStream();
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(audioFile.ContentType ?? "audio/wav");
                
                content.Add(fileContent, "file", audioFile.FileName ?? "query.wav");
                content.Add(new StringContent("whisper-large-v3"), "model");

                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new { error = $"Whisper transcription failed: {errorMsg}" });
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                if (doc.RootElement.TryGetProperty("text", out var textProp))
                {
                    transcribedText = textProp.GetString() ?? "";
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Transcription error: {ex.Message}" });
            }

            if (string.IsNullOrWhiteSpace(transcribedText))
            {
                return BadRequest(new { error = "Could not transcribe audio content." });
            }

            // 2. Fetch the read-only P&L data snapshot from VoiceBiService
            string biJson = "";
            try
            {
                // We default to current month for the query
                var today = DateTime.Today;
                var startDate = new DateTime(today.Year, today.Month, 1);
                var endDate = today;
                biJson = await _biService.GetPnlSummaryJsonAsync(startDate, endDate);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to fetch BI data snapshot: {ex.Message}" });
            }

            // 3. Pipe everything to Google AI Studio Gemma 2 API
            try
            {
                var googleApiKey = _configuration["GoogleGemini:ApiKey"] ?? "";
                if (string.IsNullOrEmpty(googleApiKey) || googleApiKey == "YOUR_GEMINI_API_KEY")
                {
                    googleApiKey = apiKey;
                }

                var googleChatUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent?key={googleApiKey}";

                var prompt = "System Prompt: You are a helpful business voice assistant. Answer the user's question about the branch P&L, sales, or cash flow using ONLY the following structured JSON data. Keep the answer extremely brief (1-2 sentences max) so it is pleasant to read out loud. Do not perform calculations yourself - use the exact numbers from the data. If the question cannot be answered from the data, say you do not have that information.\n\nJSON Data:\n" + biJson + "\n\nUser Question: " + transcribedText;

                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.2
                    }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, googleChatUrl);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, new { error = $"Gemma 2 generation failed: {errorMsg}" });
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                var answer = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "";

                return Ok(new { text = answer.Trim() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"LLM generation error: {ex.Message}" });
            }
        }
    }
}
