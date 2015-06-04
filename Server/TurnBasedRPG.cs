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

        public bool hasStarted = false;
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
        
        // Storing and retrieving relevant game information.
        // If you ask for information that doesn't yet exist you will be queued up to
        // get the information once it's created.
        public const char GAME_DATA_GET = 'd';
        public const char GAME_DATA_SET = 'c';
        
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
            if (game != null)
            {
                Debug("Player " + user + " tried to create a new game before leaving the one they were in");
                // TODO: Send error message
                return;
            }
            if (games.ContainsKey(message))
            {
                Debug("Player " + user + " tried to create a new game with the same name as an existing game.");
                // TODO: Send error message
                return;
            }
            // TODO: tell players looking at open lobbies that a new one was made
            games[message] = new Game();
            games[message].players.Add(user);
        }

        private void ParseJoinLobby(string message)
        {
            if (game != null)
            {
                Debug("Player " + user + " tried to join a game before leaving the one they were in");
                // TODO: Send error message
                return;
            }
            if (!games.ContainsKey(message))
            {
                Debug("Player " + user + " tried to join a game that doesn't exist");
                // TODO: Send error message
                return;
            }
            if (games[message].players.Count >= PLAYER_LIMIT)
            {
                Debug("Player " + user + " tried to join a game that is full");
                // TODO: Send error message
                return;
            }
            // TODO: tell other players that this player has joined
            game = message;
            games[message].players.Add(user);
            games[message].sendMessage += SendMessage;
        }

        private void ParseStartGame(string message)
        {
            if (game == null)
            {
                Debug("Player " + user + " tried to start a game that they weren't even in");
                // TODO: Send error message
                return;
            }
            if (games[game].players[0] != user)
            {
                Debug("Player " + user + " tried to start a game that they weren't the host of");
                // TODO: Send error message
                return;
            }
            if (games[game].hasStarted == true)
            {
                Debug("Player " + user + " tried to start a game that has already begun");
                // TODO: Send error message
                return;
            }
            // TODO: tell players the game has started
            games[game].hasStarted = true;
            games[game].sendMessage(START_GAME + "");
        }

        private void ParseKickPlayer(string message)
        {
            if (game == null)
            {
                Debug("Player " + user + " tried to kick a player when they weren't in a game");
                // TODO: Send error message
                return;
            }
            if (games[game].players[0] != user)
            {
                Debug("Player " + user + " tried to kick a player when they weren't the host");
                // TODO: Send error message
                return;
            }
            int index = 0;
            for (int i = 1; i < games[game].players.Count; ++i)
            {
                if (games[game].players[i] == message)
                {
                    index = i;
                    break;
                }
            }
            if (message == user)
            {
                Debug("Player " + user + " tried to kick themselves from the game");
                // TODO: Send error message
                return;
            }
            else if (index == 0)
            {
                Debug("Player " + user + " tried to kick a player that wasn't in the game");
                // TODO: Send error message
                return;
            }
            // TODO: tell other players that this player has been removed
            games[game].players.RemoveAt(index);
        }

        private void ParseLeaveLobby(string message)
        {
            if (game == null)
            {
                Debug("Player " + user + " tried to leave a game when they weren't in one");
                // TODO: Send error message
                return;
            }
            // TODO: Do special things if the user was the host
            // TODO: tell other players that this player left
            games[game].players.Remove(user);
            if (games[game].players.Count == 0)
            {
                // TODO: tell players looking at open lobbies that this game is dead
                games.Remove(game);
            }
            game = null;
        }

        private void ParseGameInfo(string message)
        {
            if (game == null)
            {
                Debug("Player " + user + " tried to send game info when they weren't in a game");
                // TODO: Send error message
                return;
            }
            // Relay the info
            games[game].sendMessage(message);
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
