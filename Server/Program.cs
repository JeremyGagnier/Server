using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace Server
{
    class Program
    {

        static int usersOnline = 0;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            SocketHandler.Server servSocket = new SocketHandler.Server(1134);
            servSocket.onNewConnection += StartNewSession;

            bool running = true;
            servSocket.onCloseConnection += (e) =>
            {
                if (e != null)
                {
                    Console.WriteLine("Server crashed:");
                    Console.WriteLine(e);
                    running = false;
                }
            };

            string message;
            while (running)
            {
                message = Console.ReadLine();
                if (message == "q")
                {
                    running = false;
                    servSocket.IsRunning = false;
                }
                else if (message == "h")
                {
                    Console.Write("h - help\nq - quit (shutdown server)\nlive - how many users are online right now\n");
                }
                else if (message == "live")
                {
                    Console.WriteLine(usersOnline);
                }
            }
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
            usersOnline += 1;
            if (game == Messenger.GAME_TOKEN)
            {
                Messenger tmp = new Messenger(socket, username);
                tmp.onCloseConnection += (e) =>
                {
                    usersOnline -= 1;
                };
            }
            else if (game == TurnBasedRPG.GAME_TOKEN)
            {
                TurnBasedRPG tmp = new TurnBasedRPG(socket, username);
                tmp.onCloseConnection += (e) =>
                {
                    usersOnline -= 1;
                };
            }
        }
    }
}
