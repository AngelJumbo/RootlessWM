using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Drawing.Text;

namespace RootlessWM.Platform.Win32;

internal sealed class Direct2DRenderer : IDisposable
{
    private static readonly Guid D2DFactoryId = new("06152247-6f50-465a-9245-118bfd3b6007");
    private static readonly Guid DWriteFactoryId = new("b859ee5a-d838-4b5b-a2e8-1adc7d93db48");
    private nint _d2dFactory;
    private nint _writeFactory;

    public Direct2DRenderer()
    {
        var d2dFactoryId = D2DFactoryId;
        var dWriteFactoryId = DWriteFactoryId;
        ThrowIfFailed(NativeMethods.D2D1CreateFactory(0, ref d2dFactoryId, nint.Zero, out _d2dFactory));
        ThrowIfFailed(NativeMethods.DWriteCreateFactory(0, ref dWriteFactoryId, out _writeFactory));
    }

    public float Dpi { get; set; } = 96;

    public void Render(Bitmap bitmap, Action<Direct2DCanvas> draw)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(draw);

        using var graphics = Graphics.FromImage(bitmap);
        var hdc = graphics.GetHdc();
        nint target = nint.Zero;
        try
        {
            var properties = new RenderTargetProperties
            {
                Type = 0,
                PixelFormat = new PixelFormatDescription { Format = 87, AlphaMode = 1 },
                DpiX = Dpi,
                DpiY = Dpi,
                Usage = 0,
                MinLevel = 0
            };
            ThrowIfFailed(Call<CreateDCRenderTarget>(16, _d2dFactory, ref properties, out target));
            var bounds = new NativeMethods.Rect { Left = 0, Top = 0, Right = bitmap.Width, Bottom = bitmap.Height };
            ThrowIfFailed(Call<BindDC>(57, target, hdc, ref bounds));
            var canvas = new Direct2DCanvas(target, _writeFactory);
            ((BeginDrawDelegate)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(target), 48 * IntPtr.Size), typeof(BeginDrawDelegate)))(target);
            canvas.Clear(Color.Transparent);
            draw(canvas);
            ThrowIfFailed(Call<EndDraw>(49, target, out _, out _));
            Marshal.Release(target);
            target = nint.Zero;
            graphics.ReleaseHdc(hdc);
            hdc = nint.Zero;
            canvas.RenderText(bitmap);
        }
        finally
        {
            if (target != nint.Zero)
            {
                Marshal.Release(target);
            }

            if (hdc != nint.Zero)
            {
                graphics.ReleaseHdc(hdc);
            }
        }
    }

    public void Dispose()
    {
        if (_writeFactory != nint.Zero)
        {
            Marshal.Release(_writeFactory);
            _writeFactory = nint.Zero;
        }

        if (_d2dFactory != nint.Zero)
        {
            Marshal.Release(_d2dFactory);
            _d2dFactory = nint.Zero;
        }
    }

    internal static float MeasureText(string text, string family, float pixelSize, FontStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        using var bitmap = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new Font(family, pixelSize, style, GraphicsUnit.Pixel);
        return graphics.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic).Width;
    }

    private static void ThrowIfFailed(int result)
    {
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    private static int Call<T>(int index, nint instance, ref RenderTargetProperties properties, out nint target) where T : Delegate
        => ((CreateDCRenderTarget)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), index * IntPtr.Size), typeof(CreateDCRenderTarget)))(instance, ref properties, out target);

    private static int Call<T>(int index, nint instance, nint hdc, ref NativeMethods.Rect bounds) where T : Delegate
        => ((BindDC)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), index * IntPtr.Size), typeof(BindDC)))(instance, hdc, ref bounds);

    private static int Call<T>(int index, nint instance, out nint tag1, out nint tag2) where T : Delegate
        => ((EndDraw)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), index * IntPtr.Size), typeof(EndDraw)))(instance, out tag1, out tag2);

    [StructLayout(LayoutKind.Sequential)] private struct PixelFormatDescription { public int Format; public int AlphaMode; }
    [StructLayout(LayoutKind.Sequential)] private struct RenderTargetProperties { public int Type; public PixelFormatDescription PixelFormat; public float DpiX, DpiY; public int Usage, MinLevel; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateDCRenderTarget(nint instance, ref RenderTargetProperties properties, out nint target);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int BindDC(nint instance, nint hdc, ref NativeMethods.Rect bounds);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EndDraw(nint instance, out nint tag1, out nint tag2);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void BeginDrawDelegate(nint instance);

    internal sealed class Direct2DCanvas
    {
        private readonly nint _target;
        private readonly nint _writeFactory;

        public Direct2DCanvas(nint target, nint writeFactory)
        {
            _target = target;
            _writeFactory = writeFactory;
        }

        public void Clear(Color color)
        {
            var value = ToColor4(color);
            Invoke<ClearDelegate>(47, _target)(_target, ref value);
        }

        public void FillRoundedRectangle(RectangleF rectangle, float radius, Color color)
        {
            using var brush = CreateBrush(color);
            var value = ToRoundedRectangle(rectangle, radius);
            Invoke<FillRoundedRectangleDelegate>(19, _target)(_target, ref value, brush.Handle);
        }

        public void DrawRoundedRectangle(RectangleF rectangle, float radius, Color color, float strokeWidth)
        {
            using var brush = CreateBrush(color);
            var value = ToRoundedRectangle(rectangle, radius);
            Invoke<DrawRoundedRectangleDelegate>(18, _target)(_target, ref value, brush.Handle, strokeWidth, nint.Zero);
        }

        public void DrawText(string text, RectangleF rectangle, Color color, string family, float pixelSize, FontStyle style, TextAlignment alignment)
        {
            if (string.IsNullOrEmpty(text) || rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                return;
            }

            _textCommands.Add(new TextCommand(text, rectangle, color, family, pixelSize, style, alignment));
        }

        private readonly List<TextCommand> _textCommands = [];

        public void RenderText(Bitmap bitmap)
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            foreach (var command in _textCommands)
            {
                using var font = new Font(command.Family, command.PixelSize, command.Style, GraphicsUnit.Pixel);
                using var brush = new SolidBrush(command.Color);
                using var format = new StringFormat(StringFormat.GenericTypographic)
                {
                    Alignment = command.Alignment switch
                    {
                        TextAlignment.Center => StringAlignment.Center,
                        TextAlignment.Right => StringAlignment.Far,
                        _ => StringAlignment.Near
                    },
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                graphics.DrawString(command.Text, font, brush, command.Rectangle, format);
            }

            _textCommands.Clear();
        }

        private BrushHandle CreateBrush(Color color)
        {
            var value = ToColor4(color);
            ThrowIfFailed(Invoke<CreateSolidColorBrushDelegate>(8, _target)(_target, ref value, nint.Zero, out var brush));
            return new BrushHandle(brush);
        }

        private static RoundedRectangle ToRoundedRectangle(RectangleF rectangle, float radius)
            => new() { Rect = ToRect(rectangle), RadiusX = Math.Max(0, radius), RadiusY = Math.Max(0, radius) };

        private static RectF ToRect(RectangleF rectangle)
            => new() { Left = rectangle.Left, Top = rectangle.Top, Right = rectangle.Right, Bottom = rectangle.Bottom };

        private static Color4 ToColor4(Color color)
            => new() { R = color.R / 255F, G = color.G / 255F, B = color.B / 255F, A = color.A / 255F };

        private static T Invoke<T>(int index, nint instance) where T : Delegate
            => (T)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), index * IntPtr.Size), typeof(T));

        private readonly struct BrushHandle : IDisposable
        {
            public BrushHandle(nint handle) => Handle = handle;
            public nint Handle { get; }
            public void Dispose() => Marshal.Release(Handle);
        }

        internal enum TextAlignment : uint { Left = 0, Right = 1, Center = 2 }
        private sealed record TextCommand(string Text, RectangleF Rectangle, Color Color, string Family, float PixelSize, FontStyle Style, TextAlignment Alignment);
        [StructLayout(LayoutKind.Sequential)] private struct Color4 { public float R, G, B, A; }
        [StructLayout(LayoutKind.Sequential)] private struct RectF { public float Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct RoundedRectangle { public RectF Rect; public float RadiusX, RadiusY; }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void ClearDelegate(nint instance, ref Color4 color);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void FillRoundedRectangleDelegate(nint instance, ref RoundedRectangle rectangle, nint brush);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void DrawRoundedRectangleDelegate(nint instance, ref RoundedRectangle rectangle, nint brush, float width, nint strokeStyle);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateSolidColorBrushDelegate(nint instance, ref Color4 color, nint properties, out nint brush);
    }
}