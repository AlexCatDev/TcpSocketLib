using System;
using System.Net.Sockets;

namespace TcpSocketLib
{
    public class TcpSocketClient
    {

        public class PacketReceivedArgs : EventArgs
        {
            public byte[] Data { get; set; }
            public int Length { get; set; }

            public PacketReceivedArgs(byte[] data) {
                Data = data;
                Length = data.Length;
            }
        }
        public delegate void PacketReceivedHandler(PacketReceivedArgs PacketReceivedArgs);
        public event PacketReceivedHandler PacketRecieved;

        object sendLock = new object();

        Socket socket;

        public string IP { get; private set; }
        public int Port { get; private set; }

        byte[] buffer;

        public TcpSocketClient() {
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.socket.NoDelay = true;
        }

        public void Connect(string IP, int Port) {
            this.IP = IP;
            this.Port = Port;
            socket.Connect(IP, Port);

            Read();
        }

        private void Read() {
            buffer = new byte[4];
            this.socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.Partial, RecieveCallBack, null);
        }

        private void RecieveCallBack(IAsyncResult iar) {
            try {
                if (this.socket.EndReceive(iar) > 1) {
                    buffer = new byte[BitConverter.ToInt32(buffer, 0)];
                    this.socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.Partial, FinalCallBack, null);
                } else {
                    //Idk tbh :\

                    //kys
                }
            }catch(Exception ex) {
                HandleDisconnect(ex);
            }
        }

        private void FinalCallBack(IAsyncResult iar) {
            try {
                this.socket.EndReceive(iar);
                PacketRecieved?.Invoke(new PacketReceivedArgs(buffer));
                Read();
            }catch(Exception ex) {
                HandleDisconnect(ex);
            }
        }

        void HandleDisconnect(Exception ex) {
#if DEBUG
            Console.WriteLine(ex.StackTrace);
#endif
            this.socket.Close();
        }

        public void Disconnect() {
            HandleDisconnect(new Exception("Manual disconnect"));
        }

        public void Send(byte[] data) {
            lock (sendLock) {
                socket.Send(BitConverter.GetBytes(data.Length));
                socket.Send(data);
            }
        }
    }

}
