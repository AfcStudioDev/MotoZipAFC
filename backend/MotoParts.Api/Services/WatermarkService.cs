using SixLabors.ImageSharp.Formats;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MotoParts.Api.Services
{

    public class WatermarkResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string OutputPath { get; set; }
        public Exception Exception { get; set; }
    }

    public enum WatermarkPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center
    }

    public class WatermarkService
    {
        public static WatermarkResult ApplyImageWatermark(
            string baseImagePath,
            string watermarkImagePath,
            string outputPath,
            WatermarkPosition position = WatermarkPosition.BottomRight,
            float opacity = 0.3f,
            float scale = 0.2f,
            float padding = 0.02f)
        {
            try
            {
                using var baseImage = Image.FromFile(baseImagePath);
                using var watermark = Image.FromFile(watermarkImagePath);
                using var result = new Bitmap(baseImage.Width, baseImage.Height, PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(result);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                g.DrawImage(baseImage, 0, 0, baseImage.Width, baseImage.Height);

                int wmWidth = (int)(baseImage.Width * scale);
                int wmHeight = (int)(watermark.Height * (wmWidth / (float)watermark.Width));

                int paddingX = (int)(baseImage.Width * padding);
                int paddingY = (int)(baseImage.Height * padding);

                int x, y;
                switch (position)
                {
                    case WatermarkPosition.TopLeft:
                        x = paddingX;
                        y = paddingY;
                        break;
                    case WatermarkPosition.TopRight:
                        x = baseImage.Width - wmWidth - paddingX;
                        y = paddingY;
                        break;
                    case WatermarkPosition.BottomLeft:
                        x = paddingX;
                        y = baseImage.Height - wmHeight - paddingY;
                        break;
                    case WatermarkPosition.Center:
                        x = (baseImage.Width - wmWidth) / 2;
                        y = (baseImage.Height - wmHeight) / 2;
                        break;
                    case WatermarkPosition.BottomRight:
                    default:
                        x = baseImage.Width - wmWidth - paddingX;
                        y = baseImage.Height - wmHeight - paddingY;
                        break;
                }

                using var attributes = new ImageAttributes();
                var colorMatrix = new ColorMatrix
                {
                    Matrix00 = 1f,
                    Matrix11 = 1f,
                    Matrix22 = 1f,
                    Matrix33 = opacity,
                    Matrix44 = 1f
                };
                attributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                var destRect = new Rectangle(x, y, wmWidth, wmHeight);
                g.DrawImage(watermark, destRect, 0, 0, watermark.Width, watermark.Height, GraphicsUnit.Pixel, attributes);

                result.Save(outputPath, GetImageFormat(outputPath));

                return new WatermarkResult
                {
                    Success = true,
                    Message = "Image watermark applied successfully",
                    OutputPath = outputPath
                };
            }
            catch (Exception ex)
            {
                return new WatermarkResult
                {
                    Success = false,
                    Message = ex.Message,
                    OutputPath = null,
                    Exception = ex
                };
            }
        }

        public static async Task<WatermarkResult> ApplyImageWatermarkAsync(
            string baseImagePath,
            string watermarkImagePath,
            string outputPath,
            WatermarkPosition position = WatermarkPosition.BottomRight,
            float opacity = 0.3f,
            float scale = 0.2f,
            float padding = 0.02f)
        {
            return await Task.Run(() =>
                ApplyImageWatermark(baseImagePath, watermarkImagePath, outputPath, position, opacity, scale, padding));
        }

        public static WatermarkResult ApplyTextWatermark(
            string baseImagePath,
            string outputPath,
            string text,
            string fontFamily = "Arial",
            float fontSize = 0.05f,
            WatermarkPosition position = WatermarkPosition.BottomRight,
            float opacity = 0.5f,
            Color? color = null,
            float padding = 0.02f,
            float rotation = 0f)
        {
            try
            {
                using var baseImage = Image.FromFile(baseImagePath);
                using var result = new Bitmap(baseImage.Width, baseImage.Height, PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(result);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                g.DrawImage(baseImage, 0, 0, baseImage.Width, baseImage.Height);

                float actualFontSize = fontSize < 1f ? baseImage.Height * fontSize : fontSize;
                using var font = new Font(fontFamily, actualFontSize, FontStyle.Bold);

                var textSize = g.MeasureString(text, font);
                int textWidth = (int)textSize.Width;
                int textHeight = (int)textSize.Height;

                int paddingX = (int)(baseImage.Width * padding);
                int paddingY = (int)(baseImage.Height * padding);

                float x, y;
                switch (position)
                {
                    case WatermarkPosition.TopLeft:
                        x = paddingX;
                        y = paddingY;
                        break;
                    case WatermarkPosition.TopRight:
                        x = baseImage.Width - textWidth - paddingX;
                        y = paddingY;
                        break;
                    case WatermarkPosition.BottomLeft:
                        x = paddingX;
                        y = baseImage.Height - textHeight - paddingY;
                        break;
                    case WatermarkPosition.Center:
                        x = (baseImage.Width - textWidth) / 2f;
                        y = (baseImage.Height - textHeight) / 2f;
                        break;
                    case WatermarkPosition.BottomRight:
                    default:
                        x = baseImage.Width - textWidth - paddingX;
                        y = baseImage.Height - textHeight - paddingY;
                        break;
                }

                var textColor = color ?? Color.White;
                var brush = new SolidBrush(Color.FromArgb(
                    (int)(opacity * 255),
                    textColor.R,
                    textColor.G,
                    textColor.B
                ));

                if (rotation != 0f)
                {
                    g.TranslateTransform(x + textWidth / 2f, y + textHeight / 2f);
                    g.RotateTransform(rotation);
                    g.DrawString(text, font, brush, -textWidth / 2f, -textHeight / 2f);
                    g.ResetTransform();
                }
                else
                {
                    g.DrawString(text, font, brush, x, y);
                }

                result.Save(outputPath, GetImageFormat(outputPath));

                return new WatermarkResult
                {
                    Success = true,
                    Message = "Text watermark applied successfully",
                    OutputPath = outputPath
                };
            }
            catch (Exception ex)
            {
                return new WatermarkResult
                {
                    Success = false,
                    Message = ex.Message,
                    OutputPath = null,
                    Exception = ex
                };
            }
        }

        public static async Task<WatermarkResult> ApplyTextWatermarkAsync(
            string baseImagePath,
            string outputPath,
            string text,
            string fontFamily = "Arial",
            float fontSize = 0.05f,
            WatermarkPosition position = WatermarkPosition.BottomRight,
            float opacity = 0.5f,
            Color? color = null,
            float padding = 0.02f,
            float rotation = 0f)
        {
            return await Task.Run(() =>
                ApplyTextWatermark(baseImagePath, outputPath, text, fontFamily, fontSize, position, opacity, color, padding, rotation));
        }

        private static ImageFormat GetImageFormat(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                ".png" => ImageFormat.Png,
                ".gif" => ImageFormat.Gif,
                ".bmp" => ImageFormat.Bmp,
                ".tiff" or ".tif" => ImageFormat.Tiff,
                ".webp" => ImageFormat.Webp,
                _ => ImageFormat.Png
            };
        }

        private static EncoderParameters GetJpegEncoderParameters(int quality = 90)
        {
            var jpegEncoder = GetEncoder(ImageFormat.Jpeg);
            var qualityEncoder = System.Drawing.Imaging.Encoder.Quality;
            var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(qualityEncoder, quality);
            return encoderParameters;
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            foreach (var codec in ImageCodecInfo.GetImageEncoders())
            {
                if (codec.FormatID == format.Guid)
                    return codec;
            }
            return null;
        }
    }
}
