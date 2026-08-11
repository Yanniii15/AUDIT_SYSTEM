using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AuditCkDayo.Services
{
    public class FallbackOcrService : IOcrService
    {
        private readonly GoogleGeminiOcrService _geminiService;
        private readonly TesseractOcrService _tesseractService;

        public FallbackOcrService(GoogleGeminiOcrService geminiService, TesseractOcrService tesseractService)
        {
            _geminiService = geminiService;
            _tesseractService = tesseractService;
        }

        public async Task<OcrResult> ParseReceiptAsync(List<Stream> imageStreams)
        {
            // We must copy the streams so that if the first attempt (Gemini) fails, 
            // the streams are still readable from the beginning for the fallback (Tesseract).
            var copiedForGemini = new List<Stream>();
            var copiedForTesseract = new List<Stream>();

            foreach (var stream in imageStreams)
            {
                var geminiMs = new MemoryStream();
                var tesseractMs = new MemoryStream();

                await stream.CopyToAsync(geminiMs);
                
                geminiMs.Position = 0;
                geminiMs.CopyTo(tesseractMs);

                geminiMs.Position = 0;
                tesseractMs.Position = 0;

                copiedForGemini.Add(geminiMs);
                copiedForTesseract.Add(tesseractMs);
            }

            try
            {
                Console.WriteLine("[FALLBACK_OCR] Attempting primary OCR with Google Gemini...");
                return await _geminiService.ParseReceiptAsync(copiedForGemini);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FALLBACK_OCR] Gemini failed, falling back to local Tesseract OCR. Error: {ex.Message}");
                return await _tesseractService.ParseReceiptAsync(copiedForTesseract);
            }
            finally
            {
                foreach (var s in copiedForGemini) s.Dispose();
                foreach (var s in copiedForTesseract) s.Dispose();
            }
        }

        public async Task<SalesReportOcrResult> ParseSalesReportAsync(Stream imageStream)
        {
            var geminiMs = new MemoryStream();
            var tesseractMs = new MemoryStream();

            await imageStream.CopyToAsync(geminiMs);
            
            geminiMs.Position = 0;
            geminiMs.CopyTo(tesseractMs);

            geminiMs.Position = 0;
            tesseractMs.Position = 0;

            try
            {
                Console.WriteLine("[FALLBACK_OCR] Attempting primary SalesReport OCR with Google Gemini...");
                return await _geminiService.ParseSalesReportAsync(geminiMs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FALLBACK_OCR] Gemini SalesReport failed, falling back to local Tesseract OCR. Error: {ex.Message}");
                return await _tesseractService.ParseSalesReportAsync(tesseractMs);
            }
            finally
            {
                geminiMs.Dispose();
                tesseractMs.Dispose();
            }
        }
    }
}
