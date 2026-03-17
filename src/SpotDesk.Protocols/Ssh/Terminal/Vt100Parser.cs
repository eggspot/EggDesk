using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace SpotDesk.Protocols.Ssh.Terminal;

/// <summary>
/// VT100/xterm-256color escape sequence parser.
/// Uses System.IO.Pipelines for zero-copy stream handoff.
/// Supports:
///   - Cursor movement: A/B/C/D/E/F/G/H/f
///   - Erase: J (full/partial), K (full/partial)
///   - SGR: bold, italic, underline, reverse, 16/256/truecolor fg/bg
///   - Scroll region: r
///   - Save/restore cursor: s/u
/// </summary>
public class Vt100Parser
{
    private readonly TerminalBuffer _buffer;
    private CellAttributes _currentAttrs = CellAttributes.Default;
    private readonly Pipe _pipe = new();

    // Saved cursor state (ESC [ s / ESC [ u)
    private int _savedRow;
    private int _savedCol;

    public PipeWriter Input => _pipe.Writer;

    public Vt100Parser(TerminalBuffer buffer) => _buffer = buffer;

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var result = await _pipe.Reader.ReadAsync(ct);
            var buf    = result.Buffer;
            var consumed = Process(buf);
            _pipe.Reader.AdvanceTo(consumed);
            if (result.IsCompleted) break;
        }
    }

    private SequencePosition Process(ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);

        while (reader.TryRead(out var b))
        {
            switch (b)
            {
                case 0x1B: // ESC
                    if (!reader.TryRead(out var next)) goto done;
                    if (next == '[')        { if (!TryReadCsi(ref reader)) goto done; }
                    else if (next == '(')   { reader.TryRead(out _); }  // character set designation — skip one
                    else if (next == '7')   { _savedRow = _buffer.CursorRow; _savedCol = _buffer.CursorCol; }
                    else if (next == '8')   { _buffer.SetCursor(_savedRow, _savedCol); }
                    // Other ESC sequences skipped
                    break;

                case 0x08: // BS
                    if (_buffer.CursorCol > 0) _buffer.SetCursor(_buffer.CursorRow, _buffer.CursorCol - 1);
                    break;

                case 0x09: // HT — tab to next 8-column stop
                    var nextTab = (_buffer.CursorCol / 8 + 1) * 8;
                    _buffer.SetCursor(_buffer.CursorRow, Math.Min(nextTab, _buffer.Cols - 1));
                    break;

                case 0x07: // BEL — ignore
                    break;

                case 0x0D: // CR
                    _buffer.CarriageReturn();
                    break;

                case 0x0A: // LF
                case 0x0B:
                case 0x0C:
                    _buffer.NewLine();
                    break;

                case >= 0x20: // printable
                    _buffer.WriteChar((char)b, _currentAttrs);
                    break;
            }
        }

        done:
        return reader.Position;
    }

    private bool TryReadCsi(ref SequenceReader<byte> reader)
    {
        // Read params until final byte (0x40–0x7E)
        var paramBytes = new List<byte>(16);
        while (reader.TryRead(out var b))
        {
            if (b >= 0x40 && b <= 0x7E)
            {
                var paramStr = Encoding.ASCII.GetString(LocalCollectionsMarshal.AsSpan(paramBytes));
                ApplyCsi(b, paramStr);
                return true;
            }
            paramBytes.Add(b);
        }
        return false; // incomplete
    }

    private void ApplyCsi(byte finalByte, string paramStr)
    {
        // Strip leading ? (private mode — DEC sequences)
        var stripped = paramStr.StartsWith('?') ? paramStr[1..] : paramStr;
        var parts = stripped.Split(';', StringSplitOptions.None);
        int P(int idx, int def = 0) =>
            idx < parts.Length && int.TryParse(parts[idx], out var v) ? v : def;

        switch ((char)finalByte)
        {
            // ── Cursor movement ──────────────────────────────────────────
            case 'A': // Cursor Up
                _buffer.SetCursor(Math.Max(0, _buffer.CursorRow - Math.Max(1, P(0, 1))), _buffer.CursorCol);
                break;
            case 'B': // Cursor Down
                _buffer.SetCursor(Math.Min(_buffer.Rows - 1, _buffer.CursorRow + Math.Max(1, P(0, 1))), _buffer.CursorCol);
                break;
            case 'C': // Cursor Forward
                _buffer.SetCursor(_buffer.CursorRow, Math.Min(_buffer.Cols - 1, _buffer.CursorCol + Math.Max(1, P(0, 1))));
                break;
            case 'D': // Cursor Back
                _buffer.SetCursor(_buffer.CursorRow, Math.Max(0, _buffer.CursorCol - Math.Max(1, P(0, 1))));
                break;
            case 'E': // Cursor Next Line
                _buffer.SetCursor(Math.Min(_buffer.Rows - 1, _buffer.CursorRow + Math.Max(1, P(0, 1))), 0);
                break;
            case 'F': // Cursor Previous Line
                _buffer.SetCursor(Math.Max(0, _buffer.CursorRow - Math.Max(1, P(0, 1))), 0);
                break;
            case 'G': // Cursor Horizontal Absolute
                _buffer.SetCursor(_buffer.CursorRow, Math.Max(0, P(0, 1) - 1));
                break;
            case 'H': // Cursor Position
            case 'f':
                _buffer.SetCursor(Math.Max(0, P(0, 1) - 1), Math.Max(0, P(1, 1) - 1));
                break;

            // ── Save/restore ─────────────────────────────────────────────
            case 's':
                _savedRow = _buffer.CursorRow;
                _savedCol = _buffer.CursorCol;
                break;
            case 'u':
                _buffer.SetCursor(_savedRow, _savedCol);
                break;

            // ── Erase ─────────────────────────────────────────────────────
            case 'J': // Erase in Display
                switch (P(0))
                {
                    case 0: _buffer.EraseFromCursorToEnd();   break;
                    case 1: _buffer.EraseFromStartToCursor(); break;
                    case 2:
                    case 3: _buffer.ClearScreen(); break;
                }
                break;

            case 'K': // Erase in Line
                switch (P(0))
                {
                    case 0: _buffer.EraseLineFromCursor();    break;
                    case 1: _buffer.EraseLineToCursor();      break;
                    case 2: _buffer.EraseLine(_buffer.CursorRow); break;
                }
                break;

            case 'L': // Insert Line
                _buffer.InsertLines(Math.Max(1, P(0, 1)));
                break;
            case 'M': // Delete Line
                _buffer.DeleteLines(Math.Max(1, P(0, 1)));
                break;
            case '@': // Insert Character
                _buffer.InsertChars(Math.Max(1, P(0, 1)));
                break;
            case 'P': // Delete Character
                _buffer.DeleteChars(Math.Max(1, P(0, 1)));
                break;

            // ── Scroll region ─────────────────────────────────────────────
            case 'r':
                _buffer.SetScrollRegion(Math.Max(0, P(0, 1) - 1), Math.Min(_buffer.Rows - 1, P(1, _buffer.Rows) - 1));
                break;

            // ── SGR ───────────────────────────────────────────────────────
            case 'm':
                ApplySgr(parts);
                break;

            // All other CSI sequences are silently ignored
        }
    }

    private void ApplySgr(string[] parts)
    {
        if (parts.Length == 0 || (parts.Length == 1 && parts[0] == ""))
        {
            _currentAttrs = CellAttributes.Default;
            return;
        }

        bool bold      = _currentAttrs.Bold;
        bool italic    = _currentAttrs.Italic;
        bool underline = _currentAttrs.Underline;
        bool reverse   = _currentAttrs.Reverse;
        byte fg        = _currentAttrs.Fg;
        byte bg        = _currentAttrs.Bg;

        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var code)) continue;

            switch (code)
            {
                case 0:  bold = italic = underline = reverse = false; fg = 7; bg = 0; break;
                case 1:  bold      = true;  break;
                case 3:  italic    = true;  break;
                case 4:  underline = true;  break;
                case 7:  reverse   = true;  break;
                case 22: bold      = false; break;
                case 23: italic    = false; break;
                case 24: underline = false; break;
                case 27: reverse   = false; break;

                // Standard 8-color fg (30-37) + bright fg (90-97)
                case >= 30 and <= 37: fg = (byte)(code - 30); break;
                case 39:              fg = 7;                  break;
                case >= 90 and <= 97: fg = (byte)(code - 90 + 8); break;

                // Standard 8-color bg (40-47) + bright bg (100-107)
                case >= 40 and <= 47:   bg = (byte)(code - 40); break;
                case 49:                bg = 0;                  break;
                case >= 100 and <= 107: bg = (byte)(code - 100 + 8); break;

                // 256-color: ESC[38;5;n m  or  ESC[48;5;n m
                case 38 when i + 2 < parts.Length && parts[i + 1] == "5":
                    if (byte.TryParse(parts[i + 2], out var fg256)) fg = fg256;
                    i += 2;
                    break;
                case 48 when i + 2 < parts.Length && parts[i + 1] == "5":
                    if (byte.TryParse(parts[i + 2], out var bg256)) bg = bg256;
                    i += 2;
                    break;

                // Truecolor: ESC[38;2;r;g;b m — approximate to nearest xterm256
                case 38 when i + 4 < parts.Length && parts[i + 1] == "2":
                    if (int.TryParse(parts[i + 2], out var r38) &&
                        int.TryParse(parts[i + 3], out var g38) &&
                        int.TryParse(parts[i + 4], out var b38))
                        fg = RgbToXterm256((byte)r38, (byte)g38, (byte)b38);
                    i += 4;
                    break;
                case 48 when i + 4 < parts.Length && parts[i + 1] == "2":
                    if (int.TryParse(parts[i + 2], out var r48) &&
                        int.TryParse(parts[i + 3], out var g48) &&
                        int.TryParse(parts[i + 4], out var b48))
                        bg = RgbToXterm256((byte)r48, (byte)g48, (byte)b48);
                    i += 4;
                    break;
            }
        }

        _currentAttrs = new CellAttributes(fg, bg, bold, italic, underline, reverse);
    }

    /// <summary>
    /// Map a 24-bit RGB color to the nearest xterm-256 palette index.
    /// Uses the 6x6x6 color cube (indices 16-231) for most colors
    /// and the grayscale ramp (232-255) for near-gray values.
    /// </summary>
    private static byte RgbToXterm256(byte r, byte g, byte b)
    {
        // Check grayscale ramp first
        if (Math.Abs(r - g) < 10 && Math.Abs(g - b) < 10)
        {
            if (r < 8)  return 16;
            if (r > 248) return 231;
            return (byte)(Math.Round((r - 8.0) / 247.0 * 24) + 232);
        }

        // Map to 6x6x6 cube
        static int CubeIndex(int v) => v < 48 ? 0 : v < 115 ? 1 : (v - 35) / 40;
        var ri = CubeIndex(r);
        var gi = CubeIndex(g);
        var bi = CubeIndex(b);
        return (byte)(16 + 36 * ri + 6 * gi + bi);
    }
}

// Local alias to avoid ambiguity with System.Runtime.InteropServices.CollectionsMarshal
file static class LocalCollectionsMarshal
{
    public static System.Span<T> AsSpan<T>(List<T> list) =>
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list);
}
