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
        public Dictionary<string, string> gameData = new Dictionary<string, string>();
        public Dictionary<string, Action<string>> dataQueue = new Dictionary<string, Action<string>>();
    }



    public class TurnBasedRPG
    {
        public const bool DEBUG = true;

        public const char GAME_TOKEN = 'r';
        public const int PLAYER_LIMIT = 4;

        /*
         * The following is protocol so that messages can be interpreted by both ends.
         */

        // This token is used to signify a new message.
        public const char NEW_MESSAGE =     (char)0;

        // This token is used to separate important pieces of data
        public const char SEPARATOR =       (char)1;

        // These tokens are used for the creation and management of new games.
        public const char NEW_LOBBY =       (char)2;
        public const char JOIN_LOBBY =      (char)3;
        public const char START_GAME =      (char)4;
        public const char KICK_PLAYER =     (char)5;
        public const char LEAVE_LOBBY =     (char)6;
        public const char REMOVE_LOBBY =    (char)7;
        public const char LOBBY_INFO =      (char)8;
        public const char HOST_PROMOTE =    (char)9;

        // This token is used when sending important game information such as
        // which ability you've chosen to use next or what item you bought.
        public const char GAME_INFO =       (char)10;
        
        // Storing and retrieving relevant game information.
        // If you ask for information that doesn't yet exist you will be queued up to
        // get the information once it's created.
        public const char GAME_DATA_GET =   (char)11;
        public const char GAME_DATA_SET =   (char)12;

        /*
         * The following are tokens for error messages.
         */
        public const char ERROR_NOT_IN_GAME =       (char)13;
        public const char ERROR_ALREADY_IN_GAME =   (char)14;
        public const char ERROR_GAME_EXISTS =       (char)15;
        public const char ERROR_GAME_DOESNT_EXIST = (char)16;
        public const char ERROR_GAME_FULL =         (char)17;
        public const char ERROR_NOT_HOST =          (char)18;
        public const char ERROR_GAME_STARTED =      (char)19;
        public const char ERROR_SELF_KICK =         (char)20;
        public const char ERROR_PLAYER_NOT_FOUND =  (char)21;
        


        // Indexed by unique game ID
        private static Dictionary<string, Game> games = new Dictionary<string, Game>();
        private static Dictionary<string, Action<string>> players = new Dictionary<string, Action<string>>();
        private static Action<string> waitingRoom = null;

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
            waitingRoom += SendMessage;
            players.Add(user, SendMessage);
            LobbyInfo();

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
                SendMessage(ERROR_ALREADY_IN_GAME + "");
                return;
            }
            if (games.ContainsKey(message))
            {
                Debug("Player " + user + " tried to create a new game with the same name as an existing game.");
                SendMessage(ERROR_GAME_EXISTS + "");
                return;
            }
            waitingRoom(NEW_LOBBY + message);   // Confirmation when you receive your own new lobby message
            waitingRoom -= SendMessage;
            game = message;
            games[game] = new Game();
            games[game].players.Add(user);
        }

        private void ParseJoinLobby(string message)
        {
            if (game != null)
            {
                Debug("Player " + user + " tried to join a game before leaving the one they were in");
                SendMessage(ERROR_ALREADY_IN_GAME + "");
                return;
            }
            if (!games.ContainsKey(message))
            {
                Debug("Player " + user + " tried to join a game that doesn't exist");
                SendMessage(ERROR_GAME_DOESNT_EXIST + "");
                return;
            }
            if (games[message].players.Count >= PLAYER_LIMIT)
            {
                Debug("Player " + user + " tried to join a game that is full");
                SendMessage(ERROR_GAME_FULL + "");
                return;
            }
            game = message;
            games[game].players.Add(user);
            games[game].sendMessage += SendMessage;
            games[game].sendMessage(JOIN_LOBBY + user); // Confirmation when you receive your own join message
            waitingRoom -= SendMessage;
        }

        private void ParseStartGame(string message)
        {
            if (game == null)
            {
                Debug("Player " + user + " tried to start a game that they weren't even in");
                SendMessage(ERROR_NOT_IN_GAME + "");
                return;
            }
            if (games[game].players[0] != user)
            {
                Debug("Player " + user + " tried to start a game that they weren't the host of");
                SendMessage(ERROR_NOT_HOST + "");
                return;
            }
            if (games[game].hasStarted == true)
            {
                Debug("Player " + user + " tried to start a game that has already begun");
                SendMessage(ERROR_GAME_STARTED + "");
                return;
            }
            games[game].hasStarted = true;
            games[game].sendMessage(START_GAME + "");   // Confirmation when you receive your own start message
        }

        private void ParseKickPlayer(string message)
        {
            if (game == null)
            {
                Debug("Player " + user + " tried to kick a player when they weren't in a game");
                SendMessage(ERROR_NOT_IN_GAME + "");
                return;
            }
            if (games[game].players[0] != user)
            {
                Debug("Player " + user + " tried to kick a player when they weren't the host");
                SendMessage(ERROR_NOT_HOST + "");
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
                SendMessage(ERROR_SELF_KICK + "");
                return;
            }
            else if (index == 0)
            {
                Debug("Player " + user + " tried to kick a player that wasn't in the game");
                SendMessage(ERROR_PLAYER_NOT_FOUND + "");
                return;
            }
            games[game].sendMessage(KICK_PLAYER + message); // Confirmation when you receive your own kick player message.
            games[game].players.RemoveAt(index);
        }

        private void ParseLeaveLobby(string message)
        {
            if (game == null)
            {
                Debug("Player " + user + " tried to leave a game when they weren't in one");
                SendMessage(ERROR_NOT_IN_GAME + "");
                return;
            }

            // Promote a new host if the player that left was the previous host.
            if (games[game].players[0] == user && games[game].players.Count > 1)
            {
                players[games[game].players[1]](HOST_PROMOTE + "");
            }

            games[game].players.Remove(user);
            if (games[game].players.Count == 0)
            {
                waitingRoom(REMOVE_LOBBY + game);    // Tell those waiting in the lobby that this game is dead
                games.Remove(game);
            }
            else
            {
                games[game].sendMessage(LEAVE_LOBBY + user);
            }
            game = null;
            waitingRoom += SendMessage;
            LobbyInfo();
        }

        private void ParseGameInfo(string message)
        {
            if (game == null)
            {
                Debug("Player " + user + " tried to send game info when they weren't in a game");
                SendMessage(ERROR_NOT_IN_GAME + "");
                return;
            }
            // Relay the info
            games[game].sendMessage(message);
        }

        private void ParseDataGet(string message)
        {
            if (game == null)
            {
                Debug("Player " + user + " tried to get game info when they weren't in a game");
                SendMessage(ERROR_NOT_IN_GAME + "");
                return;
            }
            if (games[game].gameData.ContainsKey(message))
            {
                SendMessage(GAME_DATA_GET + games[game].gameData[message]);
            }
            else
            {
                // If the data doesn't exist yet put the player on a queue to wait for it to exist.
                if (games[game].dataQueue.ContainsKey(message))
                {
                    games[game].dataQueue[message] += SendMessage;
                }
                else
                {
                    games[game].dataQueue.Add(message, SendMessage);
                }
            }
        }

        private void ParseDataSet(string message)
        {
            if (game == null)
            {
                Debug("Player " + user + " tried to get game info when they weren't in a game");
                SendMessage(ERROR_NOT_IN_GAME + "");
                return;
            }
            string key = "";
            string data = "";
            for (int i = 0; i < message.Length; ++i)
            {
                if (message[i] != SEPARATOR)
                {
                    key += message[i];
                }
                else
                {
                    data = message.Substring(i + 1);
                    break;
                }
            }
            games[game].gameData[key] = data;
            if (games[game].dataQueue.ContainsKey(key))
            {
                games[game].dataQueue[key](GAME_DATA_GET + data);
            }
        }

        private void LobbyInfo()
        {
            foreach (string gameName in games.Keys)
            {
                SendMessage(LOBBY_INFO + gameName + SEPARATOR + games[gameName].players.Count.ToString());
            }
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
