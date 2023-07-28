using System.Net.Sockets;

namespace Chess_Challenge_Server;

public class MatchRoom
{
    public readonly string RoomId;
    
    public Player? WhitePlayer { get; private set; }
    public Player? BlackPlayer { get; private set; }

    public Player? NextToMove;
    
    public bool RoomFull { get; private set; }
    
    public MatchRoom(string roomId)
    {
        RoomId = roomId;
    }

    public bool TryStartNewGame()
    {
        if (RoomFull == false)
            return false;

        try
        {
            while (true) // Run until an exception occurs usually this means someone disconnects
            {

                WhitePlayer.SendMessage(new GameSettings
                {
                    IsWhite = true,
                    TimeForEachPlayer = (int)TimeSpan.FromMinutes(5).TotalSeconds
                });

                BlackPlayer.SendMessage(new GameSettings
                {
                    IsWhite = false,
                    TimeForEachPlayer = (int)TimeSpan.FromMinutes(5).TotalSeconds
                });

                WhitePlayer.ReadMessage<IsReady>();
                BlackPlayer.ReadMessage<IsReady>();

                var gameStart = new GameStart
                {
                    Timestamp = DateTime.UtcNow.Ticks
                };
                
                WhitePlayer.SendMessage(gameStart);
                BlackPlayer.SendMessage(gameStart);

                Console.WriteLine($"{WhitePlayer.SessionId} is white");
                var moveMessage = BlackPlayer.ReadMessage<MoveMessage>(); // first read from black cuz his opponenet is white

                NextToMove = WhitePlayer; // and then send that to white to its first move


                do
                {
                    NextToMove.SendMessage(moveMessage);
                    moveMessage = NextToMove.ReadMessage<MoveMessage>();
                    moveMessage.Clock = DateTime.UtcNow.Ticks - gameStart.Timestamp; // set time on every message

                    // swap
                    if (NextToMove == WhitePlayer)
                        NextToMove = BlackPlayer;
                    else
                    {
                        NextToMove = WhitePlayer;
                    }

                } while (moveMessage.LastMove == false);
                
                NextToMove.SendMessage(moveMessage); // sent the last move
                
                // Switch white and back players

                NextToMove = WhitePlayer;
                WhitePlayer = BlackPlayer;
                BlackPlayer = NextToMove;
                NextToMove = null;
            }

        }
        catch (EndOfStreamException)
        {
            Console.WriteLine("Someone disconnected! Destroying room....");

            if (GameServer.ActiveRooms.ContainsKey(RoomId))
            {
                GameServer.ActiveRooms.Remove(RoomId);
                DisconnectPlayer(WhitePlayer);
                DisconnectPlayer(BlackPlayer);
            }
        }
        catch(Exception e)
        {
            Console.WriteLine(e);

            if (GameServer.ActiveRooms.ContainsKey(RoomId))
            {
                GameServer.ActiveRooms.Remove(RoomId);
                DisconnectPlayer(WhitePlayer);
                DisconnectPlayer(BlackPlayer);
            }
        }

        return true;
    }

    private void RemovePlayer(string id)
    {
        if (BlackPlayer?.SessionId == id)
        {
            DisconnectPlayer(BlackPlayer);
            BlackPlayer = null;
        }
        else if (WhitePlayer?.SessionId == id)
        {
            DisconnectPlayer(WhitePlayer);
            WhitePlayer = null;
        }

        if (WhitePlayer == BlackPlayer && BlackPlayer == null)
        {
            Console.WriteLine($"Destroying room {RoomId}");
            if (GameServer.ActiveRooms.ContainsKey(RoomId))
                GameServer.ActiveRooms.Remove(RoomId);
        }
    }
    private static void DisconnectPlayer(Player? player)
    {
        try
        {
            player?.Client.Close();
        }
        catch (Exception e)
        {
            // ignored
        }
    }

    public bool TryAddPlayer(string id, TcpClient client, bool preferWhite, out bool isWhite)
    {
        if (!(BlackPlayer?.Client.Connected ?? false))
        {
            BlackPlayer = null;
            RoomFull = false;
        }

        if (!(WhitePlayer?.Client.Connected ?? false))
        {
            WhitePlayer = null;
            RoomFull = false;
        }
        
        if (RoomFull)
        {
            isWhite = false; // This doesn't matter as we are returning false anyway
            return false;
        }

        try
        {

            if (WhitePlayer is not null ^ BlackPlayer is not null) // Fun fact: ^ (XOR) is the same as !=
                RoomFull = true; // As it is about to get full by this player
        
            if (BlackPlayer is null && WhitePlayer is null)
            {
                if (preferWhite)
                {
                    WhitePlayer = new Player
                    {
                        SessionId = id,
                        Stream = client.GetStream(),
                        Client = client
                    };
                    isWhite = true;
                }
                else
                {
                    BlackPlayer = new Player
                    {
                        SessionId = id,
                        Stream = client.GetStream(),
                        Client = client
                    };
                    isWhite = false;
                }

                return true;
            }

            if (WhitePlayer is null)
            {
                WhitePlayer = new Player
                {
                    SessionId = id,
                    Stream = client.GetStream(),
                    Client = client
                };
                isWhite = true;
                return true;
            }

            if (BlackPlayer is null)
            {
                BlackPlayer = new Player
                {
                    SessionId = id,
                    Stream = client.GetStream(),
                    Client = client
                };
                isWhite = false;
                return true;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("An exception occured while adding player {0}", e);
            RemovePlayer(id);
        }

        // The below code shouldn't execute
        Console.WriteLine($"Unexpected code execution in {nameof(MatchRoom)}!");
        isWhite = false;
        return false;
    }
    
}