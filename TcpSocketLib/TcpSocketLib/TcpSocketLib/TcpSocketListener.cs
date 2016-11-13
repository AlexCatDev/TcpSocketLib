using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace TcpSocketLib
{
    public class TcpSocketListener : IDisposable
    {

        public delegate void PacketReceivedEventHandler(TcpSocket sender, PacketReceivedArgs PacketReceivedArgs);
        public delegate void FloodDetectedEventHandler(TcpSocket sender);
        public delegate void ClientConnectedEventHandler(TcpSocket sender);
        public delegate void ClientDisconnectedEventHandler(TcpSocket sender);
        public delegate void ReceiveProgressChangedEventHandler(TcpSocket sender, int Received, int BytesToReceive);
        public delegate void SendProgressChangedEventHandler(TcpSocket sender, int Send);

        public event ReceiveProgressChangedEventHandler ReceiveProgressChanged;
        public event PacketReceivedEventHandler PacketReceived;
        public event ClientConnectedEventHandler ClientConnected;
        public event ClientDisconnectedEventHandler ClientDisconnected;
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
            } else {
                throw new InvalidOperationException("Listener isn't running.");
            }
        }

        private void AcceptCallBack(IAsyncResult iar) {
            try {
                Socket accepted = _listener.EndAccept(iar);
                TcpSocket tcpSocket = new TcpSocket(accepted, MaxPacketSize);

                tcpSocket.ClientConnected += TcpSocket_ClientConnected;
                tcpSocket.ClientDisconnected += TcpSocket_ClientDisconnected;
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

        private void TcpSocket_ClientDisconnected(TcpSocket sender) {
            lock (_syncLock) {
                _connectedClients.Remove(sender);
            }
            ClientDisconnected?.Invoke(sender);
        }

        private void TcpSocket_ClientConnected(TcpSocket sender) {
            lock (_syncLock) {
                _connectedClients.Add(sender);
            }
            ClientConnected?.Invoke(sender);
        }

        public void Dispose() {
            Stop();
        }
    }
}
