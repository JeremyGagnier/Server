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
    class TurnBasedRPG
    {
        public const bool DEBUG = true;

        public const char GAME_TOKEN = 'r';

        private SocketHandler.Controller controller = null;
        private Socket sock = null;
        private string user = null;
        private Dictionary<string, object> userData = null;
        public Action<Exception> onCloseConnection = null;

        public TurnBasedRPG(Socket socket, string username)
        {
            sock = socket;
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
        }

        private void OnCloseConnection(Exception e)
        {
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
