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
        private UiDsl.SceneNode? _hoveredNode;

        public SkRenderer(SKCanvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        }

        public void SetSceneGraph(UiDsl.SceneNode rootNode)
        {
            _rootNode = rootNode;
        }
        
        public void SetHoveredNode(UiDsl.SceneNode? hoveredNode)
        {
            _hoveredNode = hoveredNode;
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

            // Check if this is the hovered node
            bool isHovered = _hoveredNode == node;
            bool isClickable = !string.IsNullOrEmpty(node.OnClick);
            
            switch (node.Type)
            {
                case UiDsl.NodeType.Container:
                    RenderContainer(node, position, isHovered, isClickable);
                    break;
                case UiDsl.NodeType.Text:
                    RenderText(node, position, isHovered, isClickable);
                    break;
                case UiDsl.NodeType.Rectangle:
                    RenderRectangle(node, position, isHovered, isClickable);
                    break;
                case UiDsl.NodeType.Circle:
                    RenderCircle(node, position, isHovered, isClickable);
                    break;
                case UiDsl.NodeType.Image:
                    RenderImage(node, position, isHovered, isClickable);
                    break;
                case UiDsl.NodeType.Button:
                    RenderButton(node, position, isHovered, true);
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

        private void RenderContainer(UiDsl.SceneNode node, SKPoint position, bool isHovered, bool isClickable)
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
            
            // If clickable and hovered, show cursor feedback
            if (isClickable && isHovered)
            {
                using var paint = new SKPaint
                {
                    Color = new SKColor(255, 255, 255, 50), // Slight white overlay
                    Style = SKPaintStyle.Fill
                };
                _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, paint);
            }
        }

        private void RenderText(UiDsl.SceneNode node, SKPoint position, bool isHovered, bool isClickable)
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
            
            // If clickable, add underline for text
            if (isClickable)
            {
                paint.IsStroke = false;
                if (isHovered)
                {
                    paint.Color = paint.Color.WithAlpha(255); // Full opacity
                }
                else
                {
                    paint.Color = paint.Color.WithAlpha(230); // Slightly transparent
                }
            }

            _canvas.DrawText(node.Text, position.X, position.Y + font.Size, SKTextAlign.Left, font, paint);
            
            // Draw underline for clickable text when hovered
            if (isClickable && isHovered)
            {
                using var underlinePaint = new SKPaint
                {
                    Color = paint.Color,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1,
                    IsAntialias = true
                };
                float textWidth = paint.MeasureText(node.Text);
                _canvas.DrawLine(
                    position.X, 
                    position.Y + font.Size + 2, 
                    position.X + textWidth, 
                    position.Y + font.Size + 2, 
                    underlinePaint);
            }
        }

        private void RenderRectangle(UiDsl.SceneNode node, SKPoint position, bool isHovered, bool isClickable)
        {
            // For clickable elements, adjust fill color when hovered
            SKColor fillColor = node.FillColor ?? SKColors.Gray;
            if (isClickable && isHovered)
            {
                // Brighten the color a bit
                fillColor = new SKColor(
                    (byte)Math.Min(255, fillColor.Red + 20),
                    (byte)Math.Min(255, fillColor.Green + 20),
                    (byte)Math.Min(255, fillColor.Blue + 20)
                );
            }
            
            using var paint = new SKPaint
            {
                Color = fillColor,
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
            
            // Add highlight for clickable elements when hovered
            if (isClickable && isHovered)
            {
                using var highlightPaint = new SKPaint
                {
                    Color = new SKColor(255, 255, 255, 40), // Subtle white highlight
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, highlightPaint);
            }
        }

        private void RenderCircle(UiDsl.SceneNode node, SKPoint position, bool isHovered, bool isClickable)
        {
            float radius = Math.Min(node.Width, node.Height) / 2;
            float centerX = position.X + node.Width / 2;
            float centerY = position.Y + node.Height / 2;

            // Adjust fill color for hover state
            SKColor fillColor = node.FillColor ?? SKColors.Gray;
            if (isClickable && isHovered)
            {
                // Brighten the color a bit
                fillColor = new SKColor(
                    (byte)Math.Min(255, fillColor.Red + 20),
                    (byte)Math.Min(255, fillColor.Green + 20),
                    (byte)Math.Min(255, fillColor.Blue + 20)
                );
            }
            
            using var paint = new SKPaint
            {
                Color = fillColor,
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
            
            // Add highlight for clickable elements when hovered
            if (isClickable && isHovered)
            {
                using var highlightPaint = new SKPaint
                {
                    Color = new SKColor(255, 255, 255, 40), // Subtle white highlight
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                _canvas.DrawCircle(centerX, centerY, radius, highlightPaint);
            }
        }

        private void RenderImage(UiDsl.SceneNode node, SKPoint position, bool isHovered, bool isClickable)
        {
            if (string.IsNullOrEmpty(node.ImagePath))
                return;

            try
            {
                using var bitmap = SKBitmap.Decode(node.ImagePath);
                _canvas.DrawBitmap(bitmap, new SKRect(position.X, position.Y, position.X + node.Width, position.Y + node.Height));
                
                // Add highlight for clickable images when hovered
                if (isClickable && isHovered)
                {
                    using var highlightPaint = new SKPaint
                    {
                        Color = new SKColor(255, 255, 255, 60), // Semi-transparent overlay
                        Style = SKPaintStyle.Fill,
                        IsAntialias = true
                    };
                    _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, highlightPaint);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading image {node.ImagePath}: {ex.Message}");
            }
        }
        
        private void RenderButton(UiDsl.SceneNode node, SKPoint position, bool isHovered, bool isClickable)
        {
            // Use rectangle rendering with button-specific styling
            RenderRectangle(node, position, isHovered, true);
            
            // Additional button effects - shadow when not pressed, no shadow when pressed/hovered
            if (!isHovered)
            {
                // Draw a subtle shadow
                using var shadowPaint = new SKPaint
                {
                    Color = new SKColor(0, 0, 0, 30),
                    Style = SKPaintStyle.Fill,
                    ImageFilter = SKImageFilter.CreateDropShadow(
                        2, 2, 3, 3, new SKColor(0, 0, 0, 60))
                };
                
                _canvas.DrawRect(
                    position.X, position.Y,
                    node.Width, node.Height,
                    shadowPaint);
            }
        }
    }
} 