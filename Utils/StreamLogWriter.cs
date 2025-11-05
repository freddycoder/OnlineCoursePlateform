using System.Text;

namespace OnlineCoursePlateform.Utils;

public class StreamLogWriter : TextWriter
{
    private readonly Action<string> _onLine;
    private readonly TextWriter _inner;

    public StreamLogWriter(Action<string> onLine, TextWriter inner)
    {
        _onLine = onLine;
        _inner = inner;
    }

    public override Encoding Encoding => _inner.Encoding;

    public override void WriteLine(string? value)
    {
        if (value == null) return;
        _onLine(value);
        _inner.WriteLine(value);
    }

    public override void Write(char value)
    {
        _inner.Write(value);
    }
}