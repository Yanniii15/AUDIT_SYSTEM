using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;

namespace AuditCkDayo.Services
{
    public class TesseractOcrService : IOcrService
    {
        private static readonly Regex DateRegex = new Regex(@"\b(\d{4}[-/.]\d{1,2}[-/.]\d{1,2}|\d{1,2}[-/.]\d{1,2}[-/.]\d{2,4})\b");
        private static readonly Regex DecimalRegex = new Regex(@"-?\s*(?:₱|\b)?\s*\d{1,3}(?:,\d{3})*(?:\.\d+)?|\b-?\s*\d{1,6}\.\d+\b");
        private static readonly Regex ItemLineRegex = new Regex(@"^\s*(.*?)\s+(\d+)\s+(?:₱?\s*)?([\d,]+\.?\d*)\s+(?:₱?\s*)?([\d,]+\.?\d*)\s*$");

        public Task<OcrResult> ParseReceiptAsync(List<Stream> imageStreams)
        {
            var result = new OcrResult();
            if (imageStreams == null || imageStreams.Count == 0)
            {
                return Task.FromResult(result);
            }

            try
            {
                var tessdataPath = GetTessdataPath();

                using var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default);
                
                foreach (var stream in imageStreams)
                {
                    byte[] bytes;
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        bytes = ms.ToArray();
                    }

                    using var img = Pix.LoadFromMemory(bytes);
                    using var page = engine.Process(img);
                    var text = page.GetText();

                    if (string.IsNullOrWhiteSpace(text)) continue;

                    ApplyReceiptText(result, text);
                }

                // If items have totals but overall total is 0, sum the items
                if (result.TotalAmount == 0m && result.Items.Count > 0)
                {
                    result.TotalAmount = result.Items.Sum(i => i.Total);
                }

                if (result.TransactionDate == null)
                {
                    result.TransactionDate = DateTime.Today;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TESSERACT_OCR] Error: {ex.Message}");
            }

            return Task.FromResult(result);
        }


        private static void ApplyReceiptText(OcrResult result, string text)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (result.TransactionDate == null)
                {
                    var dateMatch = DateRegex.Match(trimmed);
                    if (dateMatch.Success && DateTime.TryParse(dateMatch.Value, out var parsedDate))
                    {
                        result.TransactionDate = parsedDate;
                    }
                }

                var itemMatch = ItemLineRegex.Match(trimmed);
                if (itemMatch.Success)
                {
                    var name = itemMatch.Groups[1].Value.Trim();
                    var qty = int.TryParse(itemMatch.Groups[2].Value, out var q) ? q : 1;
                    var price = ParseMoney(itemMatch.Groups[3].Value);
                    var total = ParseMoney(itemMatch.Groups[4].Value);

                    result.Items.Add(new OcrItemResult
                    {
                        Name = name,
                        Quantity = qty,
                        Price = price,
                        Total = total == 0m ? price * qty : total
                    });
                }
                else
                {
                    var words = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length >= 2)
                    {
                        var lastWord = words[^1];
                        if (DecimalRegex.IsMatch(lastWord))
                        {
                            var priceVal = ParseMoney(lastWord);
                            var name = string.Join(" ", words.Take(words.Length - 1));

                            if (priceVal > 0m && !name.ToLower().Contains("total") && !name.ToLower().Contains("date") && !name.ToLower().Contains("cash"))
                            {
                                result.Items.Add(new OcrItemResult
                                {
                                    Name = name,
                                    Quantity = 1,
                                    Price = priceVal,
                                    Total = priceVal
                                });
                            }
                        }
                    }
                }

                var totalLower = trimmed.ToLower();
                if (totalLower.Contains("total") || totalLower.Contains("amount") || totalLower.Contains("due") || totalLower.Contains("sum"))
                {
                    var matches = DecimalRegex.Matches(trimmed);
                    foreach (Match match in matches)
                    {
                        var total = ParseMoney(match.Value);
                        if (total > result.TotalAmount)
                        {
                            result.TotalAmount = total;
                        }
                    }
                }
            }
        }

        private static decimal ParseMoney(string value)
        {
            var cleaned = value.Replace("₱", "").Replace(" ", "").Trim();
            return decimal.TryParse(cleaned, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0m;
        }
        public async Task<SalesReportOcrResult> ParseSalesReportAsync(Stream imageStream)
        {
            var result = new SalesReportOcrResult();
            if (imageStream == null)
            {
                return result;
            }

            try
            {
                // Run generic receipt parsing to get dates/totals
                var receiptResult = await ParseReceiptAsync(new List<Stream> { imageStream });
                
                result.BusinessDate = receiptResult.TransactionDate;
                result.GrossSales = receiptResult.TotalAmount;
                result.ConfirmedCashToHandover = receiptResult.TotalAmount;

                // Reset stream to read again for specific cashier sheet fields
                if (imageStream.CanSeek)
                {
                    imageStream.Position = 0;
                }

                var tessdataPath = GetTessdataPath();
                using var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default);
                
                byte[] bytes;
                using (var ms = new MemoryStream())
                {
                    imageStream.CopyTo(ms);
                    bytes = ms.ToArray();
                }

                using var img = Pix.LoadFromMemory(bytes);
                using var page = engine.Process(img);
                var text = page.GetText();

                if (!string.IsNullOrEmpty(text))
                {
                    result.RawJson = System.Text.Json.JsonSerializer.Serialize(receiptResult);
                    ApplySalesReportText(result, text);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TESSERACT_OCR] SalesReport Error: {ex.Message}");
            }

            return result;
        }

        private static void ApplySalesReportText(SalesReportOcrResult result, string text)
        {
            var lines = text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
            string? activeGroup = null;

            foreach (var line in lines)
            {
                var normalized = line.Trim().TrimStart('•', '-', '*').Trim();
                var lower = normalized.ToLowerInvariant();

                if (result.BusinessDate == null)
                {
                    var dateMatch = DateRegex.Match(normalized);
                    if (dateMatch.Success && DateTime.TryParse(dateMatch.Value, out var parsedDate))
                    {
                        result.BusinessDate = parsedDate;
                    }
                    else if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                    {
                        result.BusinessDate = parsedDate;
                    }
                }

                if (lower.Contains("cashier"))
                {
                    var match = Regex.Match(normalized, @"cashier\s*name\s*[:=-]?\s*(.*)", RegexOptions.IgnoreCase);
                    if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                    {
                        result.CashierName = match.Groups[1].Value.Trim();
                    }
                }

                if (lower.Contains("daily gross sales"))
                {
                    result.GrossSales = FirstMoney(normalized);
                    activeGroup = null;
                    continue;
                }

                if (lower.Contains("cash sales") && !lower.Contains("g-cash") && !lower.Contains("gcash"))
                {
                    result.ConfirmedCashToHandover = FirstMoney(normalized);
                    activeGroup = null;
                    continue;
                }

                if (lower.Contains("pcf expenses") || lower.Contains("expenses from sales"))
                {
                    result.CashOut = FirstMoney(normalized);
                    activeGroup = "expenses";
                    continue;
                }

                if (lower.Contains("g-cash") || lower.Contains("gcash"))
                {
                    result.GCashAmount = FirstMoney(normalized);
                    activeGroup = "gcash";
                    continue;
                }

                if (lower.Contains("bank transfer") || lower.Contains("card") || lower.Contains("run-away"))
                {
                    result.OtherPaymentAmount += FirstMoney(normalized);
                    activeGroup = "other";
                    continue;
                }

                if (lower.Contains("credit"))
                {
                    result.CreditAmount = FirstMoney(normalized);
                    activeGroup = "credit";
                    continue;
                }

                if (IsSalesSectionHeader(lower))
                {
                    activeGroup = null;
                    continue;
                }

                if (activeGroup == "gcash")
                {
                    result.GCashAmount += FirstMoney(normalized);
                }
                else if (activeGroup == "other")
                {
                    result.OtherPaymentAmount += FirstMoney(normalized);
                }
                else if (activeGroup == "credit")
                {
                    result.CreditAmount += Math.Abs(FirstMoney(normalized));
                }

                var denomMatch = Regex.Match(normalized, @"\b(1000|500|200|100|50|20|10|5|1)\s*[xX*]\s*(\d+)\b");
                if (denomMatch.Success)
                {
                    result.Denominations.Add(new DenominationOcrResult
                    {
                        Denomination = decimal.Parse(denomMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                        Quantity = int.Parse(denomMatch.Groups[2].Value, CultureInfo.InvariantCulture)
                    });
                }
            }
        }

        private static bool IsSalesSectionHeader(string lower)
        {
            return lower.Contains("closing gross sales")
                || lower.Contains("food sales")
                || lower.Contains("beer sales")
                || lower.Contains("beverages sales")
                || lower.Contains("hard sales")
                || lower.Contains("other sales")
                || lower.Contains("senior")
                || lower.Contains("pwd")
                || lower.Contains("loyalty card")
                || lower.Contains("gift voucher")
                || lower.Contains("employee")
                || lower.Contains("eagles")
                || lower.Contains("sales shortage")
                || lower.Contains("sales overage")
                || lower.Contains("resto pcf")
                || lower.Contains("pcf from sales")
                || lower == "change:";
        }

        private static decimal FirstMoney(string value)
        {
            var match = DecimalRegex.Match(value);
            return match.Success ? Math.Abs(ParseMoney(match.Value)) : 0m;
        }

        private string GetTessdataPath()
        {
            var tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
            if (!Directory.Exists(tessdataPath))
            {
                var dir = AppContext.BaseDirectory;
                while (!string.IsNullOrEmpty(dir))
                {
                    var candidate = Path.Combine(dir, "tessdata");
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }
                    var projectDirCandidate = Path.Combine(dir, "AuditCkDayo", "tessdata");
                    if (Directory.Exists(projectDirCandidate))
                    {
                        return projectDirCandidate;
                    }
                    dir = Path.GetDirectoryName(dir);
                }
            }
            return tessdataPath;
        }
    }
}
