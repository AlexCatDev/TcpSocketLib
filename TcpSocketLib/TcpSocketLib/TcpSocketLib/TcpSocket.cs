using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace TcpSocketLib
{
    public class TcpSocket : IDisposable
    {
        public const int SIZE_PAYLOAD_LENGTH = sizeof(int);

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

        public object UserState { get; set; }
        public bool Running { get; private set; }
        public int MaxPacketSize { get; set; }

        public bool Connected { get; set; }

        public bool AllowZeroLengthPackets { get; set; }

        public EndPoint RemoteEndPoint { get; private set; }
        public FloodDetector FloodDetector { get; set; }

        Socket _socket;

        Stopwatch _stopWatch;

        byte[] _buffer;
        object _syncLock;

        long _now = 0;
        long _last = 0;
        long _time = 0;
        int _receiveRate = 0;

        int _totalRead = 0;

        public TcpSocket(Socket socket, int MaxPacketSize) {
            if (socket != null) {
                _socket = socket;
                this.MaxPacketSize = MaxPacketSize;
                RemoteEndPoint = socket.RemoteEndPoint;

                Setup();
            } else {
                throw new InvalidOperationException("Socket is null");
            }
        }

        public TcpSocket(int MaxPacketSize) {
            this.MaxPacketSize = MaxPacketSize;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.NoDelay = true;

            Setup();
        }

        private void Setup() {
            AllowZeroLengthPackets = false;
            Running = false;
            Connected = false;

            _syncLock = new object();
        }

        public void Start() {
            if (Running == false) {
                Connected = true;
                Running = true;
                ClientConnected?.Invoke(this);
                _stopWatch = Stopwatch.StartNew();
                _now = _stopWatch.ElapsedMilliseconds;
                _last = _now;
                AllocateBuffer(SIZE_PAYLOAD_LENGTH);
                BeginReadSize();

            } else if (Running == true) {
                throw new InvalidOperationException("Client already running");
            }
        }

        public void Disconnect() {
            HandleDisconnect(new Exception("Manual disconnect"));
        }

        private void AllocateBuffer(int byteCount) {
            _buffer = new byte[byteCount];
        }

        public void Connect(string IP, int Port)
        {
            try
            {
                _socket.Connect(IP, Port);
                Start();
            }
            catch
            {
                throw;
            }
        }

        private void BeginReadSize() {
            //The first 4 bytes of the stream contains the size as int32
            _socket.BeginReceive(_buffer, _totalRead, _buffer.Length - _totalRead, SocketFlags.None, ReadSizeCallBack, null);
        }

        private void ReadSizeCallBack(IAsyncResult iar) {
            try {

                int read = _socket.EndReceive(iar);

                if (read <= 0)
                    throw new SocketException((int)SocketError.ConnectionReset);

                else {

                    _totalRead += read;

                        CheckFlood();

                    if (_totalRead < _buffer.Length) {
                        BeginReadSize();
                    }
                    else {

                        int dataSize = BitConverter.ToInt32(_buffer, 0);

                        //Check if the data size is bigger than whats allowed
                        if (dataSize > MaxPacketSize)
                            HandleDisconnect(new Exception($"Packet was bigger than allowed {dataSize} > {MaxPacketSize}"));

                        //Check if dataSize is bigger than 0
                        if (dataSize > 0) {

                            //Allocate a buffer with the size
                            AllocateBuffer(dataSize);
                            _totalRead = 0;
                            BeginReadPayload();
                            return;
                        }
                        else {
                            if (AllowZeroLengthPackets) {
                                _totalRead = 0;
                                BeginReadSize();
                                PacketReceived?.Invoke(this, new PacketReceivedArgs(new byte[0]));
                            }
                            else
                                HandleDisconnect(new Exception("Zero length packets wasn't set to be allowed"));
                        }
                    }
                }
            }
            catch (ObjectDisposedException) { return; }
            catch (Exception ex) {
                HandleDisconnect(ex);
            }

        }

        private void CheckFlood() {
            if (FloodDetector != null) {
                _receiveRate++;

                _now = _stopWatch.ElapsedMilliseconds;
                _time = (_now - _last);

                if (_time >= FloodDetector?.Delta) {
                    _last = _now;

                    if (_receiveRate > FloodDetector?.Receives)
                        FloodDetected?.Invoke(this);

                    _receiveRate = 0;
                }
            }
        }

        void HandleDisconnect(Exception ex) {
            Connected = false;
            ClientDisconnected?.Invoke(this);
        }

        private void BeginReadPayload() {
            _socket.BeginReceive(_buffer, _totalRead, _buffer.Length - _totalRead, SocketFlags.None, ReadPayloadCallBack, null);
        }

        private void ReadPayloadCallBack(IAsyncResult iar) {
            try {
                int read = this._socket.EndReceive(iar);

                if (read <= 0)
                    throw new SocketException((int)SocketError.ConnectionReset);

                _totalRead += read;
                //Report progress about receiving.
                ReceiveProgressChanged?.Invoke(this, _totalRead + SIZE_PAYLOAD_LENGTH, _buffer.Length);

                if (FloodDetector != null)
                    CheckFlood();

                if (_totalRead < _buffer.Length)
                    BeginReadPayload();
                else {
                    PacketReceived?.Invoke(this, new PacketReceivedArgs(_buffer));
                    _totalRead = 0;
                    AllocateBuffer(SIZE_PAYLOAD_LENGTH);
                    BeginReadSize();
                }
            } catch (ObjectDisposedException) { return; }
            catch (Exception ex) {
                HandleDisconnect(ex);
            }
        }

        private void SendData(byte[] data)
        {
            lock (_syncLock)
            {
                _socket.Send(BitConverter.GetBytes(data.Length));
                _socket.Send(data);
                SendProgressChanged?.Invoke(this, data.Length);
            }
        }

        public void Send(byte[] bytes) {
            SendData(bytes);
        }

        public void Close() {
            _socket.Close();
        }

        public void Dispose() {
            Close();
            _buffer = null;
            RemoteEndPoint = null;
            FloodDetector = null;
            UserState = null;

            PacketReceived = null;
            FloodDetected = null;
            ClientConnected = null;
            ClientDisconnected = null;
            SendProgressChanged = null;
            ReceiveProgressChanged = null;
        }
    }
}
