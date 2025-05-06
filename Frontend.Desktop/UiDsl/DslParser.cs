using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;

namespace Frontend.Desktop.UiDsl
{
    public class DslParser
    {
        private readonly JsxParser _jsxParser;
        private ComponentManager? _componentManager;

        public DslParser()
        {
            _jsxParser = new JsxParser();
        }

        /// <summary>
        /// Initialize component manager with the path to component files
        /// </summary>
        public void InitComponentManager(string componentPath)
        {
            _componentManager = new ComponentManager(componentPath, this);
        }

        /// <summary>
        /// Parses a .skx file and returns a scene graph
        /// </summary>
        public SceneNode ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"UI DSL file not found: {filePath}");

            string content = File.ReadAllText(filePath);
            return ParseString(content);
        }

        /// <summary>
        /// Parses a string containing UI DSL and returns a scene graph
        /// </summary>
        public SceneNode ParseString(string content)
        {
            try
            {
                // Process component includes if component manager is initialized
                if (_componentManager != null)
                {
                    Console.WriteLine("Processing components before parsing JSX...");
                    content = _componentManager.ProcessIncludes(content);
                    Console.WriteLine("Component processing completed");
                }
                else
                {
                    Console.WriteLine("No component manager initialized, skipping component processing");
                }

                // Support multiple formats: JSON, JSX, and simplified DSL
                string trimmedContent = content.TrimStart();
                
                if (trimmedContent.StartsWith("<"))
                {
                    // JSX format
                    Console.WriteLine("Parsing JSX string");
                    return _jsxParser.ParseString(content);
                }
                else if (trimmedContent.StartsWith("{"))
                {
                    // JSON format
                    Console.WriteLine("Parsing JSON format");
                    return ParseJson(content);
                }
                else
                {
                    // Simplified DSL format
                    Console.WriteLine("Parsing simplified DSL format");
                    return ParseSimplifiedDsl(content);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in ParseString: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw new FormatException($"Failed to parse UI DSL: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parse JSON format
        /// </summary>
        private SceneNode ParseJson(string json)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(), new SKColorJsonConverter() }
            };

            var node = JsonSerializer.Deserialize<SceneNode>(json, options);
            if (node == null)
                throw new FormatException("Failed to deserialize UI DSL JSON");
            
            return node;
        }

        /// <summary>
        /// Parse simplified DSL format (recursive descent parser)
        /// Format: 
        ///   element(type: value, prop: value) {
        ///     child1(prop: value) { ... }
        ///     child2(prop: value) { ... }
        ///   }
        /// </summary>
        private SceneNode ParseSimplifiedDsl(string dsl)
        {
            // For now, we'll use a mock implementation that creates a simple UI
            // In a real implementation, this would be a proper parser
            
            var root = new SceneNode
            {
                Type = NodeType.Container,
                Width = 800,
                Height = 600,
                BackgroundColor = new SKColor(240, 240, 240)
            };

            // Add a header
            var header = new SceneNode
            {
                Type = NodeType.Container,
                X = 0,
                Y = 0,
                Width = 800,
                Height = 60,
                BackgroundColor = new SKColor(59, 130, 246)
            };
            root.AddChild(header);

            // Add title text to header
            var title = new SceneNode
            {
                Type = NodeType.Text,
                X = 20,
                Y = 15,
                Text = "MBV Application",
                FontSize = 24,
                TextColor = SKColors.White
            };
            header.AddChild(title);

            // Add content area
            var content = new SceneNode
            {
                Type = NodeType.Container,
                X = 20,
                Y = 80,
                Width = 760,
                Height = 500
            };
            root.AddChild(content);

            // Add a button
            var button = new SceneNode
            {
                Type = NodeType.Rectangle,
                X = 20,
                Y = 20,
                Width = 150,
                Height = 40,
                FillColor = new SKColor(59, 130, 246),
                BorderColor = new SKColor(29, 78, 216),
                BorderWidth = 2,
                OnClick = "ButtonClicked"
            };
            content.AddChild(button);

            // Add text to button
            var buttonText = new SceneNode
            {
                Type = NodeType.Text,
                X = 45,
                Y = 10,
                Text = "Click Me",
                FontSize = 18,
                TextColor = SKColors.White
            };
            button.AddChild(buttonText);

            return root;
        }
    }

    /// <summary>
    /// JSON converter for SKColor
    /// </summary>
    public class SKColorJsonConverter : JsonConverter<SKColor>
    {
        public override SKColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string colorString = reader.GetString() ?? "#000000";
                if (colorString.StartsWith("#"))
                {
                    // Parse hex format: #RRGGBB or #AARRGGBB
                    if (SKColor.TryParse(colorString, out SKColor color))
                    {
                        return color;
                    }
                }
                else
                {
                    // Try to parse named colors
                    var prop = typeof(SKColors).GetProperty(colorString);
                    if (prop != null)
                    {
                        return (SKColor)prop.GetValue(null)!;
                    }
                }
            }
            
            // Default to black if parsing fails
            return SKColors.Black;
        }

        public override void Write(Utf8JsonWriter writer, SKColor value, JsonSerializerOptions options)
        {
            writer.WriteStringValue($"#{value.Red:X2}{value.Green:X2}{value.Blue:X2}{value.Alpha:X2}");
        }
    }
} 