using SkiaSharp;
using System;

namespace Frontend.Desktop
{
    /// <summary>
    /// Handles rendering of the scene graph to Skia canvas
    /// </summary>
    public class SkRenderer
    {
        private SKCanvas _canvas;
        private UiDsl.SceneNode? _rootNode;

        public SkRenderer(SKCanvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        }

        public void SetSceneGraph(UiDsl.SceneNode rootNode)
        {
            _rootNode = rootNode;
        }

        public void Render()
        {
            if (_rootNode == null)
                return;

            // Clear the canvas
            _canvas.Clear(SKColors.White);

            // Render the scene graph
            RenderNode(_rootNode, SKPoint.Empty);

            // Flush the canvas
            _canvas.Flush();
        }

        private void RenderNode(UiDsl.SceneNode node, SKPoint parentPosition)
        {
            // Calculate absolute position
            var position = new SKPoint(
                parentPosition.X + node.X,
                parentPosition.Y + node.Y
            );

            switch (node.Type)
            {
                case UiDsl.NodeType.Container:
                    RenderContainer(node, position);
                    break;
                case UiDsl.NodeType.Text:
                    RenderText(node, position);
                    break;
                case UiDsl.NodeType.Rectangle:
                    RenderRectangle(node, position);
                    break;
                case UiDsl.NodeType.Circle:
                    RenderCircle(node, position);
                    break;
                case UiDsl.NodeType.Image:
                    RenderImage(node, position);
                    break;
            }

            // Render children
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    RenderNode(child, position);
                }
            }
        }

        private void RenderContainer(UiDsl.SceneNode node, SKPoint position)
        {
            // Container may have a background color or border
            if (node.BackgroundColor.HasValue)
            {
                using var paint = new SKPaint
                {
                    Color = node.BackgroundColor.Value,
                    Style = SKPaintStyle.Fill
                };
                _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, paint);
            }

            if (node.BorderWidth > 0 && node.BorderColor.HasValue)
            {
                using var paint = new SKPaint
                {
                    Color = node.BorderColor.Value,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = node.BorderWidth
                };
                _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, paint);
            }
        }

        private void RenderText(UiDsl.SceneNode node, SKPoint position)
        {
            if (string.IsNullOrEmpty(node.Text))
                return;

            using var font = new SKFont
            {
                Size = node.FontSize > 0 ? node.FontSize : 16
            };

            using var paint = new SKPaint
            {
                Color = node.TextColor ?? SKColors.Black,
                IsAntialias = true
            };

            _canvas.DrawText(node.Text, position.X, position.Y + font.Size, SKTextAlign.Left, font, paint);
        }

        private void RenderRectangle(UiDsl.SceneNode node, SKPoint position)
        {
            using var paint = new SKPaint
            {
                Color = node.FillColor ?? SKColors.Gray,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, paint);

            // Add border if specified
            if (node.BorderWidth > 0 && node.BorderColor.HasValue)
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.Color = node.BorderColor.Value;
                paint.StrokeWidth = node.BorderWidth;
                _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, paint);
            }
        }

        private void RenderCircle(UiDsl.SceneNode node, SKPoint position)
        {
            float radius = Math.Min(node.Width, node.Height) / 2;
            float centerX = position.X + node.Width / 2;
            float centerY = position.Y + node.Height / 2;

            using var paint = new SKPaint
            {
                Color = node.FillColor ?? SKColors.Gray,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            _canvas.DrawCircle(centerX, centerY, radius, paint);

            // Add border if specified
            if (node.BorderWidth > 0 && node.BorderColor.HasValue)
            {
                paint.Style = SKPaintStyle.Stroke;
                paint.Color = node.BorderColor.Value;
                paint.StrokeWidth = node.BorderWidth;
                _canvas.DrawCircle(centerX, centerY, radius, paint);
            }
        }

        private void RenderImage(UiDsl.SceneNode node, SKPoint position)
        {
            if (string.IsNullOrEmpty(node.ImagePath))
                return;

            try
            {
                using var bitmap = SKBitmap.Decode(node.ImagePath);
                _canvas.DrawBitmap(bitmap, new SKRect(position.X, position.Y, position.X + node.Width, position.Y + node.Height));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading image {node.ImagePath}: {ex.Message}");
            }
        }
    }
} 