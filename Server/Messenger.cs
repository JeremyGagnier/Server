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
    class Messenger
    {
        public const bool DEBUG = true;

        public const char GAME_TOKEN = 'm';

        private const char GLOBAL_TOKEN = 'g';
        private const char PRIVATE_TOKEN = 'p';

        private SocketHandler.Controller controller = null;
        private Socket sock = null;
        private string user = null;
        private Dictionary<string, object> userData = null;
        public Action<Exception> onCloseConnection = null;

        public static Action<string> onGlobalMessage = null;
        public static Dictionary<string, Action<string>> users = new Dictionary<string, Action<string>>();

        /// <summary>
        /// Basic messenger program for sending messages to other connected clients.
        /// This is a good tool for testing the server with.
        /// </summary>
        /// <param name="socket">Socket of newly connected client.</param>
        /// <param name="username">Unique username of the client.</param>
        public Messenger(Socket socket, string username)
        {
            sock = socket;
            controller = new SocketHandler.Controller(socket);
            controller.onReceiveData += ParseMessage;
            controller.onCloseConnection += OnCloseConnection;
            onGlobalMessage += SendMessage;
            users[username] = SendMessage;

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

        /// <summary>
        /// Applies general parsing and routes the message to the correct parse function.
        /// </summary>
        /// <param name="message">The message recieved from the connected client.</param>
        private void ParseMessage(string message)
        {

            string messageRegex = "[" + GLOBAL_TOKEN + PRIVATE_TOKEN + "]" + ".*";

            string match = Regex.Match(message, messageRegex).Value;
            if (string.IsNullOrEmpty(match))
            {
                Debug("Improperly formatted message:\n" + message);
                return;
            }

            // First character represents the type of message.
            if (match[0] == GLOBAL_TOKEN)
            {
                ParseGlobal(match.Substring(1));
            }
            else if (match[0] == PRIVATE_TOKEN)
            {
                ParsePrivate(match.Substring(1));
            }
        }

        /// <summary>
        /// Parses private messages. These messages are only sent to one client.
        /// </summary>
        /// <param name="message">The message recieved from the connected client.</param>
        private void ParsePrivate(string message)
        {
            // Parse the incoming message:
            // Up until the first comma represents the username to send.
            int i = 0;
            string username = "";
            while (i < message.Length && message[i] != ',')
            {
                username += message[i];
                ++i;
            }

            message = PRIVATE_TOKEN + user + ',' + message;

            // Send the message to the specified user.
            if (users.ContainsKey(username))
            {
                users[username](message);
            }
            else
            {
                // Send message that the user is not online,
                // and/or store for sending when that user logs in.
            }
        }

        /// <summary>
        /// Parse global messages. These messages are sent to all connected clients.
        /// </summary>
        /// <param name="message">The message recieved from the connected client.</param>
        private void ParseGlobal(string message)
        {
            // Preprocess message:
            // 1st character specifies message type.
            // From then up to first comma is the name of the user that sent the message.
            // From then up to the end is the message.
            message = GLOBAL_TOKEN + user + ',' + message;
            
            // Prevent sending yourself the message.
            // However sometimes it might be useful to receive your own message back.
            //onGlobalMessage -= SendMessage;
            if (onGlobalMessage != null)
            {
                onGlobalMessage(message);
            }
            //onGlobalMessage += SendMessage;
        }

        /// <summary>
        /// Wrapper for controller.SendData. This function is used to send clients messages.
        /// </summary>
        /// <param name="message">The message to be sent to a connected client.</param>
        private void SendMessage(string message)
        {
            controller.SendData(message);
        }

        /// <summary>
        /// Called to terminate the messenger`s connection with a client.
        /// </summary>
        /// <param name="e">Cause of connection loss.</param>
        private void OnCloseConnection(Exception e)
        {
            controller.IsRunning = false;
            controller = null;
            onGlobalMessage -= SendMessage;
            users.Remove(user);

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
                Console.WriteLine("MESSENGER: " + s);
            }
        }
    }
}
