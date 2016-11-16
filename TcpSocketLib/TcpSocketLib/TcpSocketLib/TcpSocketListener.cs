using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace TcpSocketLib
{
    public class TcpSocketListener : IDisposable
    {
        //Listener event delegates
        public delegate void ListeningStateChangedEventHandler(TcpSocketListener listener, bool Listening);

        //Listener events
        public event ListeningStateChangedEventHandler ListeningStateChanged;

        //Client event delegates
        public delegate void PacketReceivedEventHandler(TcpSocket sender, PacketReceivedArgs PacketReceivedArgs);
        public delegate void FloodDetectedEventHandler(TcpSocket sender);
        public delegate void ClientStateChangedEventHandler(TcpSocket sender, Exception Message, bool Connected);
        public delegate void ReceiveProgressChangedEventHandler(TcpSocket sender, int Received, int BytesToReceive);
        public delegate void SendProgressChangedEventHandler(TcpSocket sender, int Send);

        //Client events
        public event ReceiveProgressChangedEventHandler ReceiveProgressChanged;
        public event PacketReceivedEventHandler PacketReceived;
        public event ClientStateChangedEventHandler ClientStateChanged;
        public event FloodDetectedEventHandler FloodDetected;
        public event SendProgressChangedEventHandler SendProgressChanged;

        public List<TcpSocket> ConnectedClients {
            get {
                lock (_syncLock) { return _connectedClients; }
            }
        }

        public bool Running { get; private set; }
        public int Port { get; private set; }
        public int MaxConnectionQueue { get; private set; }
        public int MaxPacketSize { get; private set; }

        Socket _listener;
        List<TcpSocket> _connectedClients;
        object _syncLock;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Port">Port to listen on</param>
        /// <param name="MaxPacketSize">Determines the max allowed packet size to be received, unless specifically set by the client object</param>
        public TcpSocketListener(int Port, int MaxPacketSize = 85000) {
            this.MaxPacketSize = MaxPacketSize;
            this.Port = Port;

            Running = false;

            _syncLock = new object();
            _connectedClients = new List<TcpSocket>();

        }

        public void Start(int MaxConnectionQueue = 25) {
            if (Running) {
                throw new InvalidOperationException("Listener is already running.");
            } else {
                this.MaxConnectionQueue = MaxConnectionQueue;
                _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listener.Bind(new IPEndPoint(0, Port));
                _listener.Listen(MaxConnectionQueue);
                Running = true;
                ListeningStateChanged?.Invoke(this, Running);
                _listener.BeginAccept(AcceptCallBack, null);
            }
        }

        public void Stop() {
            if (Running) {
                _listener.Close();
                _listener = null;
                foreach (var client in _connectedClients) {
                    client.Dispose();
                }
                _connectedClients.Clear();
                MaxConnectionQueue = 0;
                Running = false;
                ListeningStateChanged?.Invoke(this, Running);
            } else {
                throw new InvalidOperationException("Listener isn't running.");
            }
        }

        private void AcceptCallBack(IAsyncResult iar) {
            try {
                Socket accepted = _listener.EndAccept(iar);
                TcpSocket tcpSocket = new TcpSocket(accepted, MaxPacketSize);

                tcpSocket.ClientStateChanged += TcpSocket_ClientStateChanged;
                tcpSocket.PacketReceived += TcpSocket_PacketReceived;
                tcpSocket.ReceiveProgressChanged += TcpSocket_ReceiveProgressChanged;
                tcpSocket.SendProgressChanged += TcpSocket_SendProgressChanged;
                tcpSocket.FloodDetected += TcpSocket_FloodDetected;

                tcpSocket.Start();
                _listener.BeginAccept(AcceptCallBack, null);
            } catch {
                //MessageBox.Show(ex.Message + " \n\n [" + ex.StackTrace + "]");
            }
        }

        public void BroadcastPacket(byte[] Packet, TcpSocket Exception = null) {
            lock (_syncLock) {
                foreach (var client in _connectedClients) {
                    if (client != Exception) {
                        try { client.SendPacket(Packet); } catch { }
                    }
                }
            }
        }

        private void TcpSocket_ClientStateChanged(TcpSocket sender, Exception Message, bool Connected) {
            lock (_syncLock) {
                if (Connected) {
                    _connectedClients.Add(sender);
                }
                else {
                    _connectedClients.Remove(sender);
                }
                ClientStateChanged?.Invoke(sender, Message, Connected);
            }
        }

        private void TcpSocket_SendProgressChanged(TcpSocket sender, int Send)
        {
            SendProgressChanged?.Invoke(sender, Send);
        }

        private void TcpSocket_FloodDetected(TcpSocket sender) {
            FloodDetected?.Invoke(sender);
        }

        private void TcpSocket_ReceiveProgressChanged(TcpSocket sender, int Received, int BytesToReceive) {
            ReceiveProgressChanged?.Invoke(sender, Received, BytesToReceive);
        }

        private void TcpSocket_PacketReceived(TcpSocket sender, PacketReceivedArgs PacketReceivedArgs) {
            PacketReceived?.Invoke(sender, PacketReceivedArgs);
        }

        public void Dispose() {
            Stop();
        }
    }
}
