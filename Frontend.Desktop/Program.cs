using SDL2;
using SkiaSharp;
using System;
using System.Runtime.InteropServices;

namespace Frontend.Desktop
{
    class Program
    {
        // Window dimensions
        private const int Width = 800;
        private const int Height = 600;
        private const string WindowTitle = "MBV (CPU Rendering)";

        // SDL window and renderer
        private static IntPtr _window;
        private static IntPtr _renderer;
        private static IntPtr _texture;
        
        // Skia objects
        private static SKSurface? _surface;
        private static SKCanvas? _canvas;
        private static byte[]? _pixelData;
        
        // Main loop control
        private static bool _running = true;

        static void Main(string[] args)
        {
            Console.WriteLine("Starting SDL2 application...");
            // Initialize SDL
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
            {
                Console.WriteLine($"SDL could not initialize! SDL_Error: {SDL.SDL_GetError()}");
                return;
            }

            Console.WriteLine("SDL initialized successfully");
            // Create window
            _window = SDL.SDL_CreateWindow(
                WindowTitle,
                SDL.SDL_WINDOWPOS_CENTERED,
                SDL.SDL_WINDOWPOS_CENTERED,
                Width, Height,
                SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);

            if (_window == IntPtr.Zero)
            {
                Console.WriteLine($"Window could not be created! SDL_Error: {SDL.SDL_GetError()}");
                SDL.SDL_Quit();
                return;
            }

            // Create renderer
            _renderer = SDL.SDL_CreateRenderer(
                _window, -1,
                SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED |
                SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);

            if (_renderer == IntPtr.Zero)
            {
                Console.WriteLine($"Renderer could not be created! SDL_Error: {SDL.SDL_GetError()}");
                SDL.SDL_DestroyWindow(_window);
                SDL.SDL_Quit();
                return;
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

            Console.WriteLine("Starting main loop");
            // Main loop
            RunMainLoop();

            // Cleanup
            _surface?.Dispose();
            SDL.SDL_DestroyTexture(_texture);
            SDL.SDL_DestroyRenderer(_renderer);
            SDL.SDL_DestroyWindow(_window);
            SDL.SDL_Quit();
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
                    
                    // Handle other input events here
                    HandleInputEvent(sdlEvent);
                }

                // Render frame
                RenderFrame();

                // Get pixel data from surface
                if (_surface != null && _pixelData != null)
                {
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
            }
        }

        static void RenderFrame()
        {
            if (_canvas == null) return;
            
            // Clear the canvas
            _canvas.Clear(SKColors.White);

            // Drawing code
            using var paint = new SKPaint
            {
                Color = SKColors.Navy,
                IsAntialias = true,
                StrokeWidth = 2,
                Style = SKPaintStyle.Fill
            };
            
            // Use SKFont instead of SKPaint.TextSize
            using var font = new SKFont { Size = 32 };
            using var textPaint = new SKPaint
            {
                Color = SKColors.DarkSlateBlue,
                IsAntialias = true
            };

            // Draw some shapes
            _canvas.DrawCircle(Width / 2, Height / 2, 100, paint);
            
            paint.Color = SKColors.Crimson;
            _canvas.DrawRect(50, 50, 150, 100, paint);

            // Draw text using the new API
            _canvas.DrawText("MBV - CPU Rendering", 20, 40, SKTextAlign.Left, font, textPaint);
            
            // Flush the canvas operations to the pixel data
            _canvas.Flush();
        }

        static void HandleInputEvent(SDL.SDL_Event sdlEvent)
        {
            // Add custom input handling code here
            // This can handle mouse clicks, key presses, etc.
            
            switch (sdlEvent.type)
            {
                case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
                    Console.WriteLine($"Mouse clicked at: {sdlEvent.button.x}, {sdlEvent.button.y}");
                    break;
                    
                case SDL.SDL_EventType.SDL_MOUSEMOTION:
                    // Handle mouse motion if needed
                    break;
                    
                case SDL.SDL_EventType.SDL_KEYDOWN:
                    // Already handling ESC in the main loop, add other keys here
                    break;
            }
        }
    }
}
