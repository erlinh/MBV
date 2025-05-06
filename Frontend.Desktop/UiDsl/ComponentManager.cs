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

        public ComponentManager(string componentPath, DslParser parser)
        {
            _componentPath = componentPath;
            _parser = parser;
            LoadComponents();
        }

        /// <summary>
        /// Loads all component templates from the component directory
        /// </summary>
        private void LoadComponents()
        {
            if (!Directory.Exists(_componentPath))
            {
                Console.WriteLine($"Component directory not found: {_componentPath}");
                return;
            }

            foreach (var file in Directory.GetFiles(_componentPath, "*.skx"))
            {
                string componentName = Path.GetFileNameWithoutExtension(file);
                _componentCache[componentName] = File.ReadAllText(file);
                Console.WriteLine($"Registered component: {componentName}");
            }
        }

        /// <summary>
        /// Process component includes in a template
        /// </summary>
        public string ProcessIncludes(string template)
        {
            Console.WriteLine("Processing component includes...");

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
            
            while (hasIncludes && recursionLimit > 0)
            {
                hasIncludes = false;
                recursionLimit--;
                
                result = includePattern.Replace(result, match =>
                {
                    hasIncludes = true;
                    replacementCount++;
                    
                    string componentName = match.Groups[1].Value;
                    string propsText = match.Groups[2].Value;
                    string children = match.Groups[3].Success ? match.Groups[3].Value : string.Empty;
                    
                    Console.WriteLine($"Including component: {componentName}, props: {propsText.Trim()}");
                    
                    if (!_componentCache.TryGetValue(componentName, out string? componentTemplate) || componentTemplate == null)
                    {
                        Console.WriteLine($"WARNING: Component not found: {componentName}");
                        return $"<!-- Component '{componentName}' not found -->";
                    }
                    
                    // Extract properties
                    var props = new Dictionary<string, string>();
                    foreach (Match propMatch in propPattern.Matches(propsText))
                    {
                        string propName = propMatch.Groups[1].Value;
                        string propValue = propMatch.Groups[2].Value;
                        props[propName] = propValue;
                        Console.WriteLine($"  Prop: {propName} = {propValue}");
                    }
                    
                    // Replace properties in template
                    string processed = componentTemplate;
                    foreach (var prop in props)
                    {
                        processed = processed.Replace($"{{{prop.Key}}}", prop.Value);
                    }
                    
                    // Handle conditional properties like isActive
                    processed = ReplaceConditionalExpressions(processed, props);
                    
                    // Replace children placeholder
                    if (!string.IsNullOrEmpty(children))
                    {
                        processed = processed.Replace("{navItems}", children);
                        processed = processed.Replace("{content}", children);
                    }
                    else
                    {
                        processed = processed.Replace("{navItems}", "");
                        processed = processed.Replace("{content}", "");
                    }
                    
                    return processed;
                });
                
                Console.WriteLine($"Recursion level: {10 - recursionLimit}, replacements: {replacementCount}");
                
                // Safety check for potential infinite loop
                if (replacementCount > 100)
                {
                    Console.WriteLine("WARNING: Too many component replacements (>100), possible infinite recursion. Breaking loop.");
                    break;
                }
            }
            
            // Cleanup any remaining placeholders
            result = CleanupUnresolvedPlaceholders(result);
            
            Console.WriteLine("Component processing completed");
            return result;
        }
        
        /// <summary>
        /// Replace conditional expressions like {isActive ? 'value1' : 'value2'}
        /// </summary>
        private string ReplaceConditionalExpressions(string template, Dictionary<string, string> props)
        {
            // Match conditional expressions: {propName ? '#value1' : '#value2'} or {propName ? 'value1' : 'value2'}
            var conditionalPattern = new Regex(@"\{(\w+)\s*\?\s*['""]?([^:'""]+)['""]?\s*:\s*['""]?([^}'""\s]+)['""]?\s*\}");
            
            return conditionalPattern.Replace(template, match =>
            {
                string propName = match.Groups[1].Value;
                string trueValue = match.Groups[2].Value;
                string falseValue = match.Groups[3].Value;
                
                Console.WriteLine($"Conditional: {propName} ? {trueValue} : {falseValue}");
                
                // Check if property exists and evaluate to true/false
                if (props.TryGetValue(propName, out string? propValue))
                {
                    bool boolValue = propValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                    Console.WriteLine($"  Value: {propValue} => {(boolValue ? trueValue : falseValue)}");
                    return boolValue ? trueValue : falseValue;
                }
                
                // If property not found, return false value
                Console.WriteLine($"  Property not found, using false value: {falseValue}");
                return falseValue;
            });
        }
        
        /// <summary>
        /// Clean up any unresolved placeholders
        /// </summary>
        private string CleanupUnresolvedPlaceholders(string template)
        {
            // Replace any {propName} placeholders that weren't substituted
            var placeholderPattern = new Regex(@"\{(\w+)[^}]*\}");
            return placeholderPattern.Replace(template, match =>
            {
                // Don't replace JSX comments like {/* Comment */}
                if (match.Value.StartsWith("{/*") && match.Value.EndsWith("*/}"))
                    return match.Value;
                    
                // Preserve math expressions like {width - 150}
                if (match.Value.Contains("+") || match.Value.Contains("-") || 
                    match.Value.Contains("*") || match.Value.Contains("/"))
                    return "0";
                    
                Console.WriteLine($"Unresolved placeholder: {match.Value}");
                return "";
            });
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