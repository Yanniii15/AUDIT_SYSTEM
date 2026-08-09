using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace AuditCkDayo.Services
{
    public class GoogleGeminiOcrService : IOcrService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public GoogleGeminiOcrService(IConfiguration configuration)
        {
            _apiKey = configuration["GoogleGemini:ApiKey"] ?? "";
            _httpClient = new HttpClient();
        }

        public async Task<OcrResult> ParseReceiptAsync(List<Stream> imageStreams)
        {
            var result = new OcrResult();

            if (imageStreams == null || imageStreams.Count == 0)
            {
                return result;
            }

            if (string.IsNullOrEmpty(_apiKey) || _apiKey == "YOUR_GEMINI_API_KEY")
            {
                throw new InvalidOperationException("Gemini API key is not configured.");
            }

            try
            {
                var parts = new List<object>();

                var prompt = "Analyze these receipt images. Combine the items and amounts if there are multiple pages/images. Extract the following details:\n" +
                             "1. The transaction date (in YYYY-MM-DD format, or the latest if multiple differ).\n" +
                             "2. The total amount as a decimal.\n" +
                             "3. The line items (each with name, quantity as integer, unit price as decimal, and total price as decimal).\n" +
                             "Return ONLY a JSON object matching this schema:\n" +
                             "{ \"TotalAmount\": decimal, \"TransactionDate\": \"YYYY-MM-DD\", \"Items\": [ { \"Name\": string, \"Quantity\": int, \"Price\": decimal, \"Total\": decimal } ] }";

                parts.Add(new { text = prompt });

                foreach (var imageStream in imageStreams)
                {
                    // Convert stream to base64
                    byte[] bytes;
                    using (var ms = new MemoryStream())
                    {
                        await imageStream.CopyToAsync(ms);
                        bytes = ms.ToArray();
                    }
                    var base64Image = Convert.ToBase64String(bytes);
                    parts.Add(new
                    {
                        inlineData = new
                        {
                            mimeType = "image/jpeg",
                            data = base64Image
                        }
                    });
                }

                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = parts.ToArray()
                        }
                    },
                    generationConfig = new
                    {
                        responseMimeType = "application/json"
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent?key={_apiKey}";
                
                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Parse Gemini Response
                using (var doc = JsonDocument.Parse(responseContent))
                {
                    var textResponse = doc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    if (!string.IsNullOrEmpty(textResponse))
                    {
                        textResponse = textResponse.Trim();
                        int openBraces = 0;
                        int firstOpenIndex = textResponse.IndexOf('{');
                        int matchingCloseIndex = -1;
                        if (firstOpenIndex >= 0)
                        {
                            for (int i = firstOpenIndex; i < textResponse.Length; i++)
                            {
                                if (textResponse[i] == '{') openBraces++;
                                else if (textResponse[i] == '}')
                                {
                                    openBraces--;
                                    if (openBraces == 0)
                                    {
                                        matchingCloseIndex = i;
                                        break;
                                    }
                                }
                            }
                        }
                        if (matchingCloseIndex >= 0)
                        {
                            textResponse = textResponse.Substring(firstOpenIndex, matchingCloseIndex - firstOpenIndex + 1);
                        }
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        var parsedResult = JsonSerializer.Deserialize<GeminiOcrResult>(textResponse, options);

                        if (parsedResult != null)
                        {
                            result.TotalAmount = parsedResult.TotalAmount ?? 0.00m;
                            
                            if (DateTime.TryParseExact(parsedResult.TransactionDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
                            {
                                result.TransactionDate = date;
                            }
                            else
                            {
                                result.TransactionDate = DateTime.Today;
                            }

                            if (parsedResult.Items != null)
                            {
                                foreach (var item in parsedResult.Items)
                                {
                                    result.Items.Add(new OcrItemResult
                                    {
                                        Name = item.Name,
                                        Quantity = item.Quantity,
                                        Price = item.Price,
                                        Total = item.Total
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GEMINI_OCR] Request failed: {ex.Message}");
                throw;
            }

            return result;
        }

        private class GeminiOcrResult
        {
            public decimal? TotalAmount { get; set; }
            public string? TransactionDate { get; set; }
            public List<GeminiOcrItem>? Items { get; set; }
        }

        private class GeminiOcrItem
        {
            public string Name { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal Price { get; set; }
            public decimal Total { get; set; }
        }
    }
}
