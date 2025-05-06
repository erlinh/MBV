using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Frontend.Desktop.MessageBus
{
    /// <summary>
    /// Client implementation of the message bus that connects to the backend
    /// </summary>
    public class MessageBusClient : IMessageBus
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Create a new MessageBusClient
        /// </summary>
        /// <param name="baseUrl">Base URL of the backend API (e.g. "http://localhost:5000")</param>
        public MessageBusClient(string baseUrl)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _httpClient = new HttpClient();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Try to read the transport settings from a temp file
        /// </summary>
        public static MessageBusClient CreateFromTransportSettings()
        {
            try
            {
                // Derive temp path for transport settings
                var tempPath = Path.Combine(Path.GetTempPath(), "mbv_transport.json");
                
                if (File.Exists(tempPath))
                {
                    var json = File.ReadAllText(tempPath);
                    var settings = JsonSerializer.Deserialize<TransportSettings>(json);
                    
                    if (settings?.Endpoint != null)
                    {
                        // Currently only supporting HTTP transport
                        // In future, this could handle pipes or other transports
                        if (settings.Transport.Equals("http", StringComparison.OrdinalIgnoreCase))
                        {
                            return new MessageBusClient(settings.Endpoint);
                        }
                    }
                }
                
                // Fallback to localhost with default port
                return new MessageBusClient("http://localhost:5000");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating MessageBusClient: {ex.Message}");
                return new MessageBusClient("http://localhost:5000");
            }
        }

        /// <summary>
        /// Send a command or query to the backend
        /// </summary>
        public async Task<TResult> Send<TCommand, TResult>(TCommand command)
        {
            try
            {
                // Get command type from the Type property if it exists, or use the class name
                string commandType = GetCommandType(command);
                var endpoint = $"{_baseUrl}/api/{commandType}";

                // Post the command to the backend
                var response = await _httpClient.PostAsJsonAsync(endpoint, command);
                response.EnsureSuccessStatusCode();
                
                // Read the response
                var result = await response.Content.ReadFromJsonAsync<TResult>(_jsonOptions);
                return result ?? throw new InvalidOperationException("Received null response from backend");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
                throw;
            }
        }

        private string GetCommandType(object? command)
        {
            if (command == null)
                return "unknown";

            var type = command.GetType();
            
            // Try to get Type property if it exists
            var typeProp = type.GetProperty("Type");
            if (typeProp != null)
            {
                var typeValue = typeProp.GetValue(command)?.ToString();
                if (!string.IsNullOrEmpty(typeValue))
                    return typeValue;
            }
            
            // Fallback to class name
            var name = type.Name;
            
            // Remove "Command" or "Query" suffix if present
            if (name.EndsWith("Command", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - 7);
            else if (name.EndsWith("Query", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - 5);
            
            // Convert to kebab-case
            return KebabCase(name);
        }

        private string KebabCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = "";
            for (int i = 0; i < input.Length; i++)
            {
                if (i > 0 && char.IsUpper(input[i]))
                    result += "-";
                result += char.ToLower(input[i]);
            }
            
            return result;
        }
    }

    /// <summary>
    /// Transport settings written by the backend
    /// </summary>
    public class TransportSettings
    {
        public string Transport { get; set; } = "http";
        public string Endpoint { get; set; } = "http://localhost:5000";
    }
} 