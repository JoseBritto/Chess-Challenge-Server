using System.Net.Sockets;

namespace Chess_Challenge_Server;

public class MatchRoom
{
    public readonly string RoomId;

    public long PlayerMoveTimeMillis { get; private set; }
    public Player? WhitePlayer { get; private set; }
    public Player? BlackPlayer { get; private set; }

    public Player? NextToMove;
    public Player? NextNotToMove;

    public bool RoomFull => IsWhitePlayerLive() && IsBlackPlayerLive();
    
    public bool InGame { get; private set; }
    
    public MatchRoom(string roomId, long playerMoveTimeMillis = -1)
    {
        RoomId = roomId;
        PlayerMoveTimeMillis = playerMoveTimeMillis;
    }

    public bool TrySetPlayerMoveTime(long millis)
    {
        if (InGame)
            return false;

        PlayerMoveTimeMillis = millis;
        return true;
    }
    public void TryStartNewGame(string fen)
    {
        if (WhitePlayer is null || BlackPlayer is null || IsWhitePlayerLive() == false || IsBlackPlayerLive() == false)
            return;

        try
        {
            InGame = true;
            
            WhitePlayer.SendMessage(new GetReady
            {
                ClockTimeMillis = PlayerMoveTimeMillis,
                GameStartFen = fen,
                IsWhite = true
            });
            
            BlackPlayer.SendMessage(new GetReady
            {
                ClockTimeMillis = PlayerMoveTimeMillis,
                GameStartFen = fen,
                IsWhite = false
            });

            var readyMsg = WhitePlayer.GetNextMessage();
            if (readyMsg is not IsReady)
            {
                RemovePlayer(WhitePlayer.SessionId, 
                    readyMsg is not ShutdownMsg && readyMsg is not null, 
                    reason: "Unresponsive client");
                return;
            }
            
            readyMsg = BlackPlayer.GetNextMessage();
            if (readyMsg is not IsReady)
            {
                RemovePlayer(BlackPlayer.SessionId, 
                    readyMsg is not ShutdownMsg && readyMsg is not null,
                    "Unresponsive client");
                return;
            }

            //Both Players are ready now
            
            WhitePlayer.SendMessage(new GameStart());
            BlackPlayer.SendMessage(new GameStart());

            WhitePlayer.StartClock();
            NextToMove = WhitePlayer;
            NextNotToMove = BlackPlayer;
            //Game loop
            
            while (true) // Run until an exception occurs or game end
            {
                while (NextToMove.HasNewMessage() == false)
                {
                    if(NextToMove.TimeElapsedMillis >= PlayerMoveTimeMillis)
                    {
                        NextToMove.SendMessage(new TimeOut
                        {
                            ItWasYou = true
                        });
                        NextNotToMove.SendMessage(new TimeOut
                        {
                            ItWasYou = false
                        });
                        goto outside;
                    }
                }

                var msg = NextToMove.GetNextMessage();

                switch (msg)
                {
                    case GameOver:
                        goto outside;
                    break;
                    
                    case ShutdownMsg:
                        RemovePlayer(NextNotToMove.SessionId, 
                            true,"Opponent connection lost");
                        RemovePlayer(NextToMove.SessionId, false);
                    return;

                    case MoveMessage moveMessage:
                        NextToMove.PauseClock();
                        NextNotToMove.SendMessage(new MoveMessage
                        {
                            MoveName = moveMessage.MoveName,
                            OpponentClockElapsed = NextToMove.TimeElapsedMillis,
                            YourClockElapsed = NextNotToMove.TimeElapsedMillis
                        });
                        NextNotToMove.StartClock();
                        //Swap players
                        (NextToMove, NextNotToMove) = (NextNotToMove, NextToMove);
                        
                        break;
                }
            }
            outside:
            RemovePlayer(WhitePlayer.SessionId, reason:"Game Over");
            RemovePlayer(BlackPlayer.SessionId, reason:"Game Over");
            return;

        }
        catch (Exception e)
        {
            Console.WriteLine(e);

            Console.WriteLine($"Destroying {RoomId}...");

            if (GameServer.ActiveRooms.ContainsKey(RoomId))
            {
                try
                {
                    RemovePlayer(WhitePlayer?.SessionId ?? "0", reason: $"Error {e.GetType()}");
                    RemovePlayer(BlackPlayer?.SessionId ?? "0", reason: $"Error {e.GetType()}");
                }
                catch
                {
                    try
                    {
                        WhitePlayer?.Dispose();
                        BlackPlayer?.Dispose();
                    }
                    catch
                    {//ignored
                    }

                    if (GameServer.ActiveRooms.ContainsKey(RoomId))
                        GameServer.ActiveRooms.Remove(RoomId);
                }
            }
        }
        finally
        {
            InGame = false;
        }

    }

    private bool IsWhitePlayerLive()
    {
        try
        {
            var stream = WhitePlayer?.Client.GetStream();
            
            return stream is not null && (WhitePlayer?.Client.Connected ?? false);
        }
        catch
        {
            return false;
        }
    }

    private bool IsBlackPlayerLive()
    {
        try
        {
            var stream = BlackPlayer?.Client.GetStream();
            
            return stream is not null && (BlackPlayer?.Client.Connected ?? false);
        }
        catch
        {
            return false;
        }
    }

    private void RemovePlayer(string id, bool sendShutdownMsg = true, string reason = "Unknown")
    {
        if (BlackPlayer?.SessionId == id)
        {
            Console.WriteLine("Removing black");
            try
            {
                if(sendShutdownMsg)
                    BlackPlayer.SendMessage(new ShutdownMsg
                    {
                        Reason = reason
                    });
            }
            catch
            {
                //ignored
            }

            BlackPlayer.Dispose();
            BlackPlayer = null;

            if (WhitePlayer is not null && IsWhitePlayerLive())
            {
                WhitePlayer.SendMessage(new PlayerLeft());
                var msg = WhitePlayer.GetNextMessage();
                if(msg is not Ack)
                    RemovePlayer(WhitePlayer.SessionId, reason: msg is null ? "Connection lost" : "Unknown packet. Expected Ack");
            }
        }
        else if (WhitePlayer?.SessionId == id)
        {
            Console.WriteLine("Removing white");
            try
            {
                if(sendShutdownMsg)
                    WhitePlayer.SendMessage(new ShutdownMsg
                    {
                        Reason = reason
                    });
            }
            catch
            {
                //ignored
            }

            WhitePlayer.Dispose();
            WhitePlayer = null;

            if (BlackPlayer is not null && IsBlackPlayerLive())
            {
                BlackPlayer.SendMessage(new PlayerLeft());
                var msg = BlackPlayer.GetNextMessage();
                if(msg is not Ack)
                    RemovePlayer(BlackPlayer.SessionId, reason: msg is null ? "Connection lost" : "Unknown packet. Expected Ack");
            }
        }

        if (WhitePlayer == BlackPlayer && BlackPlayer == null)
        {
            Console.WriteLine($"Destroying room {RoomId}");
            if (GameServer.ActiveRooms.ContainsKey(RoomId))
                GameServer.ActiveRooms.Remove(RoomId);
        }
    }

    public bool TryAddPlayer(string id, TcpClient client, string username)
    {
        if (InGame)
            return false;

        if (RoomFull)
        {
            return false;
        }

        try
        {
            var player = new Player(client, username, id);
            
            //TODO: Handle exceptions while sending messages as an error on only that particular client and kick him only
            
            if (WhitePlayer is null)
            {
                WhitePlayer = player;

                //StartPingThread(RoomId, player);
                
                if (BlackPlayer is null || !IsBlackPlayerLive()) 
                    return true;
                
                BlackPlayer.SendMessage(new PlayerJoined
                {
                    UserName = username
                });

                var response = BlackPlayer.GetNextMessage();

                if (response is not Ack)
                {
                    if (response is Reject)
                    {
                        // Black doesn't like white it seems. So don't let him in
                        RemovePlayer(WhitePlayer.SessionId);
                        return false;
                    }
                    // We received null or an unknown packet from black. Kick the black player
                    RemovePlayer(BlackPlayer.SessionId);
                    return true; // We added the new player successfully but removed the old guy and now we have only 1 player
                }
                    
                // We got an ack. Everything good! Continue on...
                    
                // Tell white  player about black player
                WhitePlayer.SendMessage(new PlayerJoined
                {
                    UserName = BlackPlayer!.UserName
                });

                response = WhitePlayer.GetNextMessage();

                if (response is not Ack)
                {
                    // Whatever it is we kick white because he is new and we don't want any trouble
                    RemovePlayer(WhitePlayer.SessionId);
                    return false;
                }
                    
                // Again we got an ack. All good!

                return true;
            }

            if (BlackPlayer is null)
            {
                BlackPlayer = player;
             //   StartPingThread(RoomId, player);

                if (WhitePlayer is null || !IsWhitePlayerLive()) 
                    return true;
                
                WhitePlayer.SendMessage(new PlayerJoined
                {
                    UserName = username
                });

                var response = WhitePlayer.GetNextMessage();

                if (response is not Ack)
                {
                    if (response is Reject)
                    {
                        RemovePlayer(BlackPlayer.SessionId);
                        return false;
                    }
                    // Unexpected message
                    RemovePlayer(WhitePlayer.SessionId);
                    return true; // We return cuz now we have only one player and we successfully added new guy
                }
                    
                // We got an ack. Everything good! Continue on...
                    
                // Tell the new guy about the other player
                BlackPlayer.SendMessage(new PlayerJoined
                {
                    UserName = WhitePlayer!.UserName
                });

                response = BlackPlayer.GetNextMessage();

                if (response is not Ack)
                {
                    //Remove new guy
                    RemovePlayer(BlackPlayer.SessionId);
                    return false;
                }
                    
                // All went well

                return true;
            }

        }
        catch (Exception e)
        {
            Console.WriteLine("An exception occured while adding player {0}", e);
            RemovePlayer(id);
            return false;
        }

        // The below code shouldn't execute
        Console.WriteLine($"Unexpected code execution in {nameof(MatchRoom)}!");
        return false;
    }

    private void StartPingThread(string id, Player player)
    {
        Task.Run(() =>
        {
            while (GameServer.ActiveRooms.ContainsKey(id))
            {
                try
                {
                    Task.Delay(2000).Wait();
                    Console.WriteLine("Pinged");
                    player?.SendMessage(new PingMsg());
                }
                catch
                {
                    try
                    {
                        RemovePlayer(player?.SessionId ?? "0", false);
                    }
                    catch
                    {
                        //ignored
                    }
                }
            }
        });
    }
}