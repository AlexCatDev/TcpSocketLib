using System;
using System.Net.Sockets;

namespace TcpSocketLib
{
    public class TcpSocketClient
    {

        private const int SIZE_PAYLOAD_LENGTH = sizeof(int);

        public delegate void PacketReceivedHandler(PacketReceivedArgs PacketReceivedArgs);
        public event PacketReceivedHandler PacketRecieved;

        public delegate void ReceiveProgressChangedHandler(int received, int bytesToReceive);
        public event ReceiveProgressChangedHandler ReceiveProgressChanged;

        object sendLock = new object();
        byte[] buffer;

        int totalRead = 0;

        Socket socket;

        public string IP { get; private set; }
        public int Port { get; private set; }

        public TcpSocketClient() {
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.socket.NoDelay = true;
        }

        public void Connect(string IP, int Port) {
            this.IP = IP;
            this.Port = Port;
            socket.Connect(IP, Port);
            AllocateBuffer(SIZE_PAYLOAD_LENGTH);
            ReadSize();
        }


        private void AllocateBuffer(int byteCount) {
            buffer = new byte[byteCount];
        }

        private void ReadSize() {
            //The first 4 bytes of the stream contains the size as int32
            this.socket.BeginReceive(buffer, totalRead, buffer.Length - totalRead, SocketFlags.None, ReceiveLengthCallBack, null);
        }

        private void ReceiveLengthCallBack(IAsyncResult iar) {
            try {

                int read;
                if ((read = this.socket.EndReceive(iar)) <= 0)
                    HandleDisconnect(new Exception("Read no bytes"));
                else {

                    totalRead += read;

                    if (totalRead < buffer.Length) {
                        ReadSize();
                    } else {

                        int dataSize = BitConverter.ToInt32(buffer, 0);

                        //Check if dataSize is bigger than 0
                        if (dataSize > 0) {

                            //Allocate a buffer with the size
                            AllocateBuffer(dataSize);
                            totalRead = 0;
                            ReadPayload();
                        } else {
                            totalRead = 0;
                            ReadSize();
                            PacketRecieved?.Invoke(new PacketReceivedArgs(new byte[0]));
                        }
                        }
                    }
            } catch (Exception ex) {
                HandleDisconnect(ex);
            }

        }

        private void ReadPayload() {
            this.socket.BeginReceive(buffer, totalRead, buffer.Length - totalRead, SocketFlags.None, ReceivePayloadCallBack, null);
        }

        private void ReceivePayloadCallBack(IAsyncResult iar) {
            try {
                int read;
                if ((read = this.socket.EndReceive(iar)) <= 0)
                    HandleDisconnect(new Exception("Read no bytes"));

                totalRead += read;
                //Read progress?
                ReceiveProgressChanged?.Invoke(totalRead, buffer.Length);

                if (totalRead < buffer.Length)
                    ReadPayload();
                else {
                    PacketRecieved?.Invoke(new PacketReceivedArgs(buffer));
                    totalRead = 0;
                    AllocateBuffer(SIZE_PAYLOAD_LENGTH);
                    ReadSize();
                }
            } catch (Exception ex) {
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

        public void Dispose() {
            socket.Dispose();
            buffer = null;
        }

    }
}
