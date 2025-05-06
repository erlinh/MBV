using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace Frontend.Desktop.UiDsl
{
    /// <summary>
    /// Parser for JSX-like syntax for MBV UI
    /// </summary>
    public class JsxParser
    {
        private readonly Dictionary<string, Func<Dictionary<string, string>, SceneNode>> _componentFactories = 
            new Dictionary<string, Func<Dictionary<string, string>, SceneNode>>(StringComparer.OrdinalIgnoreCase);
            
        private StreamWriter _logFile;
        
        public JsxParser()
        {
            // Create log file
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "jsx_parser.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            _logFile = new StreamWriter(logPath, true);
            _logFile.AutoFlush = true;
            
            LogToFile("JSX Parser initialized");
            
            // Register built-in components
            RegisterBuiltInComponents();
            LogToFile("Initialized JSX parser with built-in components");
        }
        
        ~JsxParser()
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

        /// <summary>
        /// Parse a file containing JSX-like UI description
        /// </summary>
        public SceneNode ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"JSX UI file not found: {filePath}");

            string content = File.ReadAllText(filePath);
            LogToFile($"Parsing JSX file: {filePath} ({content.Length} bytes)");
            return ParseString(content);
        }

        /// <summary>
        /// Parse a string containing JSX-like UI description
        /// </summary>
        public SceneNode ParseString(string content)
        {
            LogToFile("Parsing JSX string");
            
            // Clean up content
            content = StripComments(content.Trim());
            
            // Basic manual parsing for XML-like structure
            var root = ParseElement(content, 0, out _);
            if (root == null)
            {
                string error = "Failed to parse JSX: no root element found";
                LogToFile(error);
                throw new FormatException(error);
            }
            
            LogToFile($"Successfully parsed JSX with root node of type {root.Type} with {root.Children?.Count ?? 0} children");
            
            // Debug output - print node tree
            PrintNodeTree(root, 0);
            
            return root;
        }

        /// <summary>
        /// Register a custom component factory
        /// </summary>
        public void RegisterComponent(string name, Func<Dictionary<string, string>, SceneNode> factory)
        {
            _componentFactories[name] = factory;
            LogToFile($"Registered component: {name}");
        }

        private string StripComments(string content)
        {
            // Remove JSX-style comments {/* ... */}
            return Regex.Replace(content, @"\{\s*/\*.*?\*/\s*\}", "", RegexOptions.Singleline);
        }

        private SceneNode? ParseElement(string content, int startIndex, out int endIndex)
        {
            endIndex = startIndex;
            
            // Skip whitespace
            while (endIndex < content.Length && char.IsWhiteSpace(content[endIndex]))
                endIndex++;
                
            if (endIndex >= content.Length || content[endIndex] != '<')
                return null;
                
            // Find tag name
            int tagNameStart = endIndex + 1;
            int tagNameEnd = tagNameStart;
            
            while (tagNameEnd < content.Length && 
                  (char.IsLetterOrDigit(content[tagNameEnd]) || content[tagNameEnd] == '_'))
            {
                tagNameEnd++;
            }
            
            if (tagNameEnd == tagNameStart)
                return null;
                
            string tagName = content.Substring(tagNameStart, tagNameEnd - tagNameStart);
            LogToFile($"Parsing element: <{tagName}>");
            
            // Find the end of opening tag
            int openTagEnd = content.IndexOf('>', tagNameEnd);
            if (openTagEnd < 0)
            {
                LogToFile($"Error: No closing '>' found for tag <{tagName}>");
                return null;
            }
                
            // Check if it's a self-closing tag
            bool isSelfClosing = content[openTagEnd - 1] == '/';
            int propsEnd = isSelfClosing ? openTagEnd - 1 : openTagEnd;
            
            // Parse attributes
            string propsText = content.Substring(tagNameEnd, propsEnd - tagNameEnd);
            var props = ParseProps(propsText);
            
            // Create the node
            SceneNode node;
            if (_componentFactories.TryGetValue(tagName, out var factory))
            {
                LogToFile($"Creating custom component: {tagName}");
                node = factory(props);
            }
            else
            {
                LogToFile($"Creating standard node: {tagName}");
                node = CreateNodeFromTag(tagName, props);
            }
            
            if (isSelfClosing)
            {
                LogToFile($"Processing self-closing tag: <{tagName}/>");
                endIndex = openTagEnd + 1;
                return node;
            }
            
            // Parse children for non-self-closing tags
            int childrenStart = openTagEnd + 1;
            int currentPos = childrenStart;
            
            // Find the closing tag
            string closingTag = $"</{tagName}>";
            int closingTagPos = -1;
            int depth = 1; // We are already inside one level of this tag
            
            while (currentPos < content.Length)
            {
                // Check for nested opening tag of same type
                if (currentPos + tagName.Length + 1 < content.Length && 
                    content[currentPos] == '<' && 
                    content.Substring(currentPos + 1, tagName.Length) == tagName &&
                    !char.IsLetterOrDigit(content[currentPos + tagName.Length + 1]))
                {
                    // Check if it's not a self-closing tag
                    int nextClosePos = content.IndexOf('>', currentPos);
                    if (nextClosePos > 0 && content[nextClosePos - 1] != '/')
                    {
                        depth++;
                        LogToFile($"Found nested opening <{tagName}>, depth now: {depth}");
                    }
                }
                
                // Check for closing tag
                if (currentPos + closingTag.Length <= content.Length && 
                    content.Substring(currentPos, closingTag.Length) == closingTag)
                {
                    depth--;
                    LogToFile($"Found closing </{tagName}>, depth now: {depth}");
                    if (depth == 0)
                    {
                        closingTagPos = currentPos;
                        break;
                    }
                }
                
                currentPos++;
            }
            
            if (closingTagPos < 0)
            {
                LogToFile($"Warning: No closing tag found for <{tagName}>");
                
                // Special case for elements that are often self-closing
                if (tagName.ToLower() == "circle" || tagName.ToLower() == "image" || 
                    tagName.ToLower() == "text" || tagName.ToLower() == "box" ||
                    tagName.ToLower() == "slot")
                {
                    // Treat as implicitly self-closed
                    LogToFile($"Treating <{tagName}> as implicitly self-closed");
                    endIndex = openTagEnd + 1;
                    return node;
                }
                
                endIndex = openTagEnd + 1;
                return node;
            }
            
            // Process children content
            string childrenContent = content.Substring(childrenStart, closingTagPos - childrenStart);
            
            // Special case for text nodes
            if (node.Type == NodeType.Text && !childrenContent.Trim().StartsWith("<"))
            {
                node.Text = childrenContent.Trim();
                LogToFile($"Set text content: \"{node.Text}\"");
            }
            else if (!string.IsNullOrWhiteSpace(childrenContent))
            {
                // Parse child elements
                int childPos = 0;
                while (childPos < childrenContent.Length)
                {
                    // Skip whitespace
                    while (childPos < childrenContent.Length && char.IsWhiteSpace(childrenContent[childPos]))
                        childPos++;
                        
                    if (childPos >= childrenContent.Length)
                        break;
                        
                    if (childrenContent[childPos] == '<')
                    {
                        var childNode = ParseElement(childrenContent, childPos, out int childEndPos);
                        if (childNode != null)
                        {
                            node.AddChild(childNode);
                            LogToFile($"Added child node of type {childNode.Type} to {node.Type}");
                        }
                        childPos = childEndPos;
                    }
                    else
                    {
                        // Skip to next potential tag
                        childPos = childrenContent.IndexOf('<', childPos);
                        if (childPos < 0) childPos = childrenContent.Length;
                    }
                }
            }
            
            endIndex = closingTagPos + closingTag.Length;
            LogToFile($"Finished parsing <{tagName}> with {node.Children?.Count ?? 0} children");
            return node;
        }

        private Dictionary<string, string> ParseProps(string propsText)
        {
            var props = new Dictionary<string, string>();
            
            if (string.IsNullOrWhiteSpace(propsText))
                return props;
                
            // Match attributes using a regex for name=value pairs
            var propRegex = new Regex(@"\s+(?<n>[a-zA-Z][a-zA-Z0-9]*)(?:=(?<value>""[^""]*""|'[^']*'|\{[^\}]*\}))?");
            var matches = propRegex.Matches(propsText);
            
            foreach (Match match in matches)
            {
                string name = match.Groups["n"].Value;
                
                string value = match.Groups["value"].Success 
                    ? match.Groups["value"].Value 
                    : "true"; // Boolean props without values default to true
                
                // Clean up the value
                if (value.StartsWith("\"") && value.EndsWith("\"") ||
                    value.StartsWith("'") && value.EndsWith("'"))
                {
                    value = value.Substring(1, value.Length - 2);
                }
                else if (value.StartsWith("{") && value.EndsWith("}"))
                {
                    // For JSX-style expressions, strip the braces
                    value = value.Substring(1, value.Length - 2);
                }
                
                props[name] = value;
                LogToFile($"Parsed prop: {name}={value}");
            }
            
            return props;
        }

        private SceneNode CreateNodeFromTag(string tagName, Dictionary<string, string> props)
        {
            NodeType nodeType = tagName.ToLower() switch
            {
                "view" => NodeType.Container,
                "text" => NodeType.Text,
                "box" => NodeType.Rectangle,
                "circle" => NodeType.Circle,
                "image" => NodeType.Image,
                "button" => NodeType.Button,
                "input" => NodeType.Input,
                "checkbox" => NodeType.Checkbox,
                "radio" => NodeType.RadioButton,
                "radiobutton" => NodeType.RadioButton,
                "slider" => NodeType.Slider,
                "dropdown" => NodeType.DropDown,
                "select" => NodeType.DropDown,
                "slot" => NodeType.Container, // Handle slot tags as container nodes
                _ => throw new FormatException($"Unknown tag: {tagName}")
            };
            
            var node = new SceneNode { Type = nodeType };
            LogToFile($"Created node of type {nodeType}");
            
            // Default values for width and height to ensure visibility
            if (!props.ContainsKey("width")) node.Width = 100;
            if (!props.ContainsKey("height")) node.Height = 30;
            
            // For slot nodes, set an identifier
            if (tagName.ToLower() == "slot")
            {
                if (props.ContainsKey("name"))
                {
                    node.Id = $"slot_{props["name"]}";
                }
                else
                {
                    node.Id = "default_slot";
                }
            }
            
            // Set properties based on props dictionary
            foreach (var prop in props)
            {
                SetNodeProperty(node, prop.Key, prop.Value);
            }
            
            return node;
        }

        private void SetNodeProperty(SceneNode node, string propName, string propValue)
        {
            try
            {
                // Handle empty values properly
                if (string.IsNullOrWhiteSpace(propValue))
                {
                    LogToFile($"Empty value for property {propName}, using appropriate default");
                    
                    // For numeric properties, use sensible defaults
                    switch (propName.ToLower())
                    {
                        case "x":
                        case "y":
                            // Default position to 0
                            if (propName.ToLower() == "x") node.X = 0;
                            if (propName.ToLower() == "y") node.Y = 0;
                            break;
                            
                        case "width":
                            // Default width based on type
                            node.Width = node.Type == NodeType.Text ? 100 : 
                                       node.Type == NodeType.Button ? 150 : 200;
                            break;
                            
                        case "height":
                            // Default height based on type
                            node.Height = node.Type == NodeType.Text ? 30 : 
                                        node.Type == NodeType.Button ? 40 : 30;
                            break;
                            
                        case "fontsize":
                        case "size":
                            // Default font size
                            node.FontSize = 14;
                            break;
                            
                        case "borderwidth":
                            // Default border width
                            node.BorderWidth = 1;
                            break;
                            
                        case "borderradius":
                        case "radius":
                            // Default radius
                            node.BorderRadius = 5;
                            break;
                            
                        // For other properties, just record we're skipping
                        default:
                            LogToFile($"Skipping empty property {propName}");
                            break;
                    }
                    
                    return;
                }
                
                switch (propName.ToLower())
                {
                    case "id":
                        node.Id = propValue;
                        break;
                    case "x":
                        node.X = float.Parse(propValue);
                        break;
                    case "y":
                        node.Y = float.Parse(propValue);
                        break;
                    case "width":
                        node.Width = float.Parse(propValue);
                        break;
                    case "height":
                        node.Height = float.Parse(propValue);
                        break;
                    case "backgroundcolor":
                    case "background":
                        node.BackgroundColor = ParseColor(propValue);
                        break;
                    case "bordercolor":
                    case "border":
                        node.BorderColor = ParseColor(propValue);
                        break;
                    case "borderwidth":
                        node.BorderWidth = float.Parse(propValue);
                        break;
                    case "borderradius":
                    case "radius":
                        node.BorderRadius = float.Parse(propValue);
                        break;
                    case "fillcolor":
                    case "fill":
                        node.FillColor = ParseColor(propValue);
                        break;
                    case "textcolor":
                    case "color":
                        node.TextColor = ParseColor(propValue);
                        break;
                    case "fontsize":
                    case "size":
                        node.FontSize = float.Parse(propValue);
                        break;
                    case "text":
                        node.Text = propValue;
                        break;
                    case "src":
                    case "image":
                        node.ImagePath = propValue;
                        break;
                    case "visible":
                        node.Visible = bool.Parse(propValue);
                        break;
                    case "align":
                        node.Align = propValue;
                        break;
                    case "justify":
                    case "justifycontent":
                        node.JustifyContent = propValue;
                        break;
                    case "margin":
                        node.Margin = float.Parse(propValue);
                        break;
                    case "padding":
                        node.Padding = float.Parse(propValue);
                        break;
                    // Interactive properties
                    case "value":
                        node.Value = propValue;
                        break;
                    case "placeholder":
                        node.Placeholder = propValue;
                        break;
                    case "ischecked":
                        node.IsChecked = bool.Parse(propValue);
                        break;
                    case "isselected":
                        node.IsSelected = bool.Parse(propValue);
                        break;
                    case "groupname":
                        node.GroupName = propValue;
                        break;
                    case "minvalue":
                        node.MinValue = float.Parse(propValue);
                        break;
                    case "maxvalue":
                        node.MaxValue = float.Parse(propValue);
                        break;
                    case "currentvalue":
                        node.CurrentValue = float.Parse(propValue);
                        break;
                    // Event handlers
                    case "onclick":
                        node.OnClick = propValue;
                        break;
                    case "onhover":
                        node.OnHover = propValue;
                        break;
                    case "onfocus":
                        node.OnFocus = propValue;
                        break;
                    case "onchange":
                        node.OnChange = propValue;
                        break;
                    case "onblur":
                        node.OnBlur = propValue;
                        break;
                    default:
                        LogToFile($"Warning: Unknown property '{propName}' with value '{propValue}'");
                        break;
                }
                LogToFile($"Set property {propName}={propValue}");
            }
            catch (Exception ex)
            {
                LogToFile($"Error setting property {propName}={propValue}: {ex.Message}");
            }
        }

        private SKColor? ParseColor(string colorStr)
        {
            if (string.IsNullOrWhiteSpace(colorStr))
                return null;
                
            // Handle predefined colors - case insensitive matching
            foreach (var colorProp in typeof(SKColors).GetProperties())
            {
                if (string.Equals(colorProp.Name, colorStr, StringComparison.OrdinalIgnoreCase))
                {
                    return (SKColor)colorProp.GetValue(null);
                }
            }
            
            // Special case hardcoded common colors
            if (string.Equals(colorStr, "white", StringComparison.OrdinalIgnoreCase))
                return SKColors.White;
            if (string.Equals(colorStr, "black", StringComparison.OrdinalIgnoreCase))
                return SKColors.Black;
            if (string.Equals(colorStr, "red", StringComparison.OrdinalIgnoreCase))
                return SKColors.Red;
            if (string.Equals(colorStr, "green", StringComparison.OrdinalIgnoreCase))
                return SKColors.Green;
            if (string.Equals(colorStr, "blue", StringComparison.OrdinalIgnoreCase))
                return SKColors.Blue;
            if (string.Equals(colorStr, "yellow", StringComparison.OrdinalIgnoreCase))
                return SKColors.Yellow;
            if (string.Equals(colorStr, "gray", StringComparison.OrdinalIgnoreCase) || 
                string.Equals(colorStr, "grey", StringComparison.OrdinalIgnoreCase))
                return SKColors.Gray;
            
            // Handle hex colors (#RRGGBB or #AARRGGBB)
            if (colorStr.StartsWith("#"))
            {
                if (SKColor.TryParse(colorStr, out SKColor color))
                {
                    return color;
                }
            }
            
            // Handle rgb(r,g,b) and rgba(r,g,b,a) formats
            if (colorStr.StartsWith("rgb"))
            {
                var rgbMatch = Regex.Match(colorStr, @"rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([0-9.]+))?\)");
                if (rgbMatch.Success)
                {
                    int r = int.Parse(rgbMatch.Groups[1].Value);
                    int g = int.Parse(rgbMatch.Groups[2].Value);
                    int b = int.Parse(rgbMatch.Groups[3].Value);
                    
                    if (rgbMatch.Groups[4].Success)
                    {
                        float a = float.Parse(rgbMatch.Groups[4].Value);
                        return new SKColor((byte)r, (byte)g, (byte)b, (byte)(a * 255));
                    }
                    else
                    {
                        return new SKColor((byte)r, (byte)g, (byte)b);
                    }
                }
            }
            
            // Default to black if we couldn't parse
            LogToFile($"Warning: Could not parse color '{colorStr}', using black instead");
            return SKColors.Black; // Better default than null
        }

        private void RegisterBuiltInComponents()
        {
            // Button component with default styling
            RegisterComponent("Button", props => {
                var node = new SceneNode
                {
                    Type = NodeType.Rectangle,
                    Width = props.ContainsKey("width") ? float.Parse(props["width"]) : 150,
                    Height = props.ContainsKey("height") ? float.Parse(props["height"]) : 40,
                    FillColor = props.ContainsKey("fill") ? ParseColor(props["fill"]) : new SKColor(59, 130, 246),
                    BorderColor = props.ContainsKey("border") ? ParseColor(props["border"]) : new SKColor(29, 78, 216),
                    BorderWidth = props.ContainsKey("borderWidth") ? float.Parse(props["borderWidth"]) : 2
                };
                
                if (props.ContainsKey("id")) node.Id = props["id"];
                if (props.ContainsKey("x")) node.X = float.Parse(props["x"]);
                if (props.ContainsKey("y")) node.Y = float.Parse(props["y"]);
                if (props.ContainsKey("onClick")) node.OnClick = props["onClick"];
                
                // Create the button text if specified
                if (props.ContainsKey("text"))
                {
                    var textNode = new SceneNode
                    {
                        Type = NodeType.Text,
                        X = 20,
                        Y = 12,
                        Text = props["text"],
                        FontSize = props.ContainsKey("fontSize") ? float.Parse(props["fontSize"]) : 16,
                        TextColor = props.ContainsKey("textColor") ? ParseColor(props["textColor"]) : SKColors.White
                    };
                    node.AddChild(textNode);
                }
                
                return node;
            });
            
            // Card component for content cards
            RegisterComponent("Card", props => {
                var node = new SceneNode
                {
                    Type = NodeType.Container,
                    Width = props.ContainsKey("width") ? float.Parse(props["width"]) : 300,
                    Height = props.ContainsKey("height") ? float.Parse(props["height"]) : 200,
                    BackgroundColor = props.ContainsKey("background") ? 
                        ParseColor(props["background"]) : SKColors.White,
                    BorderColor = props.ContainsKey("border") ?
                        ParseColor(props["border"]) : new SKColor(226, 232, 240),
                    BorderWidth = props.ContainsKey("borderWidth") ? float.Parse(props["borderWidth"]) : 1,
                    Padding = props.ContainsKey("padding") ? float.Parse(props["padding"]) : 16
                };
                
                if (props.ContainsKey("id")) node.Id = props["id"];
                if (props.ContainsKey("x")) node.X = float.Parse(props["x"]);
                if (props.ContainsKey("y")) node.Y = float.Parse(props["y"]);
                
                return node;
            });
            
            // Header component for section headers
            RegisterComponent("Header", props => {
                var node = new SceneNode
                {
                    Type = NodeType.Text,
                    Text = props.ContainsKey("text") ? props["text"] : "Header",
                    FontSize = props.ContainsKey("fontSize") ? float.Parse(props["fontSize"]) : 24,
                    TextColor = props.ContainsKey("color") ? 
                        ParseColor(props["color"]) : new SKColor(15, 23, 42)
                };
                
                if (props.ContainsKey("id")) node.Id = props["id"];
                if (props.ContainsKey("x")) node.X = float.Parse(props["x"]);
                if (props.ContainsKey("y")) node.Y = float.Parse(props["y"]);
                
                return node;
            });
        }

        private void PrintNodeTree(SceneNode node, int indent)
        {
            var indentStr = new string(' ', indent * 2);
            LogToFile($"{indentStr}Node: {node.Type} Id: {node.Id ?? "none"} Pos: ({node.X}, {node.Y}) Size: {node.Width}x{node.Height}");
            
            if (!string.IsNullOrEmpty(node.Text))
            {
                LogToFile($"{indentStr}  Text: \"{node.Text}\"");
            }
            
            if (node.Children != null && node.Children.Count > 0)
            {
                LogToFile($"{indentStr}  Children: {node.Children.Count}");
                foreach (var child in node.Children)
                {
                    PrintNodeTree(child, indent + 1);
                }
            }
        }
    }
} 