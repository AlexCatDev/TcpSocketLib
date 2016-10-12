using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace TcpSocketLib
{
    public class TcpSocket
    {

        public delegate void PacketReceivedEventHandler(TcpSocket sender, PacketReceivedArgs PacketReceivedArgs);
        public delegate void FloodDetectedEventHandler(TcpSocket sender);
        public delegate void ClientConnectedEventHandler(TcpSocket sender);
        public delegate void ClientDisconnectedEventHandler(TcpSocket sender);
        public delegate void ReceiveProgressChangedHandler(TcpSocket sender, int Received, int BytesToReceive, double Percent);

        public event ReceiveProgressChangedHandler ReceiveProgressChanged;
        public event PacketReceivedEventHandler PacketReceived;
        public event ClientConnectedEventHandler ClientConnected;
        public event ClientDisconnectedEventHandler ClientDisconnected;
        public event FloodDetectedEventHandler FloodDetected;

        private const int SIZE_PAYLOAD_LENGTH = sizeof(int);

        public object UserState { get; set; }
        public bool Running { get; private set; }
        public int MaxPacketSize { get; set; }

        public bool AllowZeroLengthPackets { get; set; }

        public EndPoint RemoteEndPoint { get; private set; }
        public FloodDetector FloodDetector { get; set; }

        Socket socket;

        Stopwatch stopWatch;

        byte[] buffer;
        object sendLock = new object();

        long now = 0;
        long last = 0;
        long time = 0;
        int receiveRate = 0;

        int totalRead = 0;

        public TcpSocket(Socket socket, int MaxPacketSize) {
            if (socket != null) {
                this.socket = socket;
                this.MaxPacketSize = MaxPacketSize;
                RemoteEndPoint = socket.RemoteEndPoint;
                this.socket.NoDelay = true;
                AllowZeroLengthPackets = false;
                Running = false;
                ClientConnected?.Invoke(this);
            }
            else {
                throw new InvalidOperationException("Invalid constructor");
            }
        }

        public TcpSocket(int MaxPacketSize) {
            this.MaxPacketSize = MaxPacketSize;
            AllowZeroLengthPackets = false;
            Running = false;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public void Start() {
            if (!Running) {
                Running = true;
                stopWatch = Stopwatch.StartNew();
                now = stopWatch.ElapsedMilliseconds;
                last = now;

                AllocateBuffer(SIZE_PAYLOAD_LENGTH);
                BeginReadSize();
            }
            else
                throw new InvalidOperationException("Client already running");
        }

        public void Disconnect() {
            HandleDisconnect(new Exception("Disconnect"));
        }

        private void AllocateBuffer(int byteCount) {
            buffer = new byte[byteCount];
        }

        private void Connect(string IP, int Port) {
            socket.Connect(IP, Port);
            ClientConnected?.Invoke(this);
            socket.NoDelay = true;
            Start();
        }

        private void BeginReadSize() {
            //The first 4 bytes of the stream contains the size as int32
            this.socket.BeginReceive(buffer, totalRead, buffer.Length - totalRead, SocketFlags.None, ReadSizeCallBack, null);
        }

        private void ReadSizeCallBack(IAsyncResult iar) {
            try {

                int read = this.socket.EndReceive(iar);

                if (read <= 0)
                    throw new SocketException((int)SocketError.ConnectionReset);

                else {

                    totalRead += read;

                    if (FloodDetector != null)
                        CheckFlood();

                    if (totalRead < buffer.Length) {
                        BeginReadSize();
                    }
                    else {

                        int dataSize = BitConverter.ToInt32(buffer, 0);

                        //Check if the data size is bigger than whats allowed
                        if (dataSize > MaxPacketSize)
                            HandleDisconnect(new Exception($"Packet was bigger than allowed {dataSize} > {MaxPacketSize}"));

                        //Check if dataSize is bigger than 0
                        if (dataSize > 0) {

                            //Allocate a buffer with the size
                            AllocateBuffer(dataSize);
                            totalRead = 0;
                            BeginReadPayload();
                            return;
                        }
                        else {
                            if (AllowZeroLengthPackets) {
                                totalRead = 0;
                                BeginReadSize();
                                PacketReceived?.Invoke(this, new PacketReceivedArgs(new byte[0]));
                            }
                            else
                                HandleDisconnect(new Exception("zero length packets wasn't set to be allowed"));
                        }
                    }
                }
            }
            catch (Exception ex) {
                HandleDisconnect(ex);
            }

        }

        private void CheckFlood() {
            receiveRate++;

            now = stopWatch.ElapsedMilliseconds;
            time = (now - last);

            if (time >= FloodDetector?.Delta) {
                last = now;

                if (receiveRate > FloodDetector?.Receives)
                    FloodDetected?.Invoke(this);

                receiveRate = 0;
            }
        }

        void HandleDisconnect(Exception ex) {
#if DEBUG
            Console.WriteLine(ex.StackTrace);
#endif
            ClientDisconnected?.Invoke(this);
            this.socket.Close();
            Running = false;
        }

        private void BeginReadPayload() {
            this.socket.BeginReceive(buffer, totalRead, buffer.Length - totalRead, SocketFlags.None, ReadPayloadCallBack, null);
        }

        private void ReadPayloadCallBack(IAsyncResult iar) {
            try {
                int read = this.socket.EndReceive(iar);

                if (read <= 0)
                    throw new SocketException((int)SocketError.ConnectionReset);

                totalRead += read;
                //Report progress about receiving.
                ReceiveProgressChanged?.Invoke(this, totalRead, buffer.Length, buffer.Length / totalRead);

                if (FloodDetector != null)
                    CheckFlood();

                if (totalRead < buffer.Length)
                    BeginReadPayload();
                else {
                    PacketReceived?.Invoke(this, new PacketReceivedArgs(buffer));
                    totalRead = 0;
                    AllocateBuffer(SIZE_PAYLOAD_LENGTH);
                    BeginReadSize();
                }
            }
            catch (Exception ex) {
                HandleDisconnect(ex);
            }
        }

        public void Send(byte[] bytes) {
            lock (sendLock) {
                this.socket.Send(BitConverter.GetBytes(bytes.Length));
                this.socket.Send(bytes);
            }
        }
    }
}
