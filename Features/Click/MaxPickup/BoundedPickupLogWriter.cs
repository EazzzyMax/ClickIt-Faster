using System.Threading.Channels;

namespace ClickItMax.Features.Click.MaxPickup;

internal sealed class BoundedPickupLogWriter : IDisposable
{
    private readonly Channel<object> _queue;
    private readonly Task _worker;
    private readonly Func<int, TextWriter> _open;
    private readonly int _rotateChars;
    private long _dropped, _errors, _written;
    internal long Dropped => Interlocked.Read(ref _dropped);
    internal long Errors => Interlocked.Read(ref _errors);
    internal long Written => Interlocked.Read(ref _written);

    internal BoundedPickupLogWriter(string path) : this(part =>
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string partPath = part == 0 ? path : Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-part" + part + ".jsonl");
        return new StreamWriter(new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false), 65536);
    }) { }

    // The injected writer is also used to test full queues, slow disks and I/O failures without game input.
    internal BoundedPickupLogWriter(Func<int, TextWriter> open, int capacity = 256, int rotateChars = 16 * 1024 * 1024)
    {
        _open = open; _rotateChars = rotateChars;
        _queue = Channel.CreateBounded<object>(new BoundedChannelOptions(capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        _worker = Task.Run(WriteLoop);
    }

    internal bool TryWrite(object record)
    {
        if (_queue.Writer.TryWrite(record)) return true;
        Interlocked.Increment(ref _dropped); return false;
    }

    private async Task WriteLoop()
    {
        TextWriter? writer = null; int chars = 0, part = 0;
        try
        {
            while (await _queue.Reader.WaitToReadAsync())
            {
                int batch = 0;
                while (batch++ < 32 && _queue.Reader.TryRead(out var record))
                {
                    try
                    {
                        string json = JsonConvert.SerializeObject(record);
                        writer ??= _open(part);
                        writer.WriteLine(json); chars += json.Length + 2; Interlocked.Increment(ref _written);
                        if (chars >= _rotateChars) { writer.Dispose(); writer = null; chars = 0; part = (part + 1) % 4; }
                    }
                    catch { Interlocked.Increment(ref _errors); try { writer?.Dispose(); } catch { } writer = null; }
                }
                try { writer?.Flush(); } catch { Interlocked.Increment(ref _errors); }
            }
        }
        catch { Interlocked.Increment(ref _errors); }
        finally { try { writer?.Dispose(); } catch { } }
    }

    public void Dispose()
    {
        _queue.Writer.TryComplete();
        try { _worker.Wait(500); } catch { } // Only shutdown may wait; input never waits for disk or queue space.
    }
}
