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

            SendMessage(serverHello, stream);

            Console.WriteLine("Hello Sent!");

            var clientHello = ReceiveClientHello(stream);

            if (clientHello.ProtocolVersion != PROTOCOL_VERSION)
            {
                Console.WriteLine("Incompatible client! Closing connection..");
                client.Close();
                return;
            }

            roomId = clientHello.RoomId;

            bool isWhite;
            if (ActiveRooms.TryGetValue(clientHello.RoomId, out var room))
            {
                var roomResult = room.TryAddPlayer(serverHello.SessionId, client, false, out isWhite);

                if (roomResult == false)
                {
                    Console.WriteLine("Room Full!"); // TODO: Sent this to client too before closing
                    client.Close();
                    return;
                }
            }
            else
            {
                room = new MatchRoom(clientHello.RoomId);
                room.TryAddPlayer(serverHello.SessionId, client, false, out isWhite);
                ActiveRooms.Add(clientHello.RoomId, room);
            }


            SendMessage(new RoomInfo
            {
                RoomId = clientHello.RoomId,
                StartsOffAsWhite = isWhite
            }, stream);


            Task.Run(() => { room.TryStartNewGame(); }).ConfigureAwait(false); // Make that run in a new thread

            Console.WriteLine("Transferred responsibility to MatchRoom");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("An error occured while doing client handshake!");
            
            try
            {
                client.Close();
            }
            finally
            {
                if (roomId != null && ActiveRooms.ContainsKey(roomId))
                    ActiveRooms.Remove(roomId);
            }
        }

        ClientHelloMsg ReceiveClientHello(NetworkStream networkStream)
        {
            var result = ReadMessage<ClientHelloMsg>(networkStream);
            
            Console.WriteLine(result.ProtocolVersion);
            Console.WriteLine(result.ClientVersion);
            Console.WriteLine(result.RoomId);
            return result;
        }
    }
    
    
    static void SendMessage(ISerializableMessage message, Stream stream) => message.SerializeIntoStream(stream);

    static T ReadMessage<T>(Stream stream) where T : ISerializableMessage, new()
    {
        var ret = new T();
            
        ret.ReadFromStream(stream);
        return ret;
    }

}