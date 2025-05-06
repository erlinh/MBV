using SDL2;
using SkiaSharp;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Frontend.Desktop.UiDsl;
using Frontend.Desktop.MessageBus;
using Frontend.Desktop.State;

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

        static void Main(string[] args)
        {
            Console.WriteLine("Starting MBV Application...");
            try
            {
                Initialize();
                RunMainLoop();
                Cleanup();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Application error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static void Initialize()
        {
            // Initialize MBV components
            InitializeMbvComponents();
            
            // Initialize SDL
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
            {
                throw new Exception($"SDL could not initialize! SDL_Error: {SDL.SDL_GetError()}");
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
                throw new Exception($"Window could not be created! SDL_Error: {SDL.SDL_GetError()}");
            }

            // Create renderer
            _renderer = SDL.SDL_CreateRenderer(
                _window, -1,
                SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED |
                SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);

            if (_renderer == IntPtr.Zero)
            {
                throw new Exception($"Renderer could not be created! SDL_Error: {SDL.SDL_GetError()}");
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
                if (File.Exists(dslPath))
                {
                    _rootNode = _dslParser.ParseFile(dslPath);
                }
                else
                {
                    // Create a default UI if file doesn't exist
                    _rootNode = CreateDefaultUi();
                }
                
                _skRenderer.SetSceneGraph(_rootNode);
                Console.WriteLine("UI loaded successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading UI: {ex.Message}");
                _rootNode = CreateDefaultUi();
                _skRenderer.SetSceneGraph(_rootNode);
            }
        }

        private static SceneNode CreateDefaultUi()
        {
            // Create a minimal default UI
            var root = new SceneNode
            {
                Type = NodeType.Container,
                Width = Width,
                Height = Height,
                BackgroundColor = new SKColor(240, 240, 240)
            };
            
            var title = new SceneNode
            {
                Type = NodeType.Text,
                X = 20,
                Y = 50,
                Text = "MBV Application",
                FontSize = 32,
                TextColor = SKColors.DarkBlue
            };
            root.AddChild(title);
            
            var subtitle = new SceneNode
            {
                Type = NodeType.Text,
                X = 20,
                Y = 100,
                Text = "UI DSL file not found. This is a default fallback UI.",
                FontSize = 18,
                TextColor = SKColors.Gray
            };
            root.AddChild(subtitle);
            
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
            // In a full implementation, this would find the node at the given coordinates
            // and trigger its onClick handler by sending a message through the bus
            Console.WriteLine($"Mouse clicked at: {x}, {y}");
            
            // For demonstration, toggle sidebar on clicks in the left area
            if (x < 200)
            {
                _appStore.ToggleSidebar();
            }
            // Handle navigation menu clicks
            else if (x > 200 && x < 800 && y > 60 && y < 150)
            {
                _appStore.NavigateTo("clicked");
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
