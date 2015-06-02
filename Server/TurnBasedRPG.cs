using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;

namespace Server
{
    class Game
    {
        private static int _gameID = 0;
        static int gameID
        {
            get
            {
                return _gameID++;
            }
        }

        public List<string> players = new List<string>();
        public Action<string> sendMessage = null;
        public Dictionary<string, object> gameData = new Dictionary<string, object>();
    }

    public class TurnBasedRPG
    {
        public const bool DEBUG = true;

        public const char GAME_TOKEN = 'r';
        public const int PLAYER_LIMIT = 4;

        // These tokens are used for the creation and management of new games.
        public const char NEW_LOBBY = 'n';
        public const char JOIN_LOBBY = 'j';
        public const char START_GAME = 's';
        public const char KICK_PLAYER = 'k';
        public const char LEAVE_LOBBY = 'l';

        // This token is used when sending important game information such as
        // which ability you've chosen to use next or what item you bought.
        public const char GAME_INFO = 'g';  
        
        // Indexed by unique game ID
        public static Dictionary<string, Game> games = new Dictionary<string, Game>();

        private SocketHandler.Controller controller = null;
        private Socket sock = null;
        private string user = null;
        private string game = null;
        private Dictionary<string, object> userData = null;
        public Action<Exception> onCloseConnection = null;

        public TurnBasedRPG(Socket socket, string username)
        {
            sock = socket;
            user = username;
            controller = new SocketHandler.Controller(socket);
            controller.onReceiveData += ParseMessage;
            controller.onCloseConnection += OnCloseConnection;

            try
            {
                using (System.IO.FileStream fIn = System.IO.File.OpenRead(GAME_TOKEN + "/" + username))
                {
                    byte[] readData = new byte[fIn.Length];
                    fIn.Read(readData, 0, (int)fIn.Length);
                    userData = (Dictionary<string, object>)JsonConvert.DeserializeObject(Encoding.Unicode.GetString(readData));
                }
            }
            catch (System.IO.FileNotFoundException)
            {
                using (System.IO.FileStream fIn = System.IO.File.OpenWrite(GAME_TOKEN + "/" + username))
                {
                }
            }
        }

        private void ParseMessage(string message)
        {
            string messageRegex = "[" + NEW_LOBBY + 
                                        JOIN_LOBBY + 
                                        START_GAME + 
                                        KICK_PLAYER + 
                                        LEAVE_LOBBY + 
                                        GAME_INFO + "]" + ".*";

            string match = Regex.Match(message, messageRegex).Value;
            if (string.IsNullOrEmpty(match))
            {
                Debug("Improperly formatted message:\n" + message);
                return;
            }

            switch(match[0])
            {
                case NEW_LOBBY:
                    ParseNewLobby(match.Substring(1));
                    break;
                case JOIN_LOBBY:
                    ParseJoinLobby(match.Substring(1));
                    break;
                case START_GAME:
                    ParseStartGame(match.Substring(1));
                    break;
                case KICK_PLAYER:
                    ParseKickPlayer(match.Substring(1));
                    break;
                case LEAVE_LOBBY:
                    ParseLeaveLobby(match.Substring(1));
                    break;
                case GAME_INFO:
                    ParseGameInfo(match.Substring(1));
                    break;
            }
        }

        private void ParseNewLobby(string message)
        {
        }

        private void ParseJoinLobby(string message)
        {
            if (game != null)
            {
                Debug("Player " + user + " tried to join a game before leaving the one they were in");
                return;
            }
            if (games[message] == null)
            {
                Debug("Player " + user + " tried to join a game that doesn't exist");
                return;
            }
            if (games[message].players.Count >= PLAYER_LIMIT)
            {
                Debug("Player " + user + " tried to join a game that is full");
                return;
            }
            game = message;
            games[message].players.Add(user);
            games[message].sendMessage += SendMessage;
        }

        private void ParseStartGame(string message)
        {
        }

        private void ParseKickPlayer(string message)
        {
        }

        private void ParseLeaveLobby(string message)
        {
        }

        private void ParseGameInfo(string message)
        {
        }

        private void SendMessage(string message)
        {
            controller.SendData(message);
        }

        private void OnCloseConnection(Exception e)
        {
            controller.IsRunning = false;
            controller = null;

            try
            {
                sock.Shutdown(SocketShutdown.Both);
                sock.Close();
            }
            catch (Exception)
            {
            }

            if (onCloseConnection != null)
            {
                onCloseConnection(e);
            }
        }

        private void Debug(string s)
        {
            if (DEBUG)
            {
                Console.WriteLine("TURN_BASED_RPG: " + s);
            }
        }
    }
}
