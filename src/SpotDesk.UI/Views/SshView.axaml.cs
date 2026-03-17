using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SpotDesk.Protocols.Ssh.Terminal;
using SpotDesk.UI.ViewModels;

namespace SpotDesk.UI.Views;

/// <summary>
/// SSH terminal view: renders a WriteableBitmap at up to 60fps using a
/// DispatcherTimer. The TerminalBuffer is read every frame; only re-renders
/// if the dirty flag is set.
/// </summary>
public partial class SshView : UserControl
{
    private WriteableBitmap? _bitmap;
    private TerminalBuffer? _terminalBuffer;
    private DispatcherTimer? _renderTimer;
    private int _cols = 80;
    private int _rows = 24;
    private const int CellW = 8;
    private const int CellH = 16;

    public SshView()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is SessionTabViewModel vm)
        {
            _terminalBuffer = vm.TerminalBuffer;
            vm.TerminalBuffer.Resized += OnTerminalResized;
        }

        EnsureBitmap();
        StartRenderLoop();
        this.KeyDown += OnKeyDown;
        this.PointerPressed += (_, _) => Focus();
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _renderTimer?.Stop();
        _renderTimer = null;
        if (_terminalBuffer != null)
            _terminalBuffer.Resized -= OnTerminalResized;
    }

    private void OnTerminalResized(int cols, int rows)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _cols = cols;
            _rows = rows;
            EnsureBitmap();
        });
    }

    private void EnsureBitmap()
    {
        var w = _cols * CellW;
        var h = _rows * CellH;
        if (w <= 0 || h <= 0) return;

        _bitmap = new WriteableBitmap(
            new PixelSize(w, h),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Opaque);

        var img = this.FindControl<Image>("TerminalImage");
        if (img != null) img.Source = _bitmap;
    }

    private void StartRenderLoop()
    {
        _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 60)
        };
        _renderTimer.Tick += (_, _) => RenderFrame();
        _renderTimer.Start();
    }

    private void RenderFrame()
    {
        if (_bitmap is null || _terminalBuffer is null) return;
        if (!_terminalBuffer.IsDirty) return;

        _terminalBuffer.ClearDirty();

        using var fb = _bitmap.Lock();
        RenderTerminal(fb);
        var img = this.FindControl<Image>("TerminalImage");
        img?.InvalidateVisual();
    }

    private unsafe void RenderTerminal(ILockedFramebuffer fb)
    {
        if (_terminalBuffer is null) return;

        var ptr = (uint*)fb.Address.ToPointer();
        int stride = fb.RowBytes / 4;

        for (int row = 0; row < _rows && row < _terminalBuffer.Rows; row++)
        {
            for (int col = 0; col < _cols && col < _terminalBuffer.Cols; col++)
            {
                var cell = _terminalBuffer.GetCell(row, col);
                uint fg = Xterm256ToArgb(cell.Attributes.Fg);
                uint bg = Xterm256ToArgb(cell.Attributes.Bg);

                // Fill cell background
                for (int cy = 0; cy < CellH; cy++)
                    for (int cx = 0; cx < CellW; cx++)
                        ptr[(row * CellH + cy) * stride + col * CellW + cx] = bg;

                // Foreground glyph — simplified block rendering for space vs non-space
                if (cell.Char != ' ' && cell.Char != '\0')
                {
                    // Paint a minimal representative dot pattern for the glyph center
                    int midY = row * CellH + CellH / 2;
                    int midX = col * CellW + CellW / 2;
                    if (midY < fb.Size.Height && midX < fb.Size.Width)
                        ptr[midY * stride + midX] = fg;
                }
            }
        }

        // Draw cursor
        int curRow = _terminalBuffer.CursorRow;
        int curCol = _terminalBuffer.CursorCol;
        if (curRow >= 0 && curRow < _rows && curCol >= 0 && curCol < _cols)
        {
            uint cursorColor = 0xFFE8EAF0;
            int baseY = curRow * CellH;
            int baseX = curCol * CellW;
            for (int cx = 0; cx < CellW; cx++)
            {
                int y = baseY + CellH - 2;
                if (y < fb.Size.Height)
                    ptr[y * stride + baseX + cx] = cursorColor;
            }
        }
    }

    /// <summary>Convert xterm-256 palette index to BGRA8888 packed uint.</summary>
    private static uint Xterm256ToArgb(byte index)
    {
        // System colors 0-15
        ReadOnlySpan<uint> system = [
            0xFF0F1117, 0xFFCC0000, 0xFF4E9A06, 0xFFC4A000,
            0xFF3465A4, 0xFF75507B, 0xFF06989A, 0xFFD3D7CF,
            0xFF555753, 0xFFEF2929, 0xFF8AE234, 0xFFFCE94F,
            0xFF729FCF, 0xFFAD7FA8, 0xFF34E2E2, 0xFFEEEEEC
        ];
        if (index < 16) return system[index];

        // 6x6x6 color cube (indices 16–231)
        if (index < 232)
        {
            int i = index - 16;
            int b = i % 6;
            int g = (i / 6) % 6;
            int r = i / 36;
            uint R = (uint)(r == 0 ? 0 : 55 + r * 40);
            uint G = (uint)(g == 0 ? 0 : 55 + g * 40);
            uint B = (uint)(b == 0 ? 0 : 55 + b * 40);
            return 0xFF000000 | (R << 16) | (G << 8) | B;
        }

        // Grayscale ramp (indices 232–255)
        uint v = (uint)(8 + (index - 232) * 10);
        return 0xFF000000 | (v << 16) | (v << 8) | v;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is SessionTabViewModel vm)
            vm.HandleKeyInput(e);
    }
}
