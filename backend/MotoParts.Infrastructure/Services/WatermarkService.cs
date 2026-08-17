using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace MotoParts.Infrastructure.Services
{

    public class WatermarkResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string? OutputPath { get; set; }
        public Exception? Exception { get; set; }
    }

    public enum WatermarkPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center
    }

    /// <summary>
    /// Наложение водяных знаков на ImageSharp — кроссплатформенно, в т.ч. в Linux-контейнере
    /// (в отличие от System.Drawing/GDI+, который на Linux без libgdiplus не работает вообще).
    /// </summary>
    public static class WatermarkService
    {
        /// <summary>
        /// Шрифт для текстового водяного знака берём в первую очередь из системных (на случай,
        /// если запрошенное имя реально установлено), а если его нет — из LatoFont, который
        /// пакет QuestPDF копирует в выходную папку при каждой сборке (см. QuestPDF.targets),
        /// поэтому он гарантированно есть рядом с exe и в Docker-образе на Linux, где системных
        /// шрифтов (включая Arial) обычно нет вовсе.
        /// </summary>
        private static readonly Lazy<FontFamily?> FallbackFontFamily = new(() =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "LatoFont", "Lato-Bold.ttf");
            if (!File.Exists(path)) return null;

            try
            {
                var collection = new FontCollection();
                return collection.Add(path);
            }
            catch
            {
                return null;
            }
        });

        private static Font ResolveFont(string fontFamily, float size, FontStyle style)
        {
            // В "голом" Linux-контейнере системных шрифтов/каталогов fontconfig обычно нет вовсе —
            // на некоторых платформах TryGet в такой ситуации не просто возвращает false, а бросает
            // исключение при попытке перечислить несуществующие каталоги. Не даём этому обрушить
            // весь fallback на LatoFont.
            try
            {
                if (SystemFonts.TryGet(fontFamily, out var family))
                    return family.CreateFont(size, style);
            }
            catch
            {
                // ignore — падаем на гарантированный LatoFont ниже
            }

            var fallback = FallbackFontFamily.Value;
            if (fallback is not null)
                return fallback.Value.CreateFont(size, FontStyle.Regular); // файл уже содержит начертание Bold

            throw new InvalidOperationException(
                $"Шрифт '{fontFamily}' не найден, а резервный LatoFont отсутствует по пути '{Path.Combine(AppContext.BaseDirectory, "LatoFont")}'.");
        }

        private static Point ResolveLocation(
            WatermarkPosition position, int canvasWidth, int canvasHeight,
            int contentWidth, int contentHeight, int paddingX, int paddingY)
        {
            return position switch
            {
                WatermarkPosition.TopLeft => new Point(paddingX, paddingY),
                WatermarkPosition.TopRight => new Point(canvasWidth - contentWidth - paddingX, paddingY),
                WatermarkPosition.BottomLeft => new Point(paddingX, canvasHeight - contentHeight - paddingY),
                WatermarkPosition.Center => new Point((canvasWidth - contentWidth) / 2, (canvasHeight - contentHeight) / 2),
                WatermarkPosition.BottomRight or _ => new Point(canvasWidth - contentWidth - paddingX, canvasHeight - contentHeight - paddingY),
            };
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
            try
            {
                using var baseImage = await Image.LoadAsync<Rgba32>(baseImagePath);
                using var watermark = await Image.LoadAsync<Rgba32>(watermarkImagePath);

                int wmWidth = (int)(baseImage.Width * scale);
                int wmHeight = (int)(watermark.Height * (wmWidth / (float)watermark.Width));
                watermark.Mutate(ctx => ctx.Resize(wmWidth, wmHeight));

                int paddingX = (int)(baseImage.Width * padding);
                int paddingY = (int)(baseImage.Height * padding);
                var location = ResolveLocation(position, baseImage.Width, baseImage.Height, wmWidth, wmHeight, paddingX, paddingY);

                baseImage.Mutate(ctx => ctx.DrawImage(watermark, location, opacity));

                await baseImage.SaveAsync(outputPath);

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
            float rotation = 0f,
            bool addStroke = true,
            Color? strokeColor = null,
            float strokeWidth = 2f,
            bool addShadow = true,
            Color? shadowColor = null,
            float shadowOffsetX = 3f,
            float shadowOffsetY = 3f)
                {
            try
            {
                using var baseImage = Image.Load<Rgba32>(baseImagePath);

                float actualFontSize = fontSize < 1f ? baseImage.Height * fontSize : fontSize;
                var font = ResolveFont(fontFamily, actualFontSize, FontStyle.Bold);

                var textOptions = new RichTextOptions(font);
                var textSize = TextMeasurer.MeasureSize(text, textOptions);
                int textWidth = (int)Math.Ceiling(textSize.Width);
                int textHeight = (int)Math.Ceiling(textSize.Height);

                int paddingX = (int)(baseImage.Width * padding);
                int paddingY = (int)(baseImage.Height * padding);
                var location = ResolveLocation(position, baseImage.Width, baseImage.Height, textWidth, textHeight, paddingX, paddingY);

                var baseColor = color ?? Color.White;
                var rgba = baseColor.ToPixel<Rgba32>();
                var fillColor = Color.FromRgba(rgba.R, rgba.G, rgba.B, (byte)(opacity * 255));

                // Настройка цветов для обводки и тени
                var strokeColorFinal = strokeColor ?? Color.Black;
                var shadowColorFinal = shadowColor ?? Color.Black;

                baseImage.Mutate(ctx =>
                {
                    // Применяем трансформацию для поворота, если нужно
                    if (rotation != 0f)
                    {
                        var center = new PointF(location.X + textWidth / 2f, location.Y + textHeight / 2f);
                        var matrix = Matrix3x2Extensions.CreateRotationDegrees(rotation, center);
                        ctx.SetDrawingTransform(matrix);
                    }

                    // Рисуем тень (если включена)
                    if (addShadow)
                    {
                        var shadowLocation = new PointF(
                            location.X + shadowOffsetX,
                            location.Y + shadowOffsetY
                        );

                        // Тень рисуем с той же прозрачностью, но более темную
                        var shadowRgba = shadowColorFinal.ToPixel<Rgba32>();
                        var shadowColorWithOpacity = Color.FromRgba(
                            shadowRgba.R,
                            shadowRgba.G,
                            shadowRgba.B,
                            (byte)(opacity * 0.5 * 255) // Делаем тень более прозрачной
                        );

                        ctx.DrawText(text, font, shadowColorWithOpacity, shadowLocation);
                    }

                    // Рисуем обводку (контур)
                    if (addStroke)
                    {
                        var strokeRgba = strokeColorFinal.ToPixel<Rgba32>();
                        var strokeColorWithOpacity = Color.FromRgba(
                            strokeRgba.R,
                            strokeRgba.G,
                            strokeRgba.B,
                            (byte)(opacity * 255)
                        );

                        // Создаем обводку путем многократного рисования текста со смещением
                        // Рисуем текст с обводкой как набор смещенных копий
                        var strokeOptions = new RichTextOptions(font)
                        {
                            Origin = new PointF(location.X, location.Y),
                        };

                        // Рисуем обводку в 8 направлениях (вокруг текста)
                        float[] offsets = new float[] { -strokeWidth, 0, strokeWidth };
                        foreach (var dx in offsets)
                        {
                            foreach (var dy in offsets)
                            {
                                if (dx == 0 && dy == 0) continue; // Пропускаем центр
                                var strokeLocation = new PointF(
                                    location.X + dx,
                                    location.Y + dy
                                );
                                ctx.DrawText(text, font, strokeColorWithOpacity, strokeLocation);
                            }
                        }
                    }

                    // Рисуем основной текст поверх всего
                    ctx.DrawText(text, font, fillColor, location);
                });

                baseImage.Save(outputPath);

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
            float rotation = 0f,
            bool addStroke = true,
            Color? strokeColor = null,
            float strokeWidth = 2f,
            bool addShadow = true,
            Color? shadowColor = null,
            float shadowOffsetX = 3f,
            float shadowOffsetY = 3f)
        {
            return await Task.Run(() =>
                ApplyTextWatermark(baseImagePath, outputPath, text, fontFamily, fontSize, position, opacity, color, padding, rotation, addStroke, strokeColor, strokeWidth, addShadow, shadowColor, shadowOffsetX, shadowOffsetY));
        }
    }
}
