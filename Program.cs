using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace PGLoginServer
{
    class Program
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            SocketHandler.Server servSocket = new SocketHandler.Server(1134);
            servSocket.onNewConnection += StartNewSession;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="socket"></param>
        private static void StartNewSession(Socket socket)
        {
            new LoginHandler(socket);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="game"></param>
        /// <param name="username"></param>
        public static void Login(Socket socket, char game, string username)
        {
            if (game == Messenger.GAME_TOKEN)
            {
                new Messenger(socket, username);
            }
        }
    }
}
