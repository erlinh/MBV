﻿using SDL2;
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
                        HandleMouseClick(sdlEvent.button.x, sdlEvent.button.y);
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

        static void HandleMouseClick(int x, int y)
        {
            if (_rootNode == null) return;
            
            LogToFile(_eventLogFile, $"Mouse click at ({x}, {y})");
            
            // Find the node at the click position
            var clickedNode = HitTest(_rootNode, x, y);
            if (clickedNode != null)
            {
                // Process the OnClick action
                string onClickAction = clickedNode.OnClick;
                LogToFile(_eventLogFile, $"Node hit: {clickedNode.Type} {clickedNode.Id ?? "unknown"} with OnClick: {onClickAction ?? "none"}");
                
                if (!string.IsNullOrEmpty(onClickAction))
                {
                    ProcessAction(onClickAction);
                }
                
                Console.WriteLine($"Clicked: {clickedNode.Type} at ({x}, {y}) with action: {onClickAction}");
            }
            else
            {
                LogToFile(_eventLogFile, $"No node found at: {x}, {y}");
                Console.WriteLine($"No node found at: {x}, {y}");
            }
        }
        
        static UiDsl.SceneNode? HitTest(UiDsl.SceneNode node, int x, int y, SKPoint parentPosition = default)
        {
            // Calculate absolute position
            var position = new SKPoint(
                parentPosition.X + node.X,
                parentPosition.Y + node.Y
            );
            
            // Check if point is within this node's bounds
            bool isInside = x >= position.X && 
                            x <= position.X + node.Width && 
                            y >= position.Y && 
                            y <= position.Y + node.Height;
            
            if (!isInside) return null;
            
            // Check children first (top-most node gets priority)
            if (node.Children != null)
            {
                // Iterate in reverse order to check topmost nodes first
                for (int i = node.Children.Count - 1; i >= 0; i--)
                {
                    var childHit = HitTest(node.Children[i], x, y, position);
                    if (childHit != null)
                        return childHit;
                }
            }
            
            // If no children were hit, return this node if it has an onclick action
            if (!string.IsNullOrEmpty(node.OnClick) || node.Type == UiDsl.NodeType.Button)
                return node;
                
            return null;
        }
        
        static void ProcessAction(string action)
        {
            LogToFile(_eventLogFile, $"Processing action: {action}");
            
            // First check if we have a custom handler registered
            if (_actionHandlers.TryGetValue(action, out var handler))
            {
                LogToFile(_eventLogFile, $"Found custom handler for action: {action}");
                handler();
                return;
            }
            
            // Parse action format: ActionName:Param
            string[] parts = action.Split(':', 2);
            string actionName = parts[0];
            string param = parts.Length > 1 ? parts[1] : string.Empty;
            
            LogToFile(_eventLogFile, $"Action name: {actionName}, parameter: {param}");
            
            Console.WriteLine($"Processing action: {actionName} with param: {param}");
            
            switch (actionName)
            {
                case "NavigateTo":
                    _appStore.NavigateTo(param);
                    // Update the UI to reflect navigation
                    UpdateUiForNavigation(param);
                    Console.WriteLine($"Navigating to: {param}");
                    break;
                    
                case "ShowMessage":
                    ShowMessageBox(param, "MBV Information");
                    break;
                    
                case "ToggleSidebar":
                    _appStore.ToggleSidebar();
                    // Toggle sidebar visibility in the UI
                    if (_rootNode != null)
                    {
                        var sidebar = FindNodeById(_rootNode, "sidebar");
                        if (sidebar != null)
                        {
                            sidebar.Visible = _appStore.IsSidebarOpen;
                        }
                    }
                    break;
                    
                case "EditField":
                    // Open a dialog to edit the field content
                    if (string.IsNullOrEmpty(param))
                        break;
                        
                    var fieldNode = FindNodeById(_rootNode, param);
                    if (fieldNode != null)
                    {
                        string currentValue = fieldNode.Value;
                        string newValue = ShowInputDialog($"Edit {param}", "Enter new value", currentValue);
                        
                        if (!string.IsNullOrEmpty(newValue) && newValue != currentValue)
                        {
                            fieldNode.Value = newValue;
                            Console.WriteLine($"Updated field {param} with value: {newValue}");
                            
                            // Trigger change handler if available
                            if (!string.IsNullOrEmpty(fieldNode.OnChange))
                            {
                                ProcessAction(fieldNode.OnChange + ":" + newValue);
                            }
                            
                            _sceneNeedsUpdate = true;
                        }
                    }
                    break;
                    
                case "ToggleCheckbox":
                    // Toggle the checked state of a checkbox
                    if (string.IsNullOrEmpty(param))
                        break;
                        
                    var checkboxNode = FindNodeById(_rootNode, param);
                    if (checkboxNode != null)
                    {
                        checkboxNode.IsChecked = !checkboxNode.IsChecked;
                        Console.WriteLine($"Toggled checkbox {param}: {checkboxNode.IsChecked}");
                        
                        // Trigger change handler if available
                        if (!string.IsNullOrEmpty(checkboxNode.OnChange))
                        {
                            ProcessAction(checkboxNode.OnChange + ":" + checkboxNode.IsChecked);
                        }
                        
                        _sceneNeedsUpdate = true;
                    }
                    break;
                    
                case "SelectRadio":
                    // Select a radio button in a group
                    string[] radioParams = param.Split(':', 2);
                    if (radioParams.Length < 2)
                        break;
                        
                    string groupName = radioParams[0];
                    string radioId = radioParams[1];
                    
                    // First unselect all other radio buttons in the same group
                    if (_rootNode != null)
                    {
                        // Find all radio buttons in this group and unselect them
                        var radioNodes = FindNodesByGroupName(_rootNode, groupName);
                        foreach (var radio in radioNodes)
                        {
                            radio.IsSelected = (radio.Id == radioId);
                            
                            // Trigger change handler if this is the selected one
                            if (radio.Id == radioId && !string.IsNullOrEmpty(radio.OnChange))
                            {
                                ProcessAction(radio.OnChange + ":" + radio.Id);
                            }
                        }
                        
                        _sceneNeedsUpdate = true;
                    }
                    break;
                
                case "CloseMessage":
                    // Find and remove message overlay
                    if (_rootNode != null)
                    {
                        var overlay = FindNodeById(_rootNode, "message_overlay");
                        if (overlay != null && _rootNode.Children.Contains(overlay))
                        {
                            _rootNode.Children.Remove(overlay);
                        }
                    }
                    break;
                    
                default:
                    Console.WriteLine($"Unknown action: {actionName}");
                    break;
            }
            
            _sceneNeedsUpdate = true;
        }
        
        static void ShowMessageBox(string message, string title)
        {
            if (_rootNode == null) return;
            
            // Create overlay to block background
            var overlay = new UiDsl.SceneNode
            {
                Id = "message_overlay",
                Type = UiDsl.NodeType.Container,
                X = 0,
                Y = 0,
                Width = Width,
                Height = Height,
                BackgroundColor = new SKColor(0, 0, 0, 100) // Semi-transparent black
            };
            
            // Create message box
            var messageBox = new UiDsl.SceneNode
            {
                Type = UiDsl.NodeType.Container,
                X = Width / 2 - 200,
                Y = Height / 2 - 100,
                Width = 400,
                Height = 200,
                BackgroundColor = SKColors.White,
                BorderColor = new SKColor(203, 213, 225),
                BorderWidth = 1,
                BorderRadius = 8
            };
            
            // Add title
            var titleNode = new UiDsl.SceneNode
            {
                Type = UiDsl.NodeType.Text,
                X = 20,
                Y = 20,
                Text = title,
                FontSize = 18,
                TextColor = new SKColor(51, 65, 85)
            };
            messageBox.AddChild(titleNode);
            
            // Add message
            var messageNode = new UiDsl.SceneNode
            {
                Type = UiDsl.NodeType.Text,
                X = 20,
                Y = 60,
                Width = 360,
                Text = message,
                FontSize = 14,
                TextColor = new SKColor(71, 85, 105)
            };
            messageBox.AddChild(messageNode);
            
            // Add OK button
            var okButton = new UiDsl.SceneNode
            {
                Type = UiDsl.NodeType.Rectangle,
                X = 150,
                Y = 140,
                Width = 100,
                Height = 40,
                FillColor = new SKColor(59, 130, 246),
                BorderRadius = 4,
                OnClick = "CloseMessage"
            };
            
            var okText = new UiDsl.SceneNode
            {
                Type = UiDsl.NodeType.Text,
                X = 40,
                Y = 12,
                Text = "OK",
                FontSize = 14,
                TextColor = SKColors.White
            };
            okButton.AddChild(okText);
            messageBox.AddChild(okButton);
            
            // Add message box to overlay
            overlay.AddChild(messageBox);
            
            // Add overlay to root
            _rootNode.AddChild(overlay);
            
            _sceneNeedsUpdate = true;
        }
        
        static string ShowInputDialog(string title, string prompt, string defaultValue = "")
        {
            if (_rootNode == null) return defaultValue;
            
            // Create an input dialog similar to a message box but with a text field
            // In a real app, this would be an actual input dialog, but for this example
            // we'll simulate it with a console prompt
            
            Console.WriteLine();
            Console.WriteLine($"=== {title} ===");
            Console.WriteLine(prompt);
            Console.Write($"[{defaultValue}]: ");
            string? input = Console.ReadLine();
            
            // Use default if nothing entered
            if (string.IsNullOrEmpty(input))
                return defaultValue;
                
            return input;
        }
        
        static List<UiDsl.SceneNode> FindNodesByGroupName(UiDsl.SceneNode rootNode, string groupName)
        {
            List<UiDsl.SceneNode> results = new List<UiDsl.SceneNode>();
            
            void Search(UiDsl.SceneNode node)
            {
                // Check if this node matches
                if (node.GroupName == groupName)
                {
                    results.Add(node);
                }
                
                // Search children
                if (node.Children != null)
                {
                    foreach (var child in node.Children)
                    {
                        Search(child);
                    }
                }
            }
            
            Search(rootNode);
            return results;
        }
        
        static void ShowNotification(string message)
        {
            // Create a notification that automatically disappears
            if (_rootNode != null)
            {
                var notif = new UiDsl.SceneNode
                {
                    Id = "notification",
                    Type = UiDsl.NodeType.Container,
                    X = Width - 320,
                    Y = 70,
                    Width = 300,
                    Height = 60,
                    BackgroundColor = new SKColor(34, 197, 94), // Green
                    BorderColor = new SKColor(21, 128, 61),
                    BorderWidth = 1
                };
                
                var notifText = new UiDsl.SceneNode
                {
                    Type = UiDsl.NodeType.Text,
                    X = 15,
                    Y = 20,
                    Text = message,
                    FontSize = 16,
                    TextColor = SKColors.White
                };
                notif.AddChild(notifText);
                
                _rootNode.AddChild(notif);
                
                // Set up a timer to remove the notification
                Task.Run(async () =>
                {
                    await Task.Delay(3000); // 3 seconds
                    var existingNotif = FindNodeById(_rootNode, "notification");
                    if (existingNotif != null && _rootNode.Children.Contains(existingNotif))
                    {
                        _rootNode.Children.Remove(existingNotif);
                        _sceneNeedsUpdate = true;
                    }
                });
            }
        }
        
        static void UpdateUiForNavigation(string view)
        {
            if (_rootNode == null) return;
            
            // Find the main content container
            var contentContainer = _rootNode.Children.Find(n => 
                n.Type == UiDsl.NodeType.Container && 
                n.X >= 200 && n.Y >= 60 && 
                n.Width >= 500);
                
            if (contentContainer == null) return;
            
            // Replace the entire content container instead of just inner content
            // to ensure all previous elements are removed
            contentContainer.Children.Clear();
            
            // Create fresh inner container
            var innerContainer = new UiDsl.SceneNode
            {
                Type = UiDsl.NodeType.Container,
                X = 20,
                Y = 20,
                Width = 560,
                Height = 500
            };
            contentContainer.AddChild(innerContainer);
            
            // Add a title based on the view
            var titleText = view switch
            {
                "home" => "Welcome to MBV",
                "notes" => "Notes Page",
                "settings" => "Settings Page",
                _ => $"View: {view}"
            };
            
            var titleNode = new UiDsl.SceneNode
            {
                Type = UiDsl.NodeType.Text,
                X = 0,
                Y = 20,
                Text = titleText,
                FontSize = 32,
                TextColor = new SKColor(51, 65, 85)
            };
            innerContainer.AddChild(titleNode);
            
            // Add some content
            var contentText = view switch
            {
                "home" => "Welcome to the home page of the MBV application!",
                "notes" => "This is where your notes would be displayed.",
                "settings" => "Here you can configure application settings.",
                _ => $"Content for '{view}' view"
            };
            
            var contentNode = new UiDsl.SceneNode
            {
                Type = UiDsl.NodeType.Text,
                X = 0,
                Y = 80,
                Text = contentText,
                FontSize = 16,
                TextColor = new SKColor(100, 116, 139)
            };
            innerContainer.AddChild(contentNode);
            
            // Add architecture description text for home page
            if (view == "home")
            {
                var descriptionNode = new UiDsl.SceneNode
                {
                    Type = UiDsl.NodeType.Text,
                    X = 0,
                    Y = 110,
                    Text = "This is a sample application built with Message → Backend → View architecture.",
                    FontSize = 16,
                    TextColor = new SKColor(100, 116, 139)
                };
                innerContainer.AddChild(descriptionNode);
            }
            
            // Add a button
            var button = new UiDsl.SceneNode
            {
                Type = UiDsl.NodeType.Rectangle,
                X = 0,
                Y = 160,
                Width = 200,
                Height = 50,
                FillColor = new SKColor(59, 130, 246),
                BorderColor = new SKColor(29, 78, 216),
                BorderWidth = 2,
                OnClick = $"ShowMessage:{titleText} button clicked!"
            };
            
            var buttonText = new UiDsl.SceneNode
            {
                Type = UiDsl.NodeType.Text,
                X = 40,
                Y = 15,
                Text = view == "home" ? "Get Started" : "Click Me",
                FontSize = 18,
                TextColor = SKColors.White
            };
            button.AddChild(buttonText);
            
            innerContainer.AddChild(button);
            
            // Highlight the selected navigation item
            HighlightSelectedNavItem(view);
            
            _sceneNeedsUpdate = true;
        }
        
        static void HighlightSelectedNavItem(string view)
        {
            if (_rootNode == null) return;
            
            // Find the sidebar container
            var sidebar = _rootNode.Children.Find(n => 
                n.Type == UiDsl.NodeType.Container && 
                n.X == 0 && n.Y >= 60 && 
                n.Width <= 200);
                
            if (sidebar == null) return;
            
            // Find all navigation buttons
            foreach (var child in sidebar.Children)
            {
                if (child.Type == UiDsl.NodeType.Rectangle)
                {
                    // Check if this button is for the current view
                    bool isSelected = false;
                    if (child.OnClick.StartsWith("NavigateTo:"))
                    {
                        string buttonView = child.OnClick.Split(':')[1];
                        isSelected = buttonView.Equals(view, StringComparison.OrdinalIgnoreCase);
                    }
                    
                    // Update appearance based on selection state
                    if (isSelected)
                    {
                        child.FillColor = new SKColor(219, 234, 254); // Light blue
                        child.BorderColor = new SKColor(59, 130, 246); // Blue
                        child.BorderWidth = 2;
                    }
                    else
                    {
                        child.FillColor = new SKColor(224, 242, 254); // Very light blue
                        child.BorderColor = new SKColor(56, 189, 248); // Light blue
                        child.BorderWidth = 0;
                    }
                }
            }
        }

        // Helper method to find a node by ID
        static UiDsl.SceneNode? FindNodeById(UiDsl.SceneNode root, string id)
        {
            if (root.Id == id)
                return root;
                
            if (root.Children != null)
            {
                foreach (var child in root.Children)
                {
                    var result = FindNodeById(child, id);
                    if (result != null)
                        return result;
                }
            }
            
            return null;
        }

        static void Cleanup()
        {
            _surface?.Dispose();
            SDL.SDL_DestroyTexture(_texture);
            SDL.SDL_DestroyRenderer(_renderer);
            SDL.SDL_DestroyWindow(_window);
            SDL.SDL_Quit();
        }

        static void HandleMouseMove(int x, int y)
        {
            if (_rootNode == null) return;
            
            // Find the node at the cursor position
            var hoveredNode = HitTest(_rootNode, x, y);
            
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
                        ProcessAction(_hoveredNode.OnHover);
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
    }
}
