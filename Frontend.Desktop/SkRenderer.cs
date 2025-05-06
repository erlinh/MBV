using SkiaSharp;
using System;
using System.IO;

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
        private StreamWriter _logFile;

        public SkRenderer(SKCanvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            
            // Create log file
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "renderer.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            _logFile = new StreamWriter(logPath, true);
            _logFile.AutoFlush = true;
            
            LogToFile("SkRenderer initialized");
        }
        
        ~SkRenderer()
        {
            // Ensure log file is closed
            _logFile?.Close();
        }
        
        private void LogToFile(string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _logFile.WriteLine($"[{timestamp}] {message}");
            // Also log to console for now
            Console.WriteLine(message);
        }

        public void SetSceneGraph(UiDsl.SceneNode rootNode)
        {
            _rootNode = rootNode;
            LogToFile($"Scene graph set: {rootNode.Type} with {rootNode.Children?.Count ?? 0} children");
        }
        
        public void SetHoveredNode(UiDsl.SceneNode? hoveredNode)
        {
            _hoveredNode = hoveredNode;
        }

        public void Render()
        {
            if (_rootNode == null)
            {
                LogToFile("Cannot render: root node is null");
                return;
            }

            // Clear the canvas
            _canvas.Clear(SKColors.White);
            LogToFile("Starting scene rendering");

            // Render the scene graph
            RenderNode(_rootNode, SKPoint.Empty);

            // Flush the canvas
            _canvas.Flush();
            LogToFile("Scene rendering complete");
        }

        private void RenderNode(UiDsl.SceneNode node, SKPoint parentPosition)
        {
            if (!node.Visible)
            {
                LogToFile($"Node {node.Type} {node.Id ?? "unknown"} is not visible, skipping render");
                return;
            }
                
            // Calculate absolute position
            var position = new SKPoint(
                parentPosition.X + node.X,
                parentPosition.Y + node.Y
            );

            // Check if this is the hovered node
            bool isHovered = _hoveredNode == node;
            bool isClickable = !string.IsNullOrEmpty(node.OnClick);
            
            LogToFile($"Rendering {node.Type} at ({position.X}, {position.Y}) size: {node.Width}x{node.Height} id: {node.Id ?? "none"} onClick: {node.OnClick ?? "none"}");
            
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
                case UiDsl.NodeType.Input:
                    RenderInput(node, position, isHovered, true);
                    break;
                case UiDsl.NodeType.Checkbox:
                    RenderCheckbox(node, position, isHovered, true);
                    break;
                case UiDsl.NodeType.RadioButton:
                    RenderRadioButton(node, position, isHovered, true);
                    break;
                case UiDsl.NodeType.Slider:
                    RenderSlider(node, position, isHovered, true);
                    break;
                case UiDsl.NodeType.DropDown:
                    RenderDropDown(node, position, isHovered, true);
                    break;
                default:
                    LogToFile($"Warning: Unknown node type {node.Type}");
                    break;
            }

            // Render children
            if (node.Children != null && node.Children.Count > 0)
            {
                LogToFile($"Rendering {node.Children.Count} children of {node.Type} {node.Id ?? ""}");
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
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                
                if (node.BorderRadius > 0)
                {
                    // Draw with rounded corners
                    _canvas.DrawRoundRect(position.X, position.Y, node.Width, node.Height, node.BorderRadius, node.BorderRadius, paint);
                }
                else
                {
                    _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, paint);
                }
            }

            if (node.BorderWidth > 0 && node.BorderColor.HasValue)
            {
                using var paint = new SKPaint
                {
                    Color = node.BorderColor.Value,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = node.BorderWidth,
                    IsAntialias = true
                };
                
                if (node.BorderRadius > 0)
                {
                    // Draw with rounded corners
                    _canvas.DrawRoundRect(position.X, position.Y, node.Width, node.Height, node.BorderRadius, node.BorderRadius, paint);
                }
                else
                {
                    _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, paint);
                }
            }
            
            // If clickable and hovered, show cursor feedback
            if (isClickable && isHovered)
            {
                using var paint = new SKPaint
                {
                    Color = new SKColor(255, 255, 255, 50), // Slight white overlay
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                
                if (node.BorderRadius > 0)
                {
                    // Draw with rounded corners
                    _canvas.DrawRoundRect(position.X, position.Y, node.Width, node.Height, node.BorderRadius, node.BorderRadius, paint);
                }
                else
                {
                    _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, paint);
                }
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
                IsAntialias = true,
                TextAlign = SKTextAlign.Left,
                Typeface = SKTypeface.Default,
                SubpixelText = true  // Enables subpixel rendering for smoother text
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

            // Measure text metrics to calculate proper positioning
            var textBounds = new SKRect();
            paint.MeasureText(node.Text, ref textBounds);
            
            // Calculate proper Y position using text metrics
            // Adjusting for baseline alignment to make text vertical spacing more consistent
            float textYPosition = position.Y + font.Size + Math.Abs(textBounds.Top);
            
            // Draw the text at the proper position
            _canvas.DrawText(node.Text, position.X, textYPosition, paint);
            
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
                    textYPosition + 2, 
                    position.X + textWidth, 
                    textYPosition + 2, 
                    underlinePaint);
            }
            
            // For debugging text bounds
            // using var debugPaint = new SKPaint { Color = SKColors.Red, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            // _canvas.DrawRect(position.X, position.Y, textBounds.Width, Math.Abs(textBounds.Top) + textBounds.Height, debugPaint);
        }

        private void RenderRectangle(UiDsl.SceneNode node, SKPoint position, bool isHovered, bool isClickable)
        {
            // Fill
            if (node.FillColor.HasValue)
            {
                using var fillPaint = new SKPaint
                {
                    Color = node.FillColor.Value,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                
                if (node.BorderRadius > 0)
                {
                    // Draw with rounded corners
                    _canvas.DrawRoundRect(position.X, position.Y, node.Width, node.Height, node.BorderRadius, node.BorderRadius, fillPaint);
                }
                else
                {
                    _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, fillPaint);
                }
            }
            
            // Border
            if (node.BorderWidth > 0 && node.BorderColor.HasValue)
            {
                using var borderPaint = new SKPaint
                {
                    Color = node.BorderColor.Value,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = node.BorderWidth,
                    IsAntialias = true
                };
                
                if (node.BorderRadius > 0)
                {
                    // Draw with rounded corners
                    _canvas.DrawRoundRect(position.X, position.Y, node.Width, node.Height, node.BorderRadius, node.BorderRadius, borderPaint);
                }
                else
                {
                    _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, borderPaint);
                }
            }
            
            // Hover effect
            if (isClickable && isHovered)
            {
                using var highlightPaint = new SKPaint
                {
                    Color = new SKColor(255, 255, 255, 60), // Semi-transparent white overlay
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                
                if (node.BorderRadius > 0)
                {
                    // Draw with rounded corners
                    _canvas.DrawRoundRect(position.X, position.Y, node.Width, node.Height, node.BorderRadius, node.BorderRadius, highlightPaint);
                }
                else
                {
                    _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, highlightPaint);
                }
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
                LogToFile($"Error loading image {node.ImagePath}: {ex.Message}");
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

        // Rendering methods for interactive components
        
        private void RenderInput(UiDsl.SceneNode node, SKPoint position, bool isHovered, bool isClickable)
        {
            // Draw input background
            var bgColor = node.BackgroundColor ?? SKColors.White;
            using var bgPaint = new SKPaint
            {
                Color = bgColor,
                Style = SKPaintStyle.Fill
            };
            _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, bgPaint);
            
            // Draw border
            var borderColor = node.BorderColor ?? new SKColor(203, 213, 225);
            float borderWidth = node.BorderWidth > 0 ? node.BorderWidth : 1;
            
            // Use highlighted border if focused or hovered
            if (isHovered)
            {
                borderColor = new SKColor(59, 130, 246); // Blue highlight
                borderWidth = 2;
            }
            
            using var borderPaint = new SKPaint
            {
                Color = borderColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = borderWidth
            };
            _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, borderPaint);
            
            // Draw text content
            string displayText = !string.IsNullOrEmpty(node.Value) ? node.Value : 
                                !string.IsNullOrEmpty(node.Placeholder) ? node.Placeholder : string.Empty;
                                
            if (!string.IsNullOrEmpty(displayText))
            {
                // Placeholder text uses a lighter color
                var textColor = !string.IsNullOrEmpty(node.Value) ? 
                    (node.TextColor ?? new SKColor(51, 65, 85)) : // Regular text
                    new SKColor(148, 163, 184);  // Placeholder text
                    
                using var textPaint = new SKPaint
                {
                    Color = textColor,
                    IsAntialias = true
                };
                
                using var font = new SKFont
                {
                    Size = node.FontSize
                };
                
                _canvas.DrawText(displayText, position.X + 10, position.Y + (node.Height / 2) + (font.Size / 3), textPaint);
            }
        }
        
        private void RenderCheckbox(UiDsl.SceneNode node, SKPoint position, bool isHovered, bool isClickable)
        {
            // Size and position
            float checkboxSize = 24;
            float x = position.X;
            float y = position.Y;
            
            // Draw checkbox background
            var bgColor = node.IsChecked ? new SKColor(59, 130, 246) : // Blue when checked
                                        (node.BackgroundColor ?? SKColors.White);
            using var bgPaint = new SKPaint
            {
                Color = bgColor,
                Style = SKPaintStyle.Fill
            };
            _canvas.DrawRect(x, y, checkboxSize, checkboxSize, bgPaint);
            
            // Draw border
            var borderColor = node.BorderColor ?? new SKColor(203, 213, 225);
            if (isHovered)
            {
                borderColor = new SKColor(59, 130, 246); // Blue highlight
            }
            
            using var borderPaint = new SKPaint
            {
                Color = borderColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = node.BorderWidth > 0 ? node.BorderWidth : 1
            };
            _canvas.DrawRect(x, y, checkboxSize, checkboxSize, borderPaint);
            
            // Draw checkmark if checked
            if (node.IsChecked)
            {
                using var checkmarkPaint = new SKPaint
                {
                    Color = SKColors.White,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2,
                    IsAntialias = true
                };
                
                // Draw checkmark symbol
                var checkPath = new SKPath();
                checkPath.MoveTo(x + 6, y + 12);
                checkPath.LineTo(x + 10, y + 16);
                checkPath.LineTo(x + 18, y + 8);
                _canvas.DrawPath(checkPath, checkmarkPaint);
            }
            
            // Draw label text if present
            if (!string.IsNullOrEmpty(node.Text))
            {
                using var textPaint = new SKPaint
                {
                    Color = node.TextColor ?? new SKColor(51, 65, 85),
                    IsAntialias = true
                };
                
                using var font = new SKFont
                {
                    Size = node.FontSize > 0 ? node.FontSize : 14
                };
                
                _canvas.DrawText(node.Text, x + checkboxSize + 8, y + (checkboxSize / 2) + (font.Size / 3), textPaint);
            }
        }
        
        private void RenderRadioButton(UiDsl.SceneNode node, SKPoint position, bool isHovered, bool isClickable)
        {
            // Size and position
            float radioSize = 24;
            float x = position.X;
            float y = position.Y;
            float centerX = x + (radioSize / 2);
            float centerY = y + (radioSize / 2);
            float radius = radioSize / 2;
            
            // Draw radio button background
            var bgColor = node.IsSelected ? new SKColor(59, 130, 246) : // Blue when selected
                                        (node.BackgroundColor ?? SKColors.White);
            using var bgPaint = new SKPaint
            {
                Color = bgColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            _canvas.DrawCircle(centerX, centerY, radius, bgPaint);
            
            // Draw border
            var borderColor = node.BorderColor ?? new SKColor(203, 213, 225);
            if (isHovered)
            {
                borderColor = new SKColor(59, 130, 246); // Blue highlight
            }
            
            using var borderPaint = new SKPaint
            {
                Color = borderColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = node.BorderWidth > 0 ? node.BorderWidth : 1,
                IsAntialias = true
            };
            _canvas.DrawCircle(centerX, centerY, radius, borderPaint);
            
            // Draw inner circle if selected
            if (node.IsSelected)
            {
                using var innerCirclePaint = new SKPaint
                {
                    Color = SKColors.White,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                _canvas.DrawCircle(centerX, centerY, radius / 2.5f, innerCirclePaint);
            }
            
            // Draw label text if present
            if (!string.IsNullOrEmpty(node.Text))
            {
                using var textPaint = new SKPaint
                {
                    Color = node.TextColor ?? new SKColor(51, 65, 85),
                    IsAntialias = true
                };
                
                using var font = new SKFont
                {
                    Size = node.FontSize > 0 ? node.FontSize : 14
                };
                
                _canvas.DrawText(node.Text, x + radioSize + 8, y + (radioSize / 2) + (font.Size / 3), textPaint);
            }
        }
        
        private void RenderSlider(UiDsl.SceneNode node, SKPoint position, bool isHovered, bool isClickable)
        {
            // Size and position
            float x = position.X;
            float y = position.Y + (node.Height / 2); // Center vertically
            float trackHeight = 4;
            float handleSize = 16;
            
            // Calculate handle position based on current value
            float percentage = (node.CurrentValue - node.MinValue) / (node.MaxValue - node.MinValue);
            percentage = Math.Clamp(percentage, 0, 1); // Ensure it's between 0 and 1
            float handleX = x + (percentage * (node.Width - handleSize));
            
            // Draw track background (gray)
            using var trackBgPaint = new SKPaint
            {
                Color = new SKColor(226, 232, 240),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            _canvas.DrawRect(x, y - (trackHeight / 2), node.Width, trackHeight, trackBgPaint);
            
            // Draw active track (blue)
            using var activeTrackPaint = new SKPaint
            {
                Color = new SKColor(59, 130, 246),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            _canvas.DrawRect(x, y - (trackHeight / 2), handleX + (handleSize / 2), trackHeight, activeTrackPaint);
            
            // Draw handle
            using var handlePaint = new SKPaint
            {
                Color = new SKColor(59, 130, 246),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            
            using var handleBorderPaint = new SKPaint
            {
                Color = SKColors.White,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                IsAntialias = true
            };
            
            _canvas.DrawCircle(handleX + (handleSize / 2), y, handleSize / 2, handlePaint);
            _canvas.DrawCircle(handleX + (handleSize / 2), y, handleSize / 2 - 1, handleBorderPaint);
            
            // Draw value label if needed
            if (isHovered && !string.IsNullOrEmpty(node.Text))
            {
                string valueText = $"{node.Text}: {node.CurrentValue}";
                
                using var textPaint = new SKPaint
                {
                    Color = node.TextColor ?? new SKColor(51, 65, 85),
                    IsAntialias = true
                };
                
                using var font = new SKFont
                {
                    Size = node.FontSize > 0 ? node.FontSize : 12
                };
                
                _canvas.DrawText(valueText, x, y - 15, textPaint);
            }
        }
        
        private void RenderDropDown(UiDsl.SceneNode node, SKPoint position, bool isHovered, bool isClickable)
        {
            // Draw dropdown background
            var bgColor = node.BackgroundColor ?? SKColors.White;
            using var bgPaint = new SKPaint
            {
                Color = bgColor,
                Style = SKPaintStyle.Fill
            };
            _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, bgPaint);
            
            // Draw border
            var borderColor = node.BorderColor ?? new SKColor(203, 213, 225);
            if (isHovered)
            {
                borderColor = new SKColor(59, 130, 246); // Blue highlight
            }
            
            using var borderPaint = new SKPaint
            {
                Color = borderColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = node.BorderWidth > 0 ? node.BorderWidth : 1
            };
            _canvas.DrawRect(position.X, position.Y, node.Width, node.Height, borderPaint);
            
            // Draw selected text
            if (!string.IsNullOrEmpty(node.Value))
            {
                using var textPaint = new SKPaint
                {
                    Color = node.TextColor ?? new SKColor(51, 65, 85),
                    IsAntialias = true
                };
                
                using var font = new SKFont
                {
                    Size = node.FontSize > 0 ? node.FontSize : 14
                };
                
                _canvas.DrawText(node.Value, position.X + 10, position.Y + (node.Height / 2) + (font.Size / 3), textPaint);
            }
            else if (!string.IsNullOrEmpty(node.Placeholder))
            {
                // Draw placeholder text in gray
                using var textPaint = new SKPaint
                {
                    Color = new SKColor(148, 163, 184),
                    IsAntialias = true
                };
                
                using var font = new SKFont
                {
                    Size = node.FontSize > 0 ? node.FontSize : 14
                };
                
                _canvas.DrawText(node.Placeholder, position.X + 10, position.Y + (node.Height / 2) + (font.Size / 3), textPaint);
            }
            
            // Draw dropdown arrow
            float arrowSize = 8;
            float arrowX = position.X + node.Width - arrowSize - 10;
            float arrowY = position.Y + (node.Height / 2);
            
            using var arrowPaint = new SKPaint
            {
                Color = node.TextColor ?? new SKColor(51, 65, 85),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            
            var path = new SKPath();
            path.MoveTo(arrowX - arrowSize, arrowY - arrowSize / 2);
            path.LineTo(arrowX + arrowSize, arrowY - arrowSize / 2);
            path.LineTo(arrowX, arrowY + arrowSize / 2);
            path.Close();
            
            _canvas.DrawPath(path, arrowPaint);
        }
    }
} 