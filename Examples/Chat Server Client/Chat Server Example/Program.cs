using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TcpSocketLib;

namespace Chat_Server_Example
{
    class Program
    {
        static TcpSocketListener listener;
        static void Main(string[] args) {
            UUID.InitializeUUIDS();
            listener = new TcpSocketListener(9090);
            listener.ClientConnected += (e) => {
                e.TcpSocket.UserState = new User();
            };
            listener.ClientDisconnected += (e) => {

            };
            listener.FloodDetected += (e) => {

            };
            listener.PacketRecieved += (e) => {
                using(PacketReader pr = new PacketReader(e.Data)) {
                    Headers Header = (Headers)pr.ReadByte();
                    switch (Header) {
                        case Headers.Login:
                            string username = pr.ReadString();
                            int id = UUID.getRandomUUID();
                            (e.TcpSocket.UserState as User).Username = username;
                            (e.TcpSocket.UserState as User).Authorized = true;
                            (e.TcpSocket.UserState as User).ID = id;
                            Console.WriteLine("Authorized user {0} generated ID {1}", username, id);
                            //Broadcast someone logged in
                            using (PacketWriter pw = new PacketWriter()) {
                                pw.Write((byte)Headers.Login);
                                pw.Write((e.TcpSocket.UserState as User).ID);
                                pw.Write(username);
                                pw.Write(id);
                                Broadcast(pw.Data, e.TcpSocket);
                            }
                            
                            break;
                        case Headers.Register:
                            break;
                        case Headers.Chat:
                            if((e.TcpSocket.UserState as User).Authorized) {
                                string chat = pr.ReadString();
                                //Broadcast chat packet
                                using (PacketWriter pw = new PacketWriter()) {
                                    pw.Write((byte)Headers.Chat);
                                    pw.Write((e.TcpSocket.UserState as User).ID);
                                    pw.Write(chat);
                                    Broadcast(pw.Data, e.TcpSocket);
                                }
                            } else {
                                Console.WriteLine("User wasn't authorized to chat.");
                            }
                            break;
                        default:
                            break;
                    }
                }
            };
            listener.Start(25);
            Console.ReadLine();
        }
        public static void Broadcast(byte[] data, TcpSocketListener.TcpSocket Exclusion = null) {
            List<TcpSocketListener.TcpSocket> tmp = new List<TcpSocketListener.TcpSocket>(listener.ConnectedClients);
            if (Exclusion == null) {
                foreach (var client in tmp) {
                    client.Send(data);
                }
            }
            else {
                foreach (var client in tmp) {
                    if(client!=Exclusion)
                    client.Send(data);
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

public class User
{
    public int ID { get; set; }
    public bool Authorized { get; set; }
    public string Username { get; set; }
}
