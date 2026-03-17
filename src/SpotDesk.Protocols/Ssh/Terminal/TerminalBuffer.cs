namespace SpotDesk.Protocols.Ssh.Terminal;

/// <summary>
/// 2D character grid with scrollback history.
/// Designed for lock-free read access from the render thread.
/// </summary>
public class TerminalBuffer
{
    private const int DefaultScrollback = 10_000;

    private readonly object _lock = new();

    private int _cols;
    private int _rows;
    private readonly int _scrollback;

    // Ring buffer: each entry is a row of (char, attrs)
    private TerminalCell[][] _lines;
    private int _scrollTop;
    private int _cursorRow;
    private int _cursorCol;

    // Scroll region (set by ESC[r)
    private int _scrollRegionTop;
    private int _scrollRegionBottom;

    private volatile bool _isDirty;

    public int  Cols      => _cols;
    public int  Rows      => _rows;
    public int  CursorRow => _cursorRow;
    public int  CursorCol => _cursorCol;
    public bool IsDirty   => _isDirty;

    public event Action<int, int>? Resized;

    public TerminalBuffer(int cols = 220, int rows = 50, int scrollback = DefaultScrollback)
    {
        _cols   = cols;
        _rows   = rows;
        _scrollback         = scrollback;
        _scrollRegionTop    = 0;
        _scrollRegionBottom = rows - 1;
        _lines = new TerminalCell[scrollback + rows][];
        for (int i = 0; i < _lines.Length; i++)
            _lines[i] = new TerminalCell[cols];
    }

    // ── Write ─────────────────────────────────────────────────────────────

    public void WriteChar(char c, CellAttributes attrs)
    {
        lock (_lock)
        {
            if (_cursorCol >= _cols)
            {
                _cursorCol = 0;
                AdvanceLine();
            }
            var lineIdx = LineIndex(_cursorRow);
            _lines[lineIdx][_cursorCol] = new TerminalCell(c, attrs);
            _cursorCol++;
            _isDirty = true;
        }
    }

    public void NewLine()
    {
        lock (_lock) { _cursorCol = 0; AdvanceLine(); _isDirty = true; }
    }

    public void CarriageReturn()
    {
        lock (_lock) { _cursorCol = 0; _isDirty = true; }
    }

    public void SetCursor(int row, int col)
    {
        lock (_lock)
        {
            _cursorRow = Math.Clamp(row, 0, _rows - 1);
            _cursorCol = Math.Clamp(col, 0, _cols - 1);
        }
    }

    // ── Erase ─────────────────────────────────────────────────────────────

    public void ClearLine(int row)
    {
        lock (_lock) { Array.Clear(_lines[LineIndex(row)]); _isDirty = true; }
    }

    public void EraseLine(int row) => ClearLine(row);

    public void ClearScreen()
    {
        lock (_lock)
        {
            for (int r = 0; r < _rows; r++) Array.Clear(_lines[LineIndex(r)]);
            _isDirty = true;
        }
    }

    public void EraseFromCursorToEnd()
    {
        lock (_lock)
        {
            // Clear from cursor to end of current line
            var line = _lines[LineIndex(_cursorRow)];
            for (int c = _cursorCol; c < _cols; c++) line[c] = default;
            // Clear all rows below
            for (int r = _cursorRow + 1; r < _rows; r++) Array.Clear(_lines[LineIndex(r)]);
            _isDirty = true;
        }
    }

    public void EraseFromStartToCursor()
    {
        lock (_lock)
        {
            // Clear rows above cursor
            for (int r = 0; r < _cursorRow; r++) Array.Clear(_lines[LineIndex(r)]);
            // Clear from start of current line to cursor
            var line = _lines[LineIndex(_cursorRow)];
            for (int c = 0; c <= _cursorCol; c++) line[c] = default;
            _isDirty = true;
        }
    }

    public void EraseLineFromCursor()
    {
        lock (_lock)
        {
            var line = _lines[LineIndex(_cursorRow)];
            for (int c = _cursorCol; c < _cols; c++) line[c] = default;
            _isDirty = true;
        }
    }

    public void EraseLineToCursor()
    {
        lock (_lock)
        {
            var line = _lines[LineIndex(_cursorRow)];
            for (int c = 0; c <= _cursorCol; c++) line[c] = default;
            _isDirty = true;
        }
    }

    // ── Insert/Delete ─────────────────────────────────────────────────────

    public void InsertLines(int count)
    {
        lock (_lock)
        {
            // Scroll content down within scroll region
            for (int n = 0; n < count; n++)
            {
                for (int r = _scrollRegionBottom; r > _cursorRow; r--)
                {
                    var src = _lines[LineIndex(r - 1)];
                    var dst = _lines[LineIndex(r)];
                    Array.Copy(src, dst, _cols);
                }
                Array.Clear(_lines[LineIndex(_cursorRow)]);
            }
            _isDirty = true;
        }
    }

    public void DeleteLines(int count)
    {
        lock (_lock)
        {
            for (int n = 0; n < count; n++)
            {
                for (int r = _cursorRow; r < _scrollRegionBottom; r++)
                {
                    var src = _lines[LineIndex(r + 1)];
                    var dst = _lines[LineIndex(r)];
                    Array.Copy(src, dst, _cols);
                }
                Array.Clear(_lines[LineIndex(_scrollRegionBottom)]);
            }
            _isDirty = true;
        }
    }

    public void InsertChars(int count)
    {
        lock (_lock)
        {
            var line = _lines[LineIndex(_cursorRow)];
            // Shift right
            for (int c = _cols - 1; c >= _cursorCol + count; c--)
                line[c] = line[c - count];
            for (int c = _cursorCol; c < _cursorCol + count && c < _cols; c++)
                line[c] = default;
            _isDirty = true;
        }
    }

    public void DeleteChars(int count)
    {
        lock (_lock)
        {
            var line = _lines[LineIndex(_cursorRow)];
            for (int c = _cursorCol; c < _cols - count; c++)
                line[c] = line[c + count];
            for (int c = _cols - count; c < _cols; c++)
                line[c] = default;
            _isDirty = true;
        }
    }

    // ── Scroll region ─────────────────────────────────────────────────────

    public void SetScrollRegion(int top, int bottom)
    {
        lock (_lock)
        {
            _scrollRegionTop    = Math.Clamp(top,    0, _rows - 1);
            _scrollRegionBottom = Math.Clamp(bottom, 0, _rows - 1);
        }
    }

    // ── Resize ────────────────────────────────────────────────────────────

    public void Resize(int cols, int rows)
    {
        lock (_lock)
        {
            _cols = cols;
            _rows = rows;
            _cursorRow = Math.Min(_cursorRow, rows - 1);
            _cursorCol = Math.Min(_cursorCol, cols - 1);
            _scrollRegionTop    = 0;
            _scrollRegionBottom = rows - 1;
        }
        Resized?.Invoke(cols, rows);
    }

    // ── Read (render thread) ──────────────────────────────────────────────

    public TerminalCell GetCell(int row, int col)
    {
        var line = _lines[LineIndex(row)];
        return col >= 0 && col < line.Length ? line[col] : default;
    }

    public ReadOnlySpan<TerminalCell> GetRow(int row) =>
        _lines[LineIndex(row)].AsSpan();

    public void ClearDirty() => _isDirty = false;

    // ── Internal ──────────────────────────────────────────────────────────

    private int LineIndex(int visualRow) =>
        (_scrollTop + visualRow + _lines.Length) % _lines.Length;

    private void AdvanceLine()
    {
        _cursorRow++;
        if (_cursorRow > _scrollRegionBottom)
        {
            // Scroll within the scroll region
            _scrollTop = (_scrollTop + 1) % _lines.Length;
            _cursorRow = _scrollRegionBottom;
            Array.Clear(_lines[LineIndex(_cursorRow)]);
        }
    }
}

public readonly record struct TerminalCell(char Char, CellAttributes Attributes);

public readonly record struct CellAttributes(
    byte Fg,
    byte Bg,
    bool Bold,
    bool Italic,
    bool Underline,
    bool Reverse)
{
    public static readonly CellAttributes Default = new(7, 0, false, false, false, false);
}
