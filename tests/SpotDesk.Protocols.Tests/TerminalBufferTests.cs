using SpotDesk.Protocols.Ssh.Terminal;
using Xunit;

namespace SpotDesk.Protocols.Tests;

public class TerminalBufferTests
{
    [Fact]
    public void WriteChar_ThenGetRow_ReturnsChar()
    {
        var buf = new TerminalBuffer(80, 24);
        buf.WriteChar('A', CellAttributes.Default);

        var row = buf.GetRow(0);
        Assert.Equal('A', row[0].Char);
    }

    [Fact]
    public void CarriageReturn_ResetsCursorCol()
    {
        var buf = new TerminalBuffer(80, 24);
        buf.WriteChar('X', CellAttributes.Default);
        buf.CarriageReturn();
        Assert.Equal(0, buf.CursorCol);
    }

    [Fact]
    public void NewLine_AdvancesRow()
    {
        var buf = new TerminalBuffer(80, 24);
        var initialRow = buf.CursorRow;
        buf.NewLine();
        Assert.Equal(0, buf.CursorCol);
    }

    [Fact]
    public void ClearLine_EmptiesRow()
    {
        var buf = new TerminalBuffer(80, 24);
        buf.WriteChar('Z', CellAttributes.Default);
        buf.ClearLine(0);

        var row = buf.GetRow(0);
        Assert.Equal('\0', row[0].Char);
    }

    [Fact]
    public void SetCursor_ClampsToBounds()
    {
        var buf = new TerminalBuffer(80, 24);
        buf.SetCursor(100, 200);
        Assert.Equal(23, buf.CursorRow);
        Assert.Equal(79, buf.CursorCol);
    }
}
