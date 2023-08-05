using System.Diagnostics;
using System.Net.Sockets;
using Timer = System.Timers.Timer;

namespace Chess_Challenge_Server;

public sealed class Player : IDisposable
{
    public readonly string UserName;
    public readonly string SessionId;
    public readonly NetworkStream Stream;
    public readonly TcpClient Client;

    private readonly Stopwatch _clock;
    private CancellationTokenSource? _streamReadCancellationSource;
    private ISerializableMessage? _unreadMessage;
    private Task? _readingTask;

    private object _sendLock = new();

    
    public Player(TcpClient client, string userName, string sessionId)
    {
        UserName = userName;
        Stream = client.GetStream();
        Client = client;
        SessionId = sessionId;

        _clock = new Stopwatch();
    }


    public long TimeElapsedMillis => _clock.ElapsedMilliseconds;
    public void StartClock() => _clock.Start();
    public void PauseClock() => _clock.Stop();

    public void ResetClockTime() => _clock.Reset();


    public void SendMessage(ISerializableMessage message)
    {
        lock (_sendLock)
        {
            _streamReadCancellationSource?.Cancel();

            _readingTask?.Wait();

            Stream.EncodeMessage(message);
            
            StartReadingThread(); // Wait for next message
        }
        
    }
   

    public bool HasNewMessage() => _unreadMessage != null;

    /// <summary>
    /// If this returns null, there is some problem with the connection. BLOCKS UNTIL NEXT MESSAGE IS RECEIVED!
    /// </summary>
    /// <returns></returns>
    public ISerializableMessage? GetNextMessage()
    {
        lock (_sendLock)
        {
            if (_unreadMessage is null)
            {
                if (_readingTask is not null)
                {
                    _readingTask.Wait();
                    return _unreadMessage!;
                }

                Console.WriteLine("Reading Task was null!", true, ConsoleColor.Red);
                return null;
            }

            var msg = _unreadMessage;
            _unreadMessage = null;
            StartReadingThread(); // Start next read
            return msg;
        }
    }
    
    private void StartReadingThread()
    {
        _streamReadCancellationSource = new CancellationTokenSource();
        _unreadMessage = null;
        _readingTask = Task.Run(() =>
        {
            try
            {
                var msg = Stream.DecodeNextMessage(_streamReadCancellationSource.Token);
                while (msg is PingMsg
                       && _streamReadCancellationSource is not null
                       && _streamReadCancellationSource.IsCancellationRequested == false)
                {
                    msg = Stream.DecodeNextMessage(_streamReadCancellationSource.Token);
                }

                if (msg is PingMsg) msg = null;

                _unreadMessage = msg;
            }
            catch (ObjectDisposedException)
            {
                //ignored
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
        });
    }

    public void Dispose()
    {
        Client.Dispose();
        if(_streamReadCancellationSource is not null && _streamReadCancellationSource.IsCancellationRequested == false)
            _streamReadCancellationSource.Cancel();
        
        _streamReadCancellationSource?.Dispose();
        try
        {
            _readingTask?.Wait();
            _readingTask?.Dispose();
            Stream.Dispose();
        }
        catch
        {
            // ignored
        }
    }
}