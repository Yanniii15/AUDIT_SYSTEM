using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AuditCkDayo.Services
{
    public class FallbackOcrService : IOcrService
    {
        private readonly GoogleGeminiOcrService _geminiService;

        public FallbackOcrService(GoogleGeminiOcrService geminiService)
        {
            _geminiService = geminiService;
        }

        public async Task<OcrResult> ParseReceiptAsync(List<Stream> imageStreams)
        {
            var copiedForGemini = new List<Stream>();

            foreach (var stream in imageStreams)
            {
                var geminiMs = new MemoryStream();
                await stream.CopyToAsync(geminiMs);
                geminiMs.Position = 0;
                copiedForGemini.Add(geminiMs);
            }

            try
            {
                Console.WriteLine("[FALLBACK_OCR] Attempting primary OCR with Google Gemini...");
                return await _geminiService.ParseReceiptAsync(copiedForGemini);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FALLBACK_OCR] Gemini failed, returning empty result. Error: {ex.Message}");
                return new OcrResult();
            }
            finally
            {
                foreach (var s in copiedForGemini) s.Dispose();
            }
        }

        public async Task<SalesReportOcrResult> ParseSalesReportAsync(Stream imageStream)
        {
            var geminiMs = new MemoryStream();
            await imageStream.CopyToAsync(geminiMs);
            geminiMs.Position = 0;

            try
            {
                Console.WriteLine("[FALLBACK_OCR] Attempting primary SalesReport OCR with Google Gemini...");
                return await _geminiService.ParseSalesReportAsync(geminiMs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FALLBACK_OCR] Gemini SalesReport failed, returning empty result. Error: {ex.Message}");
                return new SalesReportOcrResult();
            }
            finally
            {
                geminiMs.Dispose();
            }
        }
    }
}
