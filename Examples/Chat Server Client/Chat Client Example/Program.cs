using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TcpSocketLib;

namespace Chat_Client_Example
{
    class Program
    {
        static void Main(string[] args) {
            TcpSocketClient client = new TcpSocketClient();
            client.PacketRecieved += (e) => {
                using (PacketReader pr = new PacketReader(e.Data)) {
                    Headers Header = (Headers)pr.ReadByte();
                    switch (Header) {
                        case Headers.Login:
                            string user = pr.ReadString();
                            int id = pr.ReadInt32();
                            Console.Title = $"User {user} logged in ID {id} ";
                            break;
                        case Headers.Register:
                            break;
                        case Headers.Chat:
                            int iduser = pr.ReadInt32();
                            string chat = pr.ReadString();
                            Console.Title = $"User {iduser} said {chat} ";
                            break;
                        default:
                            break;
                    }
                }
            };
            client.Connect("127.0.0.1", 9090);
            Thread.Sleep(1000);
            Random r = new Random();
            using (PacketWriter pw = new PacketWriter()) {
                pw.Write((byte)Headers.Login);
                pw.Write("TestUser" + r.Next());
                client.Send(pw.Data);
            }
            while (true) {
                string input = Console.ReadLine();
                using(PacketWriter pw = new PacketWriter()) {
                    pw.Write((byte)Headers.Chat);
                    pw.Write(input);
                    client.Send(pw.Data);
                }
            }
        }
    }
}

public enum Headers : byte
{
    Login,
    Register,
    Chat
}
