using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace AuditCkDayo.Services
{
    public class GoogleGeminiOcrService : IOcrService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public GoogleGeminiOcrService(IConfiguration configuration)
        {
            _apiKey = configuration["GoogleGemini:ApiKey"] ?? "";
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120)
            };
        }

        // Downscale + re-encode the receipt to a compact JPEG so Gemini receives a smaller,
        // faster-to-process payload. Falls back to the original bytes if the image can't be decoded.
        private static async Task<byte[]> CompressToJpegAsync(Stream source, int maxDimension = 1800, int quality = 82)
        {
            try
            {
                if (source.CanSeek) source.Position = 0;
                using (var image = await Image.LoadAsync(source))
                {
                    if (image.Width > maxDimension || image.Height > maxDimension)
                    {
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Mode = ResizeMode.Max,
                            Size = new SixLabors.ImageSharp.Size(maxDimension, maxDimension)
                        }));
                    }

                    using (var ms = new MemoryStream())
                    {
                        await image.SaveAsync(ms, new JpegEncoder { Quality = quality });
                        return ms.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GEMINI_OCR] Image compression failed, using original bytes. Error: {ex.Message}");
                if (source.CanSeek) source.Position = 0;
                using (var ms = new MemoryStream())
                {
                    await source.CopyToAsync(ms);
                    return ms.ToArray();
                }
            }
        }

        // Retries transient Gemini failures (503/429 and other 5xx, plus timeouts) with a short backoff.
        private async Task<string> SendWithRetryAsync(string requestUrl, string jsonPayload, int maxAttempts = 3)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                })
                {
                    try
                    {
                        var response = await _httpClient.SendAsync(request);

                        var retriable = response.StatusCode == HttpStatusCode.ServiceUnavailable
                            || response.StatusCode == HttpStatusCode.TooManyRequests
                            || (int)response.StatusCode >= 500;

                        if (retriable && attempt < maxAttempts)
                        {
                            Console.WriteLine($"[GEMINI_OCR] {response.StatusCode} on attempt {attempt}/{maxAttempts}, retrying...");
                            await Task.Delay(attempt * 500);
                            continue;
                        }

                        response.EnsureSuccessStatusCode();
                        return await response.Content.ReadAsStringAsync();
                    }
                    catch (HttpRequestException) when (attempt < maxAttempts)
                    {
                        Console.WriteLine($"[GEMINI_OCR] network error on attempt {attempt}/{maxAttempts}, retrying...");
                        await Task.Delay(attempt * 500);
                    }
                    catch (TaskCanceledException) when (attempt < maxAttempts)
                    {
                        Console.WriteLine($"[GEMINI_OCR] request timed out on attempt {attempt}/{maxAttempts}, retrying...");
                        await Task.Delay(attempt * 500);
                    }
                }
            }

            throw new HttpRequestException($"Gemini request failed after {maxAttempts} attempts.");
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
                             "3. The line items (each with name, quantity as decimal e.g. 1 or 1.5, unit price as decimal, and total price as decimal).\n" +
                             "Return ONLY a JSON object matching this schema:\n" +
                             "{ \"TotalAmount\": decimal, \"TransactionDate\": \"YYYY-MM-DD\", \"Items\": [ { \"Name\": string, \"Quantity\": decimal, \"Price\": decimal, \"Total\": decimal } ] }";

                parts.Add(new { text = prompt });

                foreach (var imageStream in imageStreams)
                {
                    // Downscale + re-encode to JPEG, then base64 so Gemini gets a smaller payload
                    var bytes = await CompressToJpegAsync(imageStream);
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
                var requestUrl = $"https://generativelanguage.googleapis.com/v1/models/gemini-3.6-flash:generateContent?key={_apiKey}";

                var responseContent = await SendWithRetryAsync(requestUrl, jsonPayload);
                
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

        public async Task<SalesReportOcrResult> ParseSalesReportAsync(Stream imageStream)
        {
            var result = new SalesReportOcrResult();

            if (imageStream == null)
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

                var prompt = "Analyze this daily sales report / cashier shift closing sheet. Extract the following details. Support both tabular reports and text-message formats like CLOSING / MAIN SALES.\n" +
                             "Mapping rules for text-message daily sales:\n" +
                             "- BusinessDate: parse the written date such as 'August 12, 2026' and return YYYY-MM-DD.\n" +
                             "- CashierName: value after 'Cashier Name'.\n" +
                             "- GrossSales: use 'Daily Gross Sales' when present, not Closing Gross Sales or category subtotals.\n" +
                             "- ConfirmedCashToHandover: use 'Cash Sales'.\n" +
                             "- GCashLines: return every individual amount listed under G-Cash sales until the next payment section; do not collapse rows.\n" +
                             "- BankTransferLines: return every individual bank transfer row with bank/name in Label when present.\n" +
                             "- CardLines: return every individual card payment row.\n" +
                             "- CreditLines: return every individual Credit row; keep names/reasons in Label and treat dash-prefixed amounts as positive credits.\n" +
                             "- RunawayCustomerLines: return every individual run-away customer row with names/reasons in Label when present.\n" +
                             "- ExpenseFromSalesLines: return every individual PCF Expenses / expenses from sales row with reason/name in Label when present.\n" +
                             "- GCashAmount, CreditAmount, OtherPaymentAmount, and CashOut must equal the sums of those returned line arrays when line detail is present.\n" +
                             "Fields to return:\n" +
                             "1. CashierName (string, name of cashier/person on shift)\n" +
                             "2. BusinessDate (string in YYYY-MM-DD format)\n" +
                             "3. GrossSales (decimal, total/gross sales amount)\n" +
                             "4. CashOut (decimal, total PCF expenses paid from starting PCF; this is not PCF change and must not reduce sales cash handover)\n" +
                             "5. ConfirmedCashToHandover (decimal, cash sales / cash to be turned over)\n" +
                             "6. GCashAmount (decimal, total GCash sales or remittance)\n" +
                             "7. CreditAmount (decimal, total credit/complimentary sales)\n" +
                             "8. OtherPaymentAmount (decimal, total bank transfer/card/run-away/other non-cash, non-GCash amounts)\n" +
                             "9. GCashLines (array of objects with optional Label and Amount)\n" +
                             "10. BankTransferLines (array of objects with optional Label and Amount)\n" +
                             "11. CardLines (array of objects with optional Label and Amount)\n" +
                             "12. CreditLines (array of objects with optional Label and Amount)\n" +
                             "13. RunawayCustomerLines (array of objects with optional Label and Amount)\n" +
                             "14. ExpenseFromSalesLines (array of objects with optional Label and Amount)\n" +
                             "15. ReceiptNumberStart (string, starting receipt number in sequence)\n" +
                             "16. ReceiptNumberEnd (string, ending receipt number in sequence)\n" +
                             "17. WitnessName (string, witness to handover)\n" +
                             "18. Denominations (array of objects with 'Denomination' as decimal and 'Quantity' as integer for any counted bills/coins like 1000, 500, 200, 100, 50, 20, 10, 5, 1)\n" +
                             "Return ONLY a JSON object matching this schema:\n" +
                             "{ \"CashierName\": string, \"BusinessDate\": \"YYYY-MM-DD\", \"GrossSales\": decimal, \"CashOut\": decimal, \"ConfirmedCashToHandover\": decimal, \"GCashAmount\": decimal, \"CreditAmount\": decimal, \"OtherPaymentAmount\": decimal, \"GCashLines\": [ { \"Label\": string, \"Amount\": decimal } ], \"BankTransferLines\": [ { \"Label\": string, \"Amount\": decimal } ], \"CardLines\": [ { \"Label\": string, \"Amount\": decimal } ], \"CreditLines\": [ { \"Label\": string, \"Amount\": decimal } ], \"RunawayCustomerLines\": [ { \"Label\": string, \"Amount\": decimal } ], \"ExpenseFromSalesLines\": [ { \"Label\": string, \"Amount\": decimal } ], \"ReceiptNumberStart\": string, \"ReceiptNumberEnd\": string, \"WitnessName\": string, \"Denominations\": [ { \"Denomination\": decimal, \"Quantity\": int } ] }";

                parts.Add(new { text = prompt });

                var bytes = await CompressToJpegAsync(imageStream);
                var base64Image = Convert.ToBase64String(bytes);
                parts.Add(new
                {
                    inlineData = new
                    {
                        mimeType = "image/jpeg",
                        data = base64Image
                    }
                });

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
                var requestUrl = $"https://generativelanguage.googleapis.com/v1/models/gemini-3.6-flash:generateContent?key={_apiKey}";

                var responseContent = await SendWithRetryAsync(requestUrl, jsonPayload);
                
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
                        var parsedResult = JsonSerializer.Deserialize<GeminiSalesReportOcrResult>(textResponse, options);

                        if (parsedResult != null)
                        {
                            result.CashierName = parsedResult.CashierName;
                            if (DateTime.TryParseExact(parsedResult.BusinessDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
                            {
                                result.BusinessDate = date;
                            }
                            result.GrossSales = parsedResult.GrossSales ?? 0.00m;
                            result.CashOut = parsedResult.CashOut ?? 0.00m;
                            result.ConfirmedCashToHandover = parsedResult.ConfirmedCashToHandover ?? 0.00m;
                            result.GCashAmount = parsedResult.GCashAmount ?? 0.00m;
                            result.CreditAmount = parsedResult.CreditAmount ?? 0.00m;
                            result.OtherPaymentAmount = parsedResult.OtherPaymentAmount ?? 0.00m;
                            CopyPaymentLines(parsedResult.GCashLines, result.GCashLines);
                            CopyPaymentLines(parsedResult.BankTransferLines, result.BankTransferLines);
                            CopyPaymentLines(parsedResult.CardLines, result.CardLines);
                            CopyPaymentLines(parsedResult.CreditLines, result.CreditLines);
                            CopyPaymentLines(parsedResult.RunawayCustomerLines, result.RunawayCustomerLines);
                            CopyPaymentLines(parsedResult.ExpenseFromSalesLines, result.ExpenseFromSalesLines);
                            result.ReceiptNumberStart = parsedResult.ReceiptNumberStart;
                            result.ReceiptNumberEnd = parsedResult.ReceiptNumberEnd;
                            result.WitnessName = parsedResult.WitnessName;
                            result.RawJson = textResponse;

                            if (parsedResult.Denominations != null)
                            {
                                foreach (var denom in parsedResult.Denominations)
                                {
                                    result.Denominations.Add(new DenominationOcrResult
                                    {
                                        Denomination = denom.Denomination ?? 0m,
                                        Quantity = denom.Quantity ?? 0
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GEMINI_OCR] SalesReport request failed: {ex.Message}");
                throw;
            }

            return result;
        }

        private class GeminiSalesReportOcrResult
        {
            public string? CashierName { get; set; }
            public string? BusinessDate { get; set; }
            public decimal? GrossSales { get; set; }
            public decimal? CashOut { get; set; }
            public decimal? ConfirmedCashToHandover { get; set; }
            public decimal? GCashAmount { get; set; }
            public decimal? CreditAmount { get; set; }
            public decimal? OtherPaymentAmount { get; set; }
            public List<GeminiSalesReportPaymentLine>? GCashLines { get; set; }
            public List<GeminiSalesReportPaymentLine>? BankTransferLines { get; set; }
            public List<GeminiSalesReportPaymentLine>? CardLines { get; set; }
            public List<GeminiSalesReportPaymentLine>? CreditLines { get; set; }
            public List<GeminiSalesReportPaymentLine>? RunawayCustomerLines { get; set; }
            public List<GeminiSalesReportPaymentLine>? ExpenseFromSalesLines { get; set; }
            public string? ReceiptNumberStart { get; set; }
            public string? ReceiptNumberEnd { get; set; }
            public string? WitnessName { get; set; }
            public List<GeminiDenominationOcrResult>? Denominations { get; set; }
        }

        private static void CopyPaymentLines(List<GeminiSalesReportPaymentLine>? source, List<SalesReportOcrPaymentLine> destination)
        {
            if (source == null)
            {
                return;
            }

            foreach (var line in source)
            {
                var amount = Math.Abs(line.Amount ?? 0m);
                if (amount > 0m || !string.IsNullOrWhiteSpace(line.Label))
                {
                    destination.Add(new SalesReportOcrPaymentLine
                    {
                        Label = line.Label,
                        Amount = amount
                    });
                }
            }
        }

        private class GeminiSalesReportPaymentLine
        {
            public string? Label { get; set; }
            public decimal? Amount { get; set; }
        }

        private class GeminiDenominationOcrResult
        {
            public decimal? Denomination { get; set; }
            public int? Quantity { get; set; }
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
            public decimal Quantity { get; set; } = 1m;
            public decimal Price { get; set; }
            public decimal Total { get; set; }
        }
    }
}
