using SDL2;
using SkiaSharp;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Frontend.Desktop.UiDsl;
using Frontend.Desktop.MessageBus;
using Frontend.Desktop.State;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Frontend.Desktop
{
    class Program
    {
        // Window dimensions
        private const int Width = 800;
        private const int Height = 600;
        private const string WindowTitle = "MBV (Message → Backend → View)";

        // SDL window and renderer
        private static IntPtr _window;
        private static IntPtr _renderer;
        private static IntPtr _texture;
        
        // Skia objects
        private static SKSurface? _surface;
        private static SKCanvas? _canvas;
        private static byte[]? _pixelData;
        
        // MBV architecture components
        private static IMessageBus _messageBus = null!;
        private static AppStore _appStore = null!;
        private static SceneNode _rootNode = null!;
        private static SkRenderer _skRenderer = null!;
        private static DslParser _dslParser = null!;
        
        // Main loop control
        private static bool _running = true;
        private static bool _sceneNeedsUpdate = true;

        // Track last hovered node for efficient rendering
        private static UiDsl.SceneNode? _hoveredNode = null;

        // Dictionary to store dynamic action handlers
        private static Dictionary<string, Action> _actionHandlers = new Dictionary<string, Action>();
        
        // Logging
        private static StreamWriter _eventLogFile;
        private static StreamWriter _appLogFile;

        static void Main(string[] args)
        {
            Console.WriteLine("Starting MBV Application...");
            try
            {
                InitializeLogs();
                LogToFile(_appLogFile, "Application starting");
                
                Initialize();
                RunMainLoop();
                Cleanup();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Application error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                
                if (_appLogFile != null)
                {
                    LogToFile(_appLogFile, $"Application error: {ex.Message}");
                    LogToFile(_appLogFile, ex.StackTrace);
                }
            }
        }
        
        private static void InitializeLogs()
        {
            try
            {
                // Create logs directory
                string logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(logsDir);
                
                // Create event log
                string eventLogPath = Path.Combine(logsDir, "events.log");
                _eventLogFile = new StreamWriter(eventLogPath, true);
                _eventLogFile.AutoFlush = true;
                
                // Create application log
                string appLogPath = Path.Combine(logsDir, "application.log");
                _appLogFile = new StreamWriter(appLogPath, true);
                _appLogFile.AutoFlush = true;
                
                LogToFile(_appLogFile, "Log files initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing logs: {ex.Message}");
            }
        }
        
        private static void LogToFile(StreamWriter logFile, string message)
        {
            if (logFile == null) return;
            
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            logFile.WriteLine($"[{timestamp}] {message}");
        }

        private static void Initialize()
        {
            LogToFile(_appLogFile, "Initializing application components");
            
            // Initialize MBV components
            InitializeMbvComponents();
            
            // Initialize SDL
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
            {
                string error = $"SDL could not initialize! SDL_Error: {SDL.SDL_GetError()}";
                LogToFile(_appLogFile, error);
                throw new Exception(error);
            }

            // Create window
            _window = SDL.SDL_CreateWindow(
                WindowTitle,
                SDL.SDL_WINDOWPOS_CENTERED,
                SDL.SDL_WINDOWPOS_CENTERED,
                Width, Height,
                SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);

            if (_window == IntPtr.Zero)
            {
                string error = $"Window could not be created! SDL_Error: {SDL.SDL_GetError()}";
                LogToFile(_appLogFile, error);
                throw new Exception(error);
            }

            // Create renderer
            _renderer = SDL.SDL_CreateRenderer(
                _window, -1,
                SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED |
                SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);

            if (_renderer == IntPtr.Zero)
            {
                string error = $"Renderer could not be created! SDL_Error: {SDL.SDL_GetError()}";
                LogToFile(_appLogFile, error);
                throw new Exception(error);
            }

            // Create texture that will be used to display the Skia surface
            uint pixelFormat = SDL.SDL_PIXELFORMAT_RGBA8888;
            _texture = SDL.SDL_CreateTexture(
                _renderer,
                pixelFormat,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                Width, Height);

            // Create Skia surface for CPU rendering
            _pixelData = new byte[Width * Height * 4]; // 4 bytes per pixel (RGBA)
            var info = new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            _surface = SKSurface.Create(info);
            _canvas = _surface.Canvas;
            
            // Setup Skia renderer
            _skRenderer = new SkRenderer(_canvas);
            
            // Load the UI from DSL
            LoadUiFromDsl();
            
            // Subscribe to store changes
            _appStore.StateChanged += () => _sceneNeedsUpdate = true;
            
            LogToFile(_appLogFile, "Application initialization complete");
        }

        private static void InitializeMbvComponents()
        {
            // Create message bus
            _messageBus = MessageBusClient.CreateFromTransportSettings();
            
            // Create app store
            _appStore = new AppStore(_messageBus);
            
            // Create DSL parser
            _dslParser = new DslParser();
        }

        private static void LoadUiFromDsl()
        {
            try
            {
                // Try to load from file
                string dslPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UiDsl", "main.skx");
                string componentPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UiDsl", "Components");
                
                Console.WriteLine($"Looking for UI file at: {dslPath}");
                Console.WriteLine($"Looking for components at: {componentPath}");
                
                // Ensure component directory exists
                if (!Directory.Exists(componentPath))
                {
                    Console.WriteLine($"Component directory not found at: {componentPath}");
                    // Try alternative path - source code location
                    string srcComponentPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "UiDsl", "Components");
                    if (Directory.Exists(srcComponentPath))
                    {
                        Console.WriteLine($"Found components in source directory: {srcComponentPath}");
                        componentPath = srcComponentPath;
                    }
                    else
                    {
                        Console.WriteLine($"Could not find components directory in source: {srcComponentPath}");
                        // Create the directory if it doesn't exist
                        try
                        {
                            Directory.CreateDirectory(componentPath);
                            Console.WriteLine($"Created component directory at: {componentPath}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to create component directory: {ex.Message}");
                        }
                    }
                }
                
                // Initialize the component manager with the components directory
                Console.WriteLine($"Initializing component manager with path: {componentPath}");
                _dslParser.InitComponentManager(componentPath);
                
                if (File.Exists(dslPath))
                {
                    Console.WriteLine($"Loading UI from {dslPath}...");
                    try
                    {
                        _rootNode = _dslParser.ParseFile(dslPath);
                        Console.WriteLine("UI loaded successfully");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing UI file: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        _rootNode = CreateDefaultUi();
                        Console.WriteLine("Falling back to default UI");
                    }
                }
                else
                {
                    // Try to find main.skx in the source code directory
                    string srcDslPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "UiDsl", "main.skx");
                    Console.WriteLine($"UI file not found at {dslPath}, trying source path: {srcDslPath}");
                    
                    if (File.Exists(srcDslPath))
                    {
                        Console.WriteLine($"Found UI file in source: {srcDslPath}");
                        try
                        {
                            _rootNode = _dslParser.ParseFile(srcDslPath);
                            Console.WriteLine("UI loaded successfully from source");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error parsing source UI file: {ex.Message}");
                            _rootNode = CreateDefaultUi();
                            Console.WriteLine("Falling back to default UI");
                        }
                    }
                    else
                    {
                        // Create a default UI if file doesn't exist
                        Console.WriteLine($"UI file not found in source either, using default UI");
                        _rootNode = CreateDefaultUi();
                        Console.WriteLine("Using default UI");
                    }
                }
                
                _skRenderer.SetSceneGraph(_rootNode);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading UI: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                _rootNode = CreateDefaultUi();
                _skRenderer.SetSceneGraph(_rootNode);
            }
        }

        private static SceneNode CreateDefaultUi()
        {
            Console.WriteLine("Creating default UI");
            
            // Create a root container matching our JSX structure
            var root = new SceneNode
            {
                Type = NodeType.Container,
                Width = Width,
                Height = Height,
                BackgroundColor = new SKColor(245, 245, 245) // #F5F5F5 - matching JSX
            };
            
            // Header (top bar)
            var header = new SceneNode
            {
                Type = NodeType.Container,
                X = 0,
                Y = 0,
                Width = Width,
                Height = 60,
                BackgroundColor = new SKColor(59, 130, 246) // #3B82F6 - blue
            };
            
            // App title
            var title = new SceneNode
            {
                Type = NodeType.Text,
                X = 20,
                Y = 15,
                Text = "MBV Application (Default UI)",
                FontSize = 24,
                TextColor = SKColors.White
            };
            header.AddChild(title);
            
            // User info
            var userInfo = new SceneNode
            {
                Type = NodeType.Text,
                X = 650,
                Y = 20,
                Text = "User: Guest",
                FontSize = 16,
                TextColor = SKColors.White
            };
            header.AddChild(userInfo);
            
            // Add header to root
            root.AddChild(header);
            
            // Sidebar
            var sidebar = new SceneNode
            {
                Id = "sidebar",
                Type = NodeType.Container,
                X = 0,
                Y = 60,
                Width = 200,
                Height = 540,
                BackgroundColor = new SKColor(240, 249, 255), // #F0F9FF - light blue
                BorderColor = new SKColor(148, 163, 184), // #94A3B8 - gray
                BorderWidth = 1
            };
            
            // Navigation header
            var navHeader = new SceneNode
            {
                Type = NodeType.Text,
                X = 20,
                Y = 20,
                Text = "Navigation",
                FontSize = 18,
                TextColor = new SKColor(71, 85, 105) // #475569 - slate
            };
            sidebar.AddChild(navHeader);
            
            // Home button
            var homeButton = new SceneNode
            {
                Type = NodeType.Rectangle,
                X = 10,
                Y = 50,
                Width = 180,
                Height = 40,
                FillColor = new SKColor(219, 234, 254), // #DBEAFE - light blue
                BorderColor = new SKColor(59, 130, 246), // #3B82F6 - blue
                BorderWidth = 2,
                OnClick = "NavigateTo:home"
            };
            var homeText = new SceneNode
            {
                Type = NodeType.Text,
                X = 20,
                Y = 12,
                Text = "Home",
                FontSize = 16,
                TextColor = new SKColor(30, 64, 175) // #1E40AF - dark blue
            };
            homeButton.AddChild(homeText);
            sidebar.AddChild(homeButton);
            
            // Add sidebar to root
            root.AddChild(sidebar);
            
            // Main content area
            var content = new SceneNode
            {
                Type = NodeType.Container,
                X = 200, 
                Y = 60,
                Width = 600,
                Height = 540
            };
            
            // Inner content container
            var innerContent = new SceneNode
            {
                Type = NodeType.Container,
                X = 20,
                Y = 20,
                Width = 560,
                Height = 500
            };
            
            // Welcome text
            var welcomeTitle = new SceneNode
            {
                Type = NodeType.Text,
                X = 0,
                Y = 0,
                Text = "Welcome to MBV",
                FontSize = 32,
                TextColor = new SKColor(51, 65, 85) // #334155 - slate
            };
            innerContent.AddChild(welcomeTitle);
            
            var welcomeSubtitle = new SceneNode
            {
                Type = NodeType.Text,
                X = 0,
                Y = 50,
                Text = "This is a sample application built with Message → Backend → View architecture.",
                FontSize = 16,
                TextColor = new SKColor(100, 116, 139) // #64748B - slate
            };
            innerContent.AddChild(welcomeSubtitle);
            
            // Get started button
            var getStartedButton = new SceneNode
            {
                Type = NodeType.Rectangle,
                X = 0,
                Y = 100,
                Width = 200,
                Height = 50,
                FillColor = new SKColor(59, 130, 246), // #3B82F6 - blue
                BorderColor = new SKColor(30, 64, 175), // #1E40AF - dark blue
                BorderWidth = 2,
                OnClick = "ShowMessage:Welcome to MBV! This is the default UI created when no JSX file was found."
            };
            var buttonText = new SceneNode
            {
                Type = NodeType.Text,
                X = 40,
                Y = 15,
                Text = "Get Started",
                FontSize = 18,
                TextColor = SKColors.White
            };
            getStartedButton.AddChild(buttonText);
            innerContent.AddChild(getStartedButton);
            
            // Add inner content to main content area
            content.AddChild(innerContent);
            
            // Add content area to root
            root.AddChild(content);
            
            Console.WriteLine("Default UI created successfully");
            return root;
        }

        static void RunMainLoop()
        {
            SDL.SDL_Event sdlEvent;
            
            while (_running)
            {
                // Process SDL events
                while (SDL.SDL_PollEvent(out sdlEvent) != 0)
                {
                    if (sdlEvent.type == SDL.SDL_EventType.SDL_QUIT)
                    {
                        _running = false;
                    }
                    else if (sdlEvent.type == SDL.SDL_EventType.SDL_KEYDOWN)
                    {
                        if (sdlEvent.key.keysym.sym == SDL.SDL_Keycode.SDLK_ESCAPE)
                        {
                            _running = false;
                        }
                    }
                    else if (sdlEvent.type == SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN)
                    {
                        HandleMouseDown(sdlEvent.button.x, sdlEvent.button.y);
                    }
                    else if (sdlEvent.type == SDL.SDL_EventType.SDL_MOUSEMOTION)
                    {
                        // Track mouse position for hover effects
                        HandleMouseMove(sdlEvent.motion.x, sdlEvent.motion.y);
                    }
                }

                // Render frame if needed
                if (_sceneNeedsUpdate)
                {
                    RenderFrame();
                    _sceneNeedsUpdate = false;
                }

                // Small delay to avoid hogging the CPU
                SDL.SDL_Delay(16); // ~60 FPS
            }
        }

        static void RenderFrame()
        {
            if (_surface == null || _canvas == null || _pixelData == null) return;
            
            // Render the scene graph
            _skRenderer.Render();

            // Get pixel data from surface
            using (var image = _surface.Snapshot())
            using (var pixmap = image.PeekPixels())
            {
                if (pixmap != null)
                {
                    // Copy to our buffer
                    Marshal.Copy(pixmap.GetPixels(), _pixelData, 0, _pixelData.Length);
                }
            }

            // Update the texture with the Skia pixel data
            IntPtr pixelsPtr = IntPtr.Zero;
            int pitch = 0;
            SDL.SDL_LockTexture(_texture, IntPtr.Zero, out pixelsPtr, out pitch);
            
            // Copy pixel data to texture
            Marshal.Copy(_pixelData, 0, pixelsPtr, _pixelData.Length);
            SDL.SDL_UnlockTexture(_texture);

            // Render the texture to the screen
            SDL.SDL_RenderClear(_renderer);
            SDL.SDL_RenderCopy(_renderer, _texture, IntPtr.Zero, IntPtr.Zero);
            SDL.SDL_RenderPresent(_renderer);
        }

        static void HandleEvent(SceneNode? node, string eventType)
        {
            if (node == null) return;
            
            string handler = string.Empty;
            
            // Get the appropriate event handler based on event type
            switch (eventType)
            {
                case "click":
                    handler = node.OnClick;
                    break;
                case "hover":
                    handler = node.OnHover;
                    break;
                case "focus":
                    handler = node.OnFocus;
                    break;
                case "change":
                    handler = node.OnChange;
                    break;
                case "blur":
                    handler = node.OnBlur;
                    break;
            }
            
            // If no handler is defined, bubble up to parent
            if (string.IsNullOrEmpty(handler) && node.Parent != null)
            {
                HandleEvent(node.Parent, eventType);
                return;
            }
            
            if (string.IsNullOrEmpty(handler)) return;
            
            LogToFile($"Handling {eventType} event: {handler} for node {node.Id}");
            
            // Parse the action and parameters
            string action = handler;
            string[] parameters = Array.Empty<string>();
            
            if (handler.Contains(':'))
            {
                var parts = handler.Split(':', 2);
                action = parts[0];
                parameters = parts[1].Split(':');
            }
            
            switch (action.ToLower())
            {
                case "navigateto":
                    string screen = parameters.Length > 0 ? parameters[0] : "home";
                    LogToFile($"Navigation to: {screen}");
                    break;
                    
                case "selectradio":
                    if (parameters.Length >= 2)
                    {
                        string groupName = parameters[0];
                        string selectedId = parameters[1];
                        LogToFile($"Selecting radio button: {selectedId} in group {groupName}");
                        
                        // Find all radio buttons in the same group and update their states
                        if (_rootNode != null)
                        {
                            // First deselect all in the group
                            foreach (var radioNode in _rootNode.FindAllNodesOfType(NodeType.Radio))
                            {
                                if (radioNode.GroupName == groupName)
                                {
                                    radioNode.IsSelected = (radioNode.Id == selectedId);
                                }
                            }
                        }
                    }
                    break;
                    
                case "togglecheckbox":
                    LogToFile($"Toggling checkbox: {node.Id}");
                    node.IsChecked = !node.IsChecked;
                    break;
                    
                case "editfield":
                    string fieldId = parameters.Length > 0 ? parameters[0] : node.Id;
                    LogToFile($"Edit field: {fieldId}");
                    // Here you would open an editor for the field
                    break;
                    
                default:
                    LogToFile($"Unknown action: {action}");
                    break;
            }
        }

        static void HandleMouseDown(int x, int y)
        {
            var node = _rootNode?.HitTest(x, y);
            LogToFile($"Mouse down at ({x}, {y}), hit: {node?.Id ?? "none"}");
            
            if (node != null)
            {
                HandleEvent(node, "click");
                _hoveredNode = node;
                _sceneNeedsUpdate = true;
            }
        }

        static void HandleMouseMove(int x, int y)
        {
            if (_rootNode == null) return;
            
            // Find the node at the cursor position
            var hoveredNode = _rootNode.HitTest(x, y);
            
            // Only update UI if the hovered node changed
            if (hoveredNode != _hoveredNode)
            {
                // Reset hover state on previously hovered node
                if (_hoveredNode != null)
                {
                    // We'd set some hover state in a full implementation
                    Console.WriteLine($"Hover exit: {_hoveredNode.Type}");
                }
                
                // Set hover state on new node
                _hoveredNode = hoveredNode;
                
                // Update the renderer's hovered node
                _skRenderer.SetHoveredNode(_hoveredNode);
                
                if (_hoveredNode != null)
                {
                    // Trigger hover action if any
                    if (!string.IsNullOrEmpty(_hoveredNode.OnHover))
                    {
                        HandleEvent(_hoveredNode, "hover");
                    }
                    
                    Console.WriteLine($"Hover enter: {_hoveredNode.Type}");
                    
                    // Change cursor to hand for clickable elements
                    if (!string.IsNullOrEmpty(_hoveredNode.OnClick))
                    {
                        SDL.SDL_SetCursor(SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND));
                    }
                    else
                    {
                        SDL.SDL_SetCursor(SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW));
                    }
                }
                else
                {
                    // Reset cursor
                    SDL.SDL_SetCursor(SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW));
                }
                
                // Update the UI
                _sceneNeedsUpdate = true;
            }
        }

        static void Cleanup()
        {
            _surface?.Dispose();
            SDL.SDL_DestroyTexture(_texture);
            SDL.SDL_DestroyRenderer(_renderer);
            SDL.SDL_DestroyWindow(_window);
            SDL.SDL_Quit();
        }
    }
}
