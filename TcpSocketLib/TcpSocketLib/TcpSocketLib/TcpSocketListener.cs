using System;
using System.Collections.Generic;
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
        public delegate void ReceiveProgressChangedHandler(TcpSocket sender, int Received, int BytesToReceive, double Percent);

        public event ReceiveProgressChangedHandler ReceiveProgressChanged;
        public event PacketReceivedEventHandler PacketReceived;
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

        public TcpSocket[] GetConnectedClients => ConnectedClients.ToArray();

        private void AcceptCallBack(IAsyncResult iar) {
            try {
                Socket accepted = listener.EndAccept(iar);
                TcpSocket s = new TcpSocket(accepted, MaxPacketSize);
                s.ClientConnected += (sender) => {
                    ClientConnected?.Invoke(sender);
                };
                s.ClientDisconnected += (sender) => {
                    ClientDisconnected?.Invoke(sender);
                };
                s.FloodDetected += (sender) => {
                    FloodDetected?.Invoke(sender);
                };
                s.PacketReceived += (sender, packet) => {
                    PacketReceived?.Invoke(sender, packet);
                };
                s.ReceiveProgressChanged += (sender, r, btr, percent) => {
                    ReceiveProgressChanged?.Invoke(sender, r, btr, percent);
                };
                this.ConnectedClients.Add(s);
                s.Start();

                this.listener.BeginAccept(AcceptCallBack, null);
            } catch (Exception ex) {
                Console.WriteLine(ex);
            }
        }

       
    }
}
