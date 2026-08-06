using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AuditCkDayo.Services
{
    public class AzureOcrService : IOcrService
    {
        private readonly string _apiKey;
        private readonly string _endpoint;

        public AzureOcrService(IConfiguration configuration)
        {
            _apiKey = configuration["AzureOcr:ApiKey"] ?? "";
            _endpoint = configuration["AzureOcr:Endpoint"] ?? "";
        }

        public async Task<OcrResult> ParseReceiptAsync(List<Stream> imageStreams)
        {
            var combinedResult = new OcrResult();
            if (imageStreams == null || imageStreams.Count == 0)
            {
                return combinedResult;
            }

            foreach (var stream in imageStreams)
            {
                var result = await ParseSingleReceiptAsync(stream);
                combinedResult.TotalAmount += result.TotalAmount;
                if (result.TransactionDate.HasValue)
                {
                    combinedResult.TransactionDate = result.TransactionDate;
                }
                if (result.Items != null)
                {
                    combinedResult.Items.AddRange(result.Items);
                }
            }

            return combinedResult;
        }

        private async Task<OcrResult> ParseSingleReceiptAsync(Stream imageStream)
        {
            var result = new OcrResult();

            // FALLBACK / MOCK: if credentials are not configured, simulate OCR response
            if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_endpoint) || _apiKey == "YOUR_API_KEY" || _endpoint == "YOUR_ENDPOINT")
            {
                result.TotalAmount = 250.50m;
                result.TransactionDate = DateTime.Today;
                result.Items.Add(new OcrItemResult { Name = "Sample Item A", Quantity = 2, Price = 100.00m, Total = 200.00m });
                result.Items.Add(new OcrItemResult { Name = "Sample Item B", Quantity = 1, Price = 50.50m, Total = 50.50m });
                return result;
            }

            var credential = new AzureKeyCredential(_apiKey);
            var client = new DocumentAnalysisClient(new Uri(_endpoint), credential);

            var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-receipt", imageStream);
            var analyzeResult = operation.Value;

            foreach (var document in analyzeResult.Documents)
            {
                if (document.Fields.TryGetValue("Total", out var totalField) && totalField.FieldType == DocumentFieldType.Double)
                {
                    result.TotalAmount = (decimal)totalField.Value.AsDouble();
                }

                if (document.Fields.TryGetValue("TransactionDate", out var dateField) && dateField.FieldType == DocumentFieldType.Date)
                {
                    result.TransactionDate = dateField.Value.AsDate().DateTime;
                }

                if (document.Fields.TryGetValue("Items", out var itemsField) && itemsField.FieldType == DocumentFieldType.List)
                {
                    foreach (var itemField in itemsField.Value.AsList())
                    {
                        var ocrItem = new OcrItemResult();
                        if (itemField.FieldType == DocumentFieldType.Dictionary)
                        {
                            var itemDict = itemField.Value.AsDictionary();
                            if (itemDict.TryGetValue("Description", out var descField) && descField.FieldType == DocumentFieldType.String)
                            {
                                ocrItem.Name = descField.Value.AsString();
                            }
                            if (itemDict.TryGetValue("Quantity", out var qtyField) && qtyField.FieldType == DocumentFieldType.Double)
                            {
                                ocrItem.Quantity = (int)qtyField.Value.AsDouble();
                            }
                            if (itemDict.TryGetValue("Price", out var priceField) && priceField.FieldType == DocumentFieldType.Double)
                            {
                                ocrItem.Price = (decimal)priceField.Value.AsDouble();
                            }
                            if (itemDict.TryGetValue("TotalPrice", out var totalPriceField) && totalPriceField.FieldType == DocumentFieldType.Double)
                            {
                                ocrItem.Total = (decimal)totalPriceField.Value.AsDouble();
                            }
                            else
                            {
                                ocrItem.Total = ocrItem.Price * ocrItem.Quantity;
                            }
                        }
                        result.Items.Add(ocrItem);
                    }
                }
            }

            return result;
        }
    }
}
