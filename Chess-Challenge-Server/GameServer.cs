using System.Net;
using System.Net.Sockets;
using static Chess_Challenge_Server.Constants;

namespace Chess_Challenge_Server;

public class GameServer
{
    public static readonly Dictionary<string, MatchRoom> ActiveRooms = new();
    
    private TcpListener server = null;

    public void Start()
    {
        try
        {
            IPAddress localAddr = IPAddress.Parse(SERVER_HOSTNAME);

            server = new TcpListener(localAddr, SERVER_PORT);

            // Start listening for client requests.
            server.Start();

            Console.Write("Waiting for a connection... ");
            server.BeginAcceptTcpClient(HandleClient, server);

            Task.Delay(-1).Wait();

        }
        catch (SocketException e)
        {
            Console.Error.WriteLine("SocketException: {0}", e);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("An exception occured in the TCPListener {0}", e);
        }
        finally
        {
            server.Stop();
        }
    }
    
    private void HandleClient(IAsyncResult result)
    {
        var listener = (TcpListener) result.AsyncState!;
        
        var client = listener.EndAcceptTcpClient(result);
        
        listener.BeginAcceptTcpClient(HandleClient, listener); //Begin another listen for connection

        string roomId = null;
        try
        {
            var serverHello = new ServerHelloMsg
            {
                ProtocolVersion = PROTOCOL_VERSION,
                ServerVersion = SERVER_VERSION,
                SessionId = Guid.NewGuid().ToString()
            };

            // Get a stream object for reading and writing
            var stream = client.GetStream();

            stream.EncodeMessage(serverHello);

            Console.WriteLine("Hello Sent!");

            //TODO: Handle shutdown message
            var clientHello = (ClientHelloMsg) (stream.DecodeNextMessage() ?? throw new Exception("Client sent wrong message!"));

            if (clientHello.ProtocolVersion != PROTOCOL_VERSION)
            {
                Console.WriteLine("Incompatible client! Closing connection..");
                
                stream.EncodeMessage(new Reject());
                
                stream.EncodeMessage(new ShutdownMsg
                {
                    Reason = "Incompatible version!"
                });
                try
                {
                    Task.Delay(1000).ContinueWith(x =>
                    {
                        client.Close(); // Close the connection after a second
                    });
                    return;
                }
                catch
                {
                    // ignored
                }

                return;
            }
            stream.EncodeMessage(new Ack());

            roomId = clientHello.RoomId;

            if (ActiveRooms.TryGetValue(clientHello.RoomId, out var room))
            {
                var roomResult = room.TryAddPlayer(serverHello.SessionId, client, clientHello.UserName);

                if (roomResult == false)
                {
                    Console.WriteLine($"Room {clientHello.RoomId} Full!");
                    stream.EncodeMessage(new Reject());
                    stream.EncodeMessage(new ShutdownMsg
                    {
                        Reason = "Room full"
                    });
                    Task.Delay(1000).ContinueWith(x =>
                    {
                        client.Close();
                    });
                    return;
                }
            }
            else
            {
                room = new MatchRoom(clientHello.RoomId);
                room.TryAddPlayer(serverHello.SessionId, client, clientHello.UserName);
                ActiveRooms.Add(clientHello.RoomId, room);
            }
            
            Console.WriteLine($"Success with roomId {roomId}");
            
            stream.EncodeMessage(new GiveYourPrefs());
            
            var msg = stream.DecodeNextMessage();

            if (msg is ShutdownMsg sMsg)
            {
                Console.WriteLine($"Shutdown Packet from a client! Reason: {sMsg.Reason}");
                client.Close(); // Take action immediately if receive a remote shutdown notification
                return;
            }

            if (msg is not ClientPrefs prefs)
            {
                Console.WriteLine($"Unknown packet from client of type {msg?.GetType().Name ?? "null"}");
                return;
            }

            if (prefs.Games != 1)
            {
                stream.EncodeMessage(new Reject());
                Task.Delay(1000).ContinueWith(x =>
                {
                    client.Close();
                });
                return;
            }
            
            if (room.PlayerMoveTimeMillis == -1)
            {
               room.TrySetPlayerMoveTime(prefs.PreferredClockMillis);
            }

            stream.EncodeMessage(new Ack());
            //TODO: Compare fens from both players
            Task.Run(() => { room.TryStartNewGame(prefs.StartFen); }).ConfigureAwait(false); // Make that run in a new thread

            Console.WriteLine("Transferred responsibility to MatchRoom");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("An error occured while doing client handshake!");
            
            try
            {
                // Dispose immediately
                //TODO: Maybe consider trying to send a shutdown packet?
                
                client.Close();
            }
            finally
            {
                if (roomId != null && ActiveRooms.ContainsKey(roomId))
                    ActiveRooms.Remove(roomId);
            }
        }
    }
    

}