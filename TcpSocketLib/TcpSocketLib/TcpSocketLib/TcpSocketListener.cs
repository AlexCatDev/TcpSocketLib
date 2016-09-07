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

        public event PacketReceivedEventHandler PacketRecieved;
        public event ClientConnectedEventHandler ClientConnected;
        public event ClientDisconnectedEventHandler ClientDisconnected;
        public event FloodDetectedEventHandler FloodDetected;

        public List<TcpSocket> ConnectedClients { get; private set; }

        public bool Running { get; private set; }
        public int Port { get; private set; }
        public int MaxConnectionQueue { get; private set; }

        Socket listener;

        public TcpSocketListener(int Port) {
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
                TcpSocket s = new TcpSocket(accepted, this);
                this.ConnectedClients.Add(s);
                this.ClientConnected?.Invoke(s);
                s.Start();

                this.listener.BeginAccept(AcceptCallBack, null);
            } catch (Exception) {

            }
        }

        public class TcpSocket
        {

            public object UserState { get; set; }
            public bool Running { get; private set; }
            public int MaxPacketSize { get; set; }

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
            int packetRate = 0;

            public TcpSocket(Socket socket, TcpSocketListener listener) {
                this.socket = socket;
                this.listener = listener;
                this.MaxPacketSize = int.MaxValue;
                this.RemoteEndPoint = socket.RemoteEndPoint;
                this.socket.NoDelay = true;

                Running = true;
            }

            public void Start() {
                if (!Running) {
                    this.stopWatch = Stopwatch.StartNew();
                    now = stopWatch.ElapsedMilliseconds;
                    last = now;

                    Read();
                } else 
                    throw new InvalidOperationException("Client already running");
            }

            private void Read() {
                //The first 4 bytes of the stream contains the size as int32
                buffer = new byte[4];
                this.socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.Partial, ReceiveCallBack, null);
            }

            public void Disconnect() {
                HandleDisconnect(new Exception("Manual disconnect"));
            }

            private void ReceiveCallBack(IAsyncResult iar) {
                try {
                    //Check if we recieved more than 1 byte
                    if (this.socket.EndReceive(iar) > 1) {

                        #region FloodProtector

                        if (FloodProtector != null) {
                            packetRate++;

                            now = stopWatch.ElapsedMilliseconds;
                            time = (now - last);

                            if (time >= FloodProtector?.Time) {
                                last = now;

                                if (packetRate > FloodProtector?.MaxPackets)
                                    listener.FloodDetected?.Invoke(this);

                                packetRate = 0;
                            }
                        }
                        #endregion

                        int dataSize = BitConverter.ToInt32(buffer, 0);

                        //Check if the data size is bigger than whats allowed
                        if (dataSize > MaxPacketSize)
                            HandleDisconnect(new Exception("Packet was bigger than allowed"));

                            //Check if dataSize is bigger than 0
                            if (dataSize > 0) {

                            //Allocate a buffer with the size
                            buffer = new byte[dataSize];
                            this.socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.Partial, FinalReceiveCallBack, null);
                                return;
                            } else {
                                HandleDisconnect(new Exception("data was 0 length"));
                            }
                    } else {
                        HandleDisconnect(new Exception("Received a invalid packet"));
                    }

                } catch (Exception ex) {
                    HandleDisconnect(ex);
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

            private void FinalReceiveCallBack(IAsyncResult iar) {
                try {
                    this.socket.EndReceive(iar);
                    this.listener.PacketRecieved?.Invoke(this, new PacketReceivedArgs(buffer));
                    Read();
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
