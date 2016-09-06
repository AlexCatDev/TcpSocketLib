using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace TcpSocketLib
{
    public class TcpSocketListener
    {

        public class ClientConnectedArgs
        {
            public TcpSocket TcpSocket { get; set; }
            public DateTime TimeStamp { get; set; }

            public ClientConnectedArgs(TcpSocket socket) {
                TcpSocket = socket;
                TimeStamp = DateTime.Now;
            }
        }

        public class ClientDisconnectedArgs
        {
            public TcpSocket TcpSocket { get; set; }
            public DateTime TimeStamp { get; set; }

            public ClientDisconnectedArgs(TcpSocket socket) {
                TcpSocket = socket;
                TimeStamp = DateTime.Now;
            }
        }

        public class FloodDetectedArgs
        {
            public TcpSocket TcpSocket { get; set; }
            public FloodProtector FloodProtector { get; set; }

            public FloodDetectedArgs(TcpSocket socket, FloodProtector protector) {
                TcpSocket = socket;
                FloodProtector = protector;
            }
        }

        public class PacketReceivedArgs
        {
            public TcpSocket TcpSocket { get; set; }
            public byte[] Data { get; set; }
            public int Length { get; set; }

            public PacketReceivedArgs(TcpSocket socket, byte[] data) {
                this.TcpSocket = socket;
                this.Data = data;
                this.Length = data.Length;
            }
        }

        public delegate void PacketReceivedEventHandler(PacketReceivedArgs PacketReceivedArgs);
        public delegate void FloodDetectedEventHandler(FloodDetectedArgs FloodDetectedArgs);
        public delegate void ClientConnectedEventHandler(ClientConnectedArgs ClientConnectedArgs);
        public delegate void ClientDisconnectedEventHandler(ClientDisconnectedArgs ClientDisconnectedArgs);

        public event PacketReceivedEventHandler PacketRecieved;
        public event ClientConnectedEventHandler ClientConnected;
        public event ClientDisconnectedEventHandler ClientDisconnected;
        public event FloodDetectedEventHandler FloodDetected;

        public List<TcpSocket> ConnectedClients { get; private set; }

        public bool Running { get; private set; }
        public int Port { get; private set; }
        public int MaxConnectionQueue { get; private set; }

        Socket listener;

        public TcpSocket[] GetConnectedClients() {
            return ConnectedClients.ToArray();
        }

        public TcpSocketListener(int Port) {
            this.Port = Port;
            this.Running = false;
        }

        public void Start(int MaxConnectionQueue = 25) {
            if (Running) {
                throw new InvalidOperationException("Listener is already running.");
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
                throw new InvalidOperationException("Listener isn't running.");
            }
        }

        private void AcceptCallBack(IAsyncResult iar) {
            try {
                Socket accepted = listener.EndAccept(iar);
                TcpSocket s = new TcpSocket(accepted, this);
                this.ConnectedClients.Add(s);
                this.ClientConnected?.Invoke(new ClientConnectedArgs(s));
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
            TcpSocketListener tsl;

            Stopwatch stopWatch;

            byte[] buffer;
            object sendLock = new object();

            long now = 0;
            long last = 0;
            long time = 0;
            int packetRate = 0;

            public TcpSocket(Socket socket, TcpSocketListener tsl) {
                this.socket = socket;
                this.tsl = tsl;
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
                        //Allocate a buffer with the size
                        buffer = new byte[BitConverter.ToInt32(buffer, 0)];

                        #region FloodProtector
                        //Flood protector stuff (buggy) lol
                        if (FloodProtector != null) {
                            packetRate++;

                            now = stopWatch.ElapsedMilliseconds;
                            time = (now - last);

                            if (time >= FloodProtector?.OverTime) {
                                last = now;
#if DEBUG
                                Console.WriteLine("FloodProtector Status:\nTime: {0}\nPackets during that period: {1}", time, packetRate);
#endif
                                if (packetRate >= FloodProtector?.MaxPackets) 
                                    tsl.FloodDetected?.Invoke(new FloodDetectedArgs(this, FloodProtector));
                                packetRate = 0;
                            }
                        }
                        #endregion

                        //Check if the buffer size is bigger than whats allowed
                        if (buffer.Length > MaxPacketSize)
                            HandleDisconnect(new Exception("Message was too big"));
                        else {
                            //Check if we're gonna recieve more than 0 bytes
                            if (buffer.Length > 0) {
                                this.socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.Partial, FinalReceiveCallBack, null);
                                return;
                            } else {
                                HandleDisconnect(new Exception("Buffer is 0 length"));
                            }
                        }
                    } else {
                        HandleDisconnect(new Exception("Length was somehow less than 1?"));
                    }

                } catch (Exception ex) {
                    HandleDisconnect(ex);
                }

            }

            void HandleDisconnect(Exception ex) {
#if DEBUG
                Console.WriteLine(ex.StackTrace);
#endif
                this.tsl.ConnectedClients.Remove(this);
                this.tsl.ClientDisconnected?.Invoke(new ClientDisconnectedArgs(this));
                this.socket.Close();
            }

            private void FinalReceiveCallBack(IAsyncResult iar) {
                try {
                    this.socket.EndReceive(iar);
                    this.tsl.PacketRecieved?.Invoke(new PacketReceivedArgs(this, buffer));
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
