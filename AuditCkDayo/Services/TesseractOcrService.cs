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
        private static readonly Regex DecimalRegex = new Regex(@"(?:₱|\b)?\s*\d{1,3}(?:,\d{3})*(?:\.\d{2})?|\b\d{1,6}\.\d{2}\b");
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
            var cleaned = value.Replace("₱", "").Trim();
            return decimal.TryParse(cleaned, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
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
                    
                    var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        var lower = trimmed.ToLower();

                        // Match cashier name pattern
                        if (lower.Contains("cashier") || lower.Contains("shift") || lower.Contains("name"))
                        {
                            var match = Regex.Match(trimmed, @"(?:cashier|name|shift)\s*[:=-]?\s*(.*)", RegexOptions.IgnoreCase);
                            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                            {
                                result.CashierName = match.Groups[1].Value.Trim();
                            }
                        }

                        // Match cash out pattern
                        if (lower.Contains("cash out") || lower.Contains("cashout") || lower.Contains("paid out") || lower.Contains("expenses"))
                        {
                            var match = DecimalRegex.Match(trimmed);
                            if (match.Success && decimal.TryParse(match.Value, out var val))
                            {
                                result.CashOut = val;
                            }
                        }

                        // Match GCash pattern
                        if (lower.Contains("gcash") || lower.Contains("g-cash"))
                        {
                            var match = DecimalRegex.Match(trimmed);
                            if (match.Success && decimal.TryParse(match.Value, out var val))
                            {
                                result.GCashAmount = val;
                            }
                        }

                        // Match credit pattern
                        if (lower.Contains("credit") || lower.Contains("receivable") || lower.Contains("charge"))
                        {
                            var match = DecimalRegex.Match(trimmed);
                            if (match.Success && decimal.TryParse(match.Value, out var val))
                            {
                                result.CreditAmount = val;
                            }
                        }

                        // Match BDO or card or other payments
                        if (lower.Contains("bdo") || lower.Contains("bank") || lower.Contains("card") || lower.Contains("other"))
                        {
                            var match = DecimalRegex.Match(trimmed);
                            if (match.Success && decimal.TryParse(match.Value, out var val))
                            {
                                result.OtherPaymentAmount = val;
                            }
                        }

                        // Match receipt sequence numbers
                        if (lower.Contains("receipt") || lower.Contains("inv") || lower.Contains("seq"))
                        {
                            var matches = Regex.Matches(trimmed, @"\b\d{3,10}\b");
                            if (matches.Count >= 2)
                            {
                                result.ReceiptNumberStart = matches[0].Value;
                                result.ReceiptNumberEnd = matches[^1].Value;
                            }
                        }

                        // Match witness name
                        if (lower.Contains("witness") || lower.Contains("verified by") || lower.Contains("checked by"))
                        {
                            var match = Regex.Match(trimmed, @"(?:witness|verified|checked)\s*[:=-]?\s*(.*)", RegexOptions.IgnoreCase);
                            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                            {
                                result.WitnessName = match.Groups[1].Value.Trim();
                            }
                        }

                        // Match cash breakdown lines (e.g. 1000x5 or 1000 * 5)
                        var denomMatch = Regex.Match(trimmed, @"\b(1000|500|200|100|50|20|10|5|1)\s*[xX*]\s*(\d+)\b");
                        if (denomMatch.Success)
                        {
                            var denom = decimal.Parse(denomMatch.Groups[1].Value);
                            var qty = int.Parse(denomMatch.Groups[2].Value);
                            result.Denominations.Add(new DenominationOcrResult
                            {
                                Denomination = denom,
                                Quantity = qty
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TESSERACT_OCR] SalesReport Error: {ex.Message}");
            }

            return result;
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
