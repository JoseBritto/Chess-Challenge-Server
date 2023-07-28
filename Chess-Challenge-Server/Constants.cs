namespace Chess_Challenge_Server;

public class Constants
{
    public const string PROTOCOL_VERSION = "0.1"; // The clients will only connect if using same version of protocol
    public const string SERVER_VERSION = "0.1"; // Just to track for any changelog later
        
        
    public const string SERVER_HOSTNAME = "127.0.0.1"; // Ip to connect to as the server
    public const int SERVER_PORT = 4578; // Open port of the server
}