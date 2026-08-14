using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;

namespace PdfGeneration.Services 
{
    public class QrCodePdfService : IDisposable
    {
        private readonly QRCodeGenerator _qrGenerator;
        private bool _disposed;

        public QrCodePdfService()
        {
            _qrGenerator = new QRCodeGenerator();
            ConfigureLicense();
        }

        private static void ConfigureLicense()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>Возвращает путь к сгенерированному файлу — вызывающему коду он нужен, чтобы отдать PDF клиенту.</summary>
        public string GenerateQrCodePdf(
            string partNumber = "000-00000-00",
            string partName = "",
            string donorInfo = "",
            int qrCodeSize = 20,
            string outputFolder = null,
            string fileName = null)
        {
            ValidateInput(partNumber);

            var qrCodeImage = GenerateQrCode(partNumber, qrCodeSize);
            var document = BuildDocument(qrCodeImage, partName, donorInfo);

            fileName ??= partNumber + ".pdf";
            string outputPath = string.IsNullOrEmpty(outputFolder) ? fileName : Path.Combine(outputFolder, fileName);
            document.GeneratePdf(outputPath);

            return outputPath;
        }

        private byte[] GenerateQrCode(string partNumber, int size)
        {
            using (var qrData = _qrGenerator.CreateQrCode(partNumber, global::QRCoder.QRCodeGenerator.ECCLevel.Q))
            using (var qrCode = new PngByteQRCode(qrData))
            {
                return qrCode.GetGraphic(size);
            }
        }

        private static IDocument BuildDocument(byte[] qrCodeImage, string partName, string donorInfo)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(20);

                    page.Content().Column(column =>
                    {
                        column.Item().Image(qrCodeImage);

                        if (!string.IsNullOrWhiteSpace(partName))
                        {
                            column.Item()
                                  .PaddingTop(-35)
                                  .Text(partName)
                                  .FontSize(40)
                                  .AlignCenter();
                        }

                        if (!string.IsNullOrWhiteSpace(donorInfo))
                        {
                            column.Item()
                                  .PaddingTop(0)
                                  .Text(donorInfo)
                                  .FontSize(40)
                                  .AlignCenter();
                        }
                    });
                });
            });
        }

        private static void ValidateInput(string partNumber)
        {
            if (string.IsNullOrWhiteSpace(partNumber))
            {
                throw new ArgumentException("Номер детали не может быть пустым", nameof(partNumber));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _qrGenerator?.Dispose();
                _disposed = true;
            }
        }
    }
}