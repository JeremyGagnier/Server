using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;

namespace PGLoginServer
{
    class LoginHandler
    {
        const char LOGIN_TOKEN = 'l';
        const char REGISTER_TOKEN = 'r';

        private SocketHandler.Controller controller = null;
        private Socket sock;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="socket"></param>
        public LoginHandler(Socket socket)
        {
            sock = socket;
            controller = new SocketHandler.Controller(socket);
            controller.onCloseConnection += OnCloseConnection;
            controller.onReceiveData += GetLoginInfo;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        private void GetLoginInfo(string message)
        {
            controller.onReceiveData -= GetLoginInfo;

            // Protocol:
            // - First character is request type
            // - Second character is game token
            // - From then up to the first comma is the username
            // - From then up to the end is the password
            bool register = (message[0] == REGISTER_TOKEN);
            char game = message[1];
            string username = "";
            string password = "";

            int i = 2;
            while (i < message.Length && message[i] != ',')
            {
                username += message[i];
                ++i;
            }
            if (i == message.Length)
            {
                Console.WriteLine("Improperly formatted login message, didn't separate username and password with a comma:");
                Console.WriteLine(message);
            }
            if (IsUsernameValid(username))
            {
                Console.WriteLine("Invalid username format:");
                Console.WriteLine(username);
            }
            
            ++i;
            while (i < message.Length)
            {
                password += message[i];
                ++i;
            }

            if (register)
            {
                Register(game, username, password);
            }
            else
            {
                Authenticate(game, username, password);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void Authenticate(char game, string username, string password)
        {
            Dictionary<string, object> info;
            try
            {
                using (System.IO.FileStream fIn = System.IO.File.OpenRead("login_info/" + username))
                {
                    byte[] readData = new byte[fIn.Length];
                    fIn.Read(readData, 0, (int)fIn.Length);
                    info = (Dictionary<string, object>)JsonConvert.DeserializeObject(Encoding.Unicode.GetString(readData));
                }

                if (password == (string)info["password"])
                {
                    Login(game, username);
                }
                else
                {
                    // report incorrect password
                }
            }
            catch (System.IO.FileNotFoundException)
            {
                // report incorrect username
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void Login(char game, string username)
        {
            Program.Login(sock, game, username);
            OnCloseConnection(null);
        }

        /// <summary>
        /// 
        /// </summary>
        private void Register(char game, string username, string password)
        {
            Dictionary<string, object> info = new Dictionary<string, object>();
            info["password"] = password;
            byte[] writeData = Encoding.Unicode.GetBytes(JsonConvert.SerializeObject(info));

            using (System.IO.FileStream fOut = System.IO.File.OpenWrite("login_info/" + username))
            {
                fOut.Write(writeData, 0, writeData.Length);
            }
            Login(game, username);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private bool IsUsernameValid(string username)
        {
            if (username.Length < 4)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        private void OnCloseConnection(Exception e)
        {
            controller.IsRunning = false;
            controller = null;
        }
    }
}
