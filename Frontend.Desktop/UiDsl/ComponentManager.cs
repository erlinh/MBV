using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Frontend.Desktop.UiDsl
{
    /// <summary>
    /// Manages UI components, allows loading and including components in UI templates
    /// </summary>
    public class ComponentManager
    {
        private readonly Dictionary<string, string> _componentCache = new Dictionary<string, string>();
        private readonly string _componentPath;
        private readonly DslParser _parser;
        private readonly StreamWriter _logFile;

        public ComponentManager(string componentPath, DslParser parser)
        {
            _componentPath = componentPath;
            _parser = parser;
            
            // Create log file
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "components.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            _logFile = new StreamWriter(logPath, true);
            _logFile.AutoFlush = true;
            
            LogToFile($"ComponentManager initialized with path: {componentPath}");
            
            LoadComponents();
        }
        
        ~ComponentManager()
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
        /// Loads all component templates from the component directory
        /// </summary>
        private void LoadComponents()
        {
            if (!Directory.Exists(_componentPath))
            {
                LogToFile($"Component directory not found: {_componentPath}");
                return;
            }

            LogToFile($"Loading components from directory: {_componentPath}");
            foreach (var file in Directory.GetFiles(_componentPath, "*.skx"))
            {
                string componentName = Path.GetFileNameWithoutExtension(file);
                _componentCache[componentName] = File.ReadAllText(file);
                LogToFile($"Registered component: {componentName}");
            }
            LogToFile($"Loaded {_componentCache.Count} components");
        }

        /// <summary>
        /// Process component includes in a template
        /// </summary>
        public string ProcessIncludes(string template)
        {
            LogToFile("Processing component includes...");

            // Component include pattern: <include component="Name" prop1="value1" prop2="value2">...</include>
            var includePattern = new Regex(@"<include\s+component=""([^""]+)""\s*([^>]*)>(?:(.*?)</include>)?", 
                RegexOptions.Singleline);
            
            // Property pattern: prop="value"
            var propPattern = new Regex(@"(\w+)=""([^""]*)""");
            
            string result = template;
            bool hasIncludes = true;
            
            // Process includes recursively (limited to 10 levels to prevent infinite recursion)
            int recursionLimit = 10;
            int replacementCount = 0;
            
            try 
            {
                while (hasIncludes && recursionLimit > 0)
                {
                    hasIncludes = false;
                    recursionLimit--;
                    
                    result = includePattern.Replace(result, match =>
                    {
                        try
                        {
                            hasIncludes = true;
                            replacementCount++;
                            
                            string componentName = match.Groups[1].Value;
                            string propsText = match.Groups[2].Value;
                            string children = match.Groups[3].Success ? match.Groups[3].Value : string.Empty;
                            
                            LogToFile($"[{replacementCount}] Including component: {componentName}, props: {propsText.Trim()}");
                            LogToFile($"Children content length: {children.Length}");
                            
                            if (!_componentCache.TryGetValue(componentName, out string? componentTemplate) || componentTemplate == null)
                            {
                                LogToFile($"WARNING: Component not found: {componentName}");
                                return $"<!-- Component '{componentName}' not found -->";
                            }
                            
                            // Extract properties
                            var props = new Dictionary<string, string>();
                            foreach (Match propMatch in propPattern.Matches(propsText))
                            {
                                string propName = propMatch.Groups[1].Value;
                                string propValue = propMatch.Groups[2].Value;
                                props[propName] = propValue;
                                LogToFile($"  Prop: {propName} = {propValue}");
                            }
                            
                            // Replace properties in template
                            string processed = componentTemplate;

                            // First handle special slot placeholders for content
                            processed = ReplaceContentSlots(processed, children);
                            
                            // Then replace all regular properties
                            foreach (var prop in props)
                            {
                                processed = processed.Replace($"{{{prop.Key}}}", prop.Value);
                            }
                            
                            // Handle default values
                            processed = ReplaceDefaultValues(processed, props);
                            
                            // Handle conditional expressions
                            processed = ReplaceConditionalExpressions(processed, props);

                            // Handle computed expressions
                            processed = ReplaceComputedExpressions(processed, props);
                            
                            return processed;
                        }
                        catch (Exception ex)
                        {
                            LogToFile($"ERROR processing component: {ex.Message}");
                            LogToFile(ex.StackTrace);
                            return $"<!-- Error processing component: {ex.Message} -->";
                        }
                    });
                    
                    LogToFile($"Recursion level: {10 - recursionLimit}, replacements: {replacementCount}");
                    
                    // Safety check for potential infinite loop
                    if (replacementCount > 100)
                    {
                        LogToFile("WARNING: Too many component replacements (>100), possible infinite recursion. Breaking loop.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"ERROR in ProcessIncludes: {ex.Message}");
                LogToFile(ex.StackTrace);
            }
            
            // Cleanup any remaining placeholders
            result = CleanupUnresolvedPlaceholders(result);
            
            // Final cleanup of any remaining slot tags
            result = CleanupRemainingSlots(result);
            
            LogToFile("Component processing completed");
            return result;
        }

        /// <summary>
        /// Replace content slots with child content
        /// </summary>
        private string ReplaceContentSlots(string template, string children)
        {
            LogToFile($"Processing slots with children content length: {children?.Length ?? 0}");
            
            if (string.IsNullOrEmpty(children))
            {
                // Remove all slot placeholders if no children
                template = Regex.Replace(template, @"<slot\s*(?:name=""[^""]*"")?\s*/>", "");
                template = Regex.Replace(template, @"<slot\s*(?:name=""[^""]*"")?\s*></slot>", "");
                
                // Replace old-style content placeholders
                template = template.Replace("{content}", "")
                                  .Replace("{navItems}", "")
                                  .Replace("{children}", "");
                return template;
            }

            // Print the children content for debugging
            LogToFile($"Children content: {children.Substring(0, Math.Min(children.Length, 100))}...");
            
            // Handle named slots - pattern: <slot name="slotName" /> or <slot name="slotName"></slot>
            var namedSlotPattern = new Regex(@"<slot\s+name=""([^""]+)""\s*(?:/>|></slot>)");
            
            // Extract named slot content - pattern: <template slot="slotName">content</template>
            var slotContentPattern = new Regex(@"<template\s+slot=""([^""]+)"">(.*?)</template>", RegexOptions.Singleline);
            
            // Extract named slots from children
            var namedSlots = new Dictionary<string, string>();
            children = slotContentPattern.Replace(children, match => {
                string slotName = match.Groups[1].Value;
                string slotContent = match.Groups[2].Value;
                namedSlots[slotName] = slotContent;
                LogToFile($"Found named slot: {slotName} with content length: {slotContent.Length}");
                return ""; // Remove the template from children
            });
            
            // Replace named slots
            template = namedSlotPattern.Replace(template, match => {
                string slotName = match.Groups[1].Value;
                if (namedSlots.TryGetValue(slotName, out string? content) && !string.IsNullOrEmpty(content))
                {
                    LogToFile($"Replacing named slot: {slotName} with content");
                    return content;
                }
                LogToFile($"Named slot {slotName} not found in children");
                return "";
            });
            
            // Count default slots
            int defaultSlotCount = Regex.Matches(template, @"<slot\s*(?:/>|></slot>)").Count;
            LogToFile($"Found {defaultSlotCount} default slot(s) in template");
            
            // Handle default slot
            template = Regex.Replace(template, @"<slot\s*(?:/>|></slot>)", match => {
                LogToFile($"Replacing default slot with children content length: {children.Length}");
                return children;
            });
            
            // In case there are other formats of slots
            template = Regex.Replace(template, @"<slot[^>]*>.*?</slot>", match => {
                LogToFile($"Replacing alternative slot format with content");
                return children;
            });
            
            // For backward compatibility
            if (template.Contains("{content}") || template.Contains("{navItems}") || template.Contains("{children}"))
            {
                LogToFile("Using legacy content placeholders");
                template = template.Replace("{content}", children)
                                  .Replace("{navItems}", children)
                                  .Replace("{children}", children);
            }
            
            return template;
        }
        
        /// <summary>
        /// Replace default value expressions like {title || 'Default Title'}
        /// </summary>
        private string ReplaceDefaultValues(string template, Dictionary<string, string> props)
        {
            // Match default value pattern: {propName || 'default value'} or {propName || "default value"}
            var defaultValuePattern = new Regex(@"\{(\w+)\s*\|\|\s*['""]([^}'""]*)['""]\s*\}");
            
            return defaultValuePattern.Replace(template, match =>
            {
                string propName = match.Groups[1].Value;
                string defaultValue = match.Groups[2].Value;
                
                LogToFile($"Default value: {propName} || {defaultValue}");
                
                // Check if property exists and use it, otherwise use default
                if (props.TryGetValue(propName, out string? propValue) && !string.IsNullOrEmpty(propValue))
                {
                    LogToFile($"  Using property value: {propValue}");
                    return propValue;
                }
                
                // If property not found or empty, return default value
                LogToFile($"  Using default value: {defaultValue}");
                return defaultValue;
            });
        }
        
        /// <summary>
        /// Replace conditional expressions like {isActive ? 'value1' : 'value2'}
        /// </summary>
        private string ReplaceConditionalExpressions(string template, Dictionary<string, string> props)
        {
            // Match conditional expressions with better support for quoted values, including spaces
            var conditionalPattern = new Regex(@"\{(\w+)\s*\?\s*['""]?([^:}'""\s][^:}]*?)['""]?\s*:\s*['""]?([^}'""\s][^}]*?)['""]?\s*\}");
            
            return conditionalPattern.Replace(template, match =>
            {
                string propName = match.Groups[1].Value;
                string trueValue = match.Groups[2].Value.Trim();
                string falseValue = match.Groups[3].Value.Trim();
                
                LogToFile($"Conditional: {propName} ? {trueValue} : {falseValue}");
                
                // Check if property exists and evaluate to true/false
                if (props.TryGetValue(propName, out string? propValue))
                {
                    bool boolValue = propValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                    LogToFile($"  Value: {propValue} => {(boolValue ? trueValue : falseValue)}");
                    return boolValue ? trueValue : falseValue;
                }
                
                // If property not found, return false value
                LogToFile($"  Property not found, using false value: {falseValue}");
                return falseValue;
            });
        }

        /// <summary>
        /// Replace computed expressions like {width - 150} or {isLarge ? width * 2 : width}
        /// </summary>
        private string ReplaceComputedExpressions(string template, Dictionary<string, string> props)
        {
            // Match expressions with math operators: {width - 150}, {height * 2}, etc.
            var mathExprPattern = new Regex(@"\{(\w+)\s*([+\-*/])\s*(\d+)\}");
            var ternaryMathExprPattern = new Regex(@"\{(\w+)\s*\?\s*(\w+)\s*([+\-*/])\s*(\d+)\s*:\s*(\w+)(?:\s*([+\-*/])\s*(\d+))?\}");
            
            // First process ternary math expressions
            template = ternaryMathExprPattern.Replace(template, match =>
            {
                string conditionProp = match.Groups[1].Value;
                string trueProp = match.Groups[2].Value; 
                string trueOp = match.Groups[3].Value;
                int trueValue = int.Parse(match.Groups[4].Value);
                string falseProp = match.Groups[5].Value;
                
                string falseOp = match.Groups[6].Success ? match.Groups[6].Value : "+";
                int falseValue = match.Groups[7].Success ? int.Parse(match.Groups[7].Value) : 0;

                // Check condition property
                bool condition = false;
                if (props.TryGetValue(conditionProp, out string? condValue))
                {
                    condition = condValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                LogToFile($"Ternary math: {conditionProp} ? {trueProp} {trueOp} {trueValue} : {falseProp} {falseOp} {falseValue}");
                
                // Determine which property to use
                string propName = condition ? trueProp : falseProp;
                string op = condition ? trueOp : falseOp;
                int value = condition ? trueValue : falseValue;
                
                // Calculate result
                if (props.TryGetValue(propName, out string? propValue) && int.TryParse(propValue, out int propIntValue))
                {
                    int result = op switch
                    {
                        "+" => propIntValue + value,
                        "-" => propIntValue - value,
                        "*" => propIntValue * value,
                        "/" => propIntValue / value,
                        _ => propIntValue
                    };
                    LogToFile($"  Calculated value: {propIntValue} {op} {value} = {result}");
                    return result.ToString();
                }
                
                LogToFile($"  Failed to calculate value, using 0");
                return "0"; // Default value if calculation fails
            });
            
            // Then process simple math expressions
            template = mathExprPattern.Replace(template, match =>
            {
                string propName = match.Groups[1].Value;
                string op = match.Groups[2].Value;
                int value = int.Parse(match.Groups[3].Value);
                
                LogToFile($"Math expression: {propName} {op} {value}");
                
                if (props.TryGetValue(propName, out string? propValue) && int.TryParse(propValue, out int propIntValue))
                {
                    int result = op switch
                    {
                        "+" => propIntValue + value,
                        "-" => propIntValue - value,
                        "*" => propIntValue * value,
                        "/" => propIntValue / value,
                        _ => propIntValue
                    };
                    LogToFile($"  Calculated value: {propIntValue} {op} {value} = {result}");
                    return result.ToString();
                }
                
                LogToFile($"  Failed to calculate value, using 0");
                return "0"; // Default value if calculation fails
            });
            
            return template;
        }
        
        /// <summary>
        /// Clean up any unresolved placeholders
        /// </summary>
        private string CleanupUnresolvedPlaceholders(string template)
        {
            // Don't replace JSX comments
            var commentPattern = new Regex(@"\{\s*/\*.*?\*/\s*\}", RegexOptions.Singleline);
            var comments = new Dictionary<string, string>();
            int commentIndex = 0;
            
            // Extract comments temporarily
            template = commentPattern.Replace(template, match => 
            {
                string placeholder = $"__COMMENT_{commentIndex}__";
                comments[placeholder] = match.Value;
                commentIndex++;
                return placeholder;
            });
            
            // Replace any {propName} placeholders that weren't substituted
            var placeholderPattern = new Regex(@"\{([^{}]*)\}");
            template = placeholderPattern.Replace(template, match =>
            {
                string expression = match.Value;
                string innerContent = match.Groups[1].Value.Trim();
                
                // Skip known patterns that might have been missed
                if (innerContent.Contains("||") || innerContent.Contains("?") || 
                    innerContent.Contains("+") || innerContent.Contains("-") || 
                    innerContent.Contains("*") || innerContent.Contains("/"))
                {
                    LogToFile($"Unresolved expression: {expression}");
                    return ""; // Remove unresolved expressions
                }
                
                // Basic property placeholder
                if (Regex.IsMatch(innerContent, @"^\w+$"))
                {
                    LogToFile($"Unresolved property: {innerContent}");
                    return ""; // Remove unresolved properties
                }
                
                // Any other unrecognized pattern
                LogToFile($"Unknown placeholder format: {expression}");
                return "";
            });
            
            // Restore comments
            foreach (var comment in comments)
            {
                template = template.Replace(comment.Key, comment.Value);
            }
            
            return template;
        }

        /// <summary>
        /// Cleanup any remaining slot tags
        /// </summary>
        private string CleanupRemainingSlots(string template)
        {
            // Replace any remaining slot tags with empty views
            template = Regex.Replace(template, @"<slot\s*(?:name=""[^""]*"")?\s*/>", "<view></view>");
            template = Regex.Replace(template, @"<slot\s*(?:name=""[^""]*"")?\s*>.*?</slot>", "<view></view>");
            
            return template;
        }

        /// <summary>
        /// Parse a template with component includes into a scene graph
        /// </summary>
        public SceneNode ParseWithComponents(string template)
        {
            string processed = ProcessIncludes(template);
            return _parser.ParseString(processed);
        }
    }
} 