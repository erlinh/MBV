using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using SkiaSharp;
using System;
using System.Numerics;
using Silk.NET.Maths;

class Program
{
    private static IWindow _window = null!;
    private static GL _gl = null!;
    private static GRContext _grContext = null!;
    private static GRBackendRenderTarget _renderTarget = null!;
    private static SKSurface _surface = null!;
    private static bool _initialized = false;

    static void Main()
    {
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(800, 600),
            Title = "MBV + Silk.NET + Skia",
            VSync = true,
            PreferredDepthBufferBits = 24,
            PreferredStencilBufferBits = 8,
            API = GraphicsAPI.Default with
            {
                API = ContextAPI.OpenGL,
                Version = new APIVersion(3, 3),
                Profile = ContextProfile.Core
            }
        };

        _window = Window.Create(options);

        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnResize;
        _window.Closing += OnClose;

        _window.Run();
    }

    private static void OnLoad()
    {
        // Create OpenGL context
        _gl = _window.CreateOpenGL();
        
        // Enable VSync
        _gl.Enable(EnableCap.FramebufferSrgb);
        
        // Create Skia context using a direct OpenGL interface
        var glInterface = GRGlInterface.CreateAngle();
        if (glInterface == null || !glInterface.Validate())
        {
            // Fallback to the default interface
            glInterface = GRGlInterface.Create();
        }
        
        _grContext = GRContext.CreateGl(glInterface);
        if (_grContext == null)
        {
            Console.WriteLine("Failed to create Skia GRContext");
            _window.Close();
            return;
        }

        RecreateRenderTarget();
        _initialized = true;
    }

    private static void OnResize(Vector2D<int> size)
    {
        if (!_initialized) return;
        
        _surface?.Dispose();
        _renderTarget?.Dispose();
        RecreateRenderTarget();
    }

    private static void RecreateRenderTarget()
    {
        if (_grContext == null) return;
        
        var width = Math.Max(1, _window.FramebufferSize.X);
        var height = Math.Max(1, _window.FramebufferSize.Y);

        var fbInfo = new GRGlFramebufferInfo(0, 0x8058); // GL_RGBA8
        _renderTarget = new GRBackendRenderTarget(width, height, 0, 8, fbInfo);
        _surface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        
        if (_surface == null)
        {
            Console.WriteLine("Failed to create Skia surface");
            _window.Close();
        }
    }

    private static void OnRender(double delta)
    {
        if (!_initialized || _surface == null) return;
        
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        
        var canvas = _surface.Canvas;
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint { Color = SKColors.DarkSlateBlue, IsAntialias = true };
        using var font = new SKFont { Size = 32 };
        canvas.DrawText("Hello Silk + Skia!", 20, 60, SKTextAlign.Left, font, paint);

        _surface.Canvas.Flush();
        _grContext.Flush();
    }

    private static void OnClose()
    {
        _initialized = false;
        _surface?.Dispose();
        _renderTarget?.Dispose();
        _grContext?.Dispose();
    }
}
