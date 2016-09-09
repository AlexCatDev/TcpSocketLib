using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace TcpSocketLib
{
    public class TcpSocketListener
    {
        public delegate void PacketReceivedEventHandler(TcpSocket sender, PacketReceivedArgs PacketReceivedArgs);
        public delegate void FloodDetectedEventHandler(TcpSocket sender);
        public delegate void ClientConnectedEventHandler(TcpSocket sender);
        public delegate void ClientDisconnectedEventHandler(TcpSocket sender);
        public delegate void ReceiveProgressChangedHandler(TcpSocket sender, int received, int bytesToReceive);

        public event ReceiveProgressChangedHandler ReceiveProgressChanged;
        public event PacketReceivedEventHandler PacketRecieved;
        public event ClientConnectedEventHandler ClientConnected;
        public event ClientDisconnectedEventHandler ClientDisconnected;
        public event FloodDetectedEventHandler FloodDetected;

        public List<TcpSocket> ConnectedClients { get; private set; }

        public bool Running { get; private set; }
        public int Port { get; private set; }
        public int MaxConnectionQueue { get; private set; }
        public int MaxPacketSize { get; private set; }

        Socket listener;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Port">Port to listen on</param>
        /// <param name="MaxPacketSize">Determines the max allowed packet size to be received, unless specifically set by the client object</param>
        public TcpSocketListener(int Port, int MaxPacketSize = 85000) {
            this.MaxPacketSize = MaxPacketSize;
            this.Port = Port;
            this.Running = false;
        }

        public void Start(int MaxConnectionQueue = 25) {
            if (Running) {
                throw new InvalidOperationException("Listener is already running");
            } else {
                this.ConnectedClients = new List<TcpSocket>();
                this.MaxConnectionQueue = MaxConnectionQueue;
                this.listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                this.listener.Bind(new IPEndPoint(0, Port));
                this.listener.Listen(MaxConnectionQueue);
                this.listener.BeginAccept(AcceptCallBack, null);
                this.Running = true;
            }
        }

        public void Stop() {
            if (Running) {
                listener.Close();
                listener = null;
                ConnectedClients = null;
                MaxConnectionQueue = 0;
                Running = false;
            } else {
                throw new InvalidOperationException("Listener isn't running");
            }
        }

        public TcpSocket[] GetConnectedClients() {
            return ConnectedClients.ToArray();
        }

        private void AcceptCallBack(IAsyncResult iar) {
            try {
                Socket accepted = listener.EndAccept(iar);
                TcpSocket s = new TcpSocket(accepted, this, MaxPacketSize);
                this.ConnectedClients.Add(s);
                this.ClientConnected?.Invoke(s);
                s.Start();

                this.listener.BeginAccept(AcceptCallBack, null);
            } catch (Exception) {

            }
        }

        public class TcpSocket
        {

            private const int SIZE_PAYLOAD_LENGTH = sizeof(int);

            public object UserState { get; set; }
            public bool Running { get; private set; }
            public int MaxPacketSize { get; set; }

            public bool AllowZeroLengthPackets { get; set; }

            public EndPoint RemoteEndPoint { get; private set; }
            public FloodProtector FloodProtector { get; set; }

            Socket socket;
            TcpSocketListener listener;

            Stopwatch stopWatch;

            byte[] buffer;
            object sendLock = new object();

            long now = 0;
            long last = 0;
            long time = 0;
            int receiveRate = 0;

            int totalRead = 0;

            public TcpSocket(Socket socket, TcpSocketListener listener, int MaxPacketSize) {
                this.socket = socket;
                this.listener = listener;
                this.MaxPacketSize = int.MaxValue;
                this.RemoteEndPoint = socket.RemoteEndPoint;
                this.socket.NoDelay = true;

                this.AllowZeroLengthPackets = false;
                this.MaxPacketSize = MaxPacketSize;
                Running = true;
            }

            public void Start() {
                if (!Running) {
                    this.stopWatch = Stopwatch.StartNew();
                    now = stopWatch.ElapsedMilliseconds;
                    last = now;
                    AllocateBuffer(SIZE_PAYLOAD_LENGTH);
                    ReadSize();
                } else 
                    throw new InvalidOperationException("Client already running");
            }

            public void Disconnect() {
                HandleDisconnect(new Exception("Manual disconnect"));
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
                        HandleDisconnect(new Exception("Disconnected."));
                    else {

                        totalRead += read;

                        if (FloodProtector != null)
                            CheckFlood();

                        if (totalRead < buffer.Length) {
                            ReadSize();
                        } else {

                            int dataSize = BitConverter.ToInt32(buffer, 0);

                            //Check if the data size is bigger than whats allowed
                            if (dataSize > MaxPacketSize)
                                HandleDisconnect(new Exception("Packet was bigger than allowed"));

                            //Check if dataSize is bigger than 0
                            if (dataSize > 0) {

                                //Allocate a buffer with the size
                                AllocateBuffer(dataSize);
                                totalRead = 0;
                                ReadPayload();
                                return;
                            } else {
                                if (AllowZeroLengthPackets) {
                                    totalRead = 0;
                                    ReadSize();
                                    this.listener.PacketRecieved?.Invoke(this, new PacketReceivedArgs(new byte[0]));
                                } else
                                    HandleDisconnect(new Exception("0 length packets wasn't set to be allowed"));
                            }
                        }
                    }
                } catch (Exception ex) {
                    HandleDisconnect(ex);
                }

            }

            private void CheckFlood() {
                receiveRate++;

                now = stopWatch.ElapsedMilliseconds;
                time = (now - last);

                if (time >= FloodProtector?.Delta) {
                    last = now;

                    if (receiveRate > FloodProtector?.MaxReceives)
                        listener.FloodDetected?.Invoke(this);

                    receiveRate = 0;
                }
            }

            void HandleDisconnect(Exception ex) {
#if DEBUG
                Console.WriteLine(ex.StackTrace);
#endif
                this.listener.ConnectedClients.Remove(this);
                this.listener.ClientDisconnected?.Invoke(this);
                this.socket.Close();
            }

            private void ReadPayload() {
                this.socket.BeginReceive(buffer, totalRead, buffer.Length - totalRead, SocketFlags.None, ReceivePayloadCallBack, null);
            }

            private void ReceivePayloadCallBack(IAsyncResult iar) {
                try {
                    int read;
                    if ((read = this.socket.EndReceive(iar)) <= 0)
                        HandleDisconnect(new Exception("Disconnected"));

                    totalRead += read;
                    //Report progress about receiving.
                    this.listener.ReceiveProgressChanged?.Invoke(this, totalRead, buffer.Length);

                    if(FloodProtector!=null)
                    CheckFlood();

                    if (totalRead < buffer.Length)
                        ReadPayload();
                    else {
                        this.listener.PacketRecieved?.Invoke(this, new PacketReceivedArgs(buffer));
                        totalRead = 0;
                        AllocateBuffer(SIZE_PAYLOAD_LENGTH);
                        ReadSize();
                    }
                }catch(Exception ex) {
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
}
