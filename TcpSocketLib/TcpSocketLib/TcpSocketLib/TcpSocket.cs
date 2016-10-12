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
        public delegate void ReceiveProgressChangedHandler(TcpSocket sender, int Received, int BytesToReceive);

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

        Socket _socket;

        Stopwatch _stopWatch;

        byte[] _buffer;
        object _syncLock = new object();

        long _now = 0;
        long last = 0;
        long _time = 0;
        int _receiveRate = 0;

        int _totalRead = 0;

        public TcpSocket(Socket socket, int MaxPacketSize) {
            if (socket != null) {
                this._socket = socket;
                this.MaxPacketSize = MaxPacketSize;
                RemoteEndPoint = socket.RemoteEndPoint;
                this._socket.NoDelay = true;
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
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.NoDelay = true;
        }

        public void Start() {
            if (!Running) {
                Running = true;
                _stopWatch = Stopwatch.StartNew();
                _now = _stopWatch.ElapsedMilliseconds;
                last = _now;

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
            _buffer = new byte[byteCount];
        }

        private void Connect(string IP, int Port)
        {
            try
            {
                _socket.Connect(IP, Port);
                ClientConnected?.Invoke(this);
                _socket.NoDelay = true;
                Start();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void BeginReadSize() {
            //The first 4 bytes of the stream contains the size as int32
            this._socket.BeginReceive(_buffer, _totalRead, _buffer.Length - _totalRead, SocketFlags.None, ReadSizeCallBack, null);
        }

        private void ReadSizeCallBack(IAsyncResult iar) {
            try {

                int read = this._socket.EndReceive(iar);

                if (read <= 0)
                    throw new SocketException((int)SocketError.ConnectionReset);

                else {

                    _totalRead += read;

                    if (FloodDetector != null)
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
            _receiveRate++;

            _now = _stopWatch.ElapsedMilliseconds;
            _time = (_now - last);

            if (_time >= FloodDetector?.Delta) {
                last = _now;

                if (_receiveRate > FloodDetector?.Receives)
                    FloodDetected?.Invoke(this);

                _receiveRate = 0;
            }
        }

        void HandleDisconnect(Exception ex) {
#if DEBUG
            Console.WriteLine(ex.StackTrace);
#endif
            ClientDisconnected?.Invoke(this);
            this._socket.Close();
            Running = false;
        }

        private void BeginReadPayload() {
            this._socket.BeginReceive(_buffer, _totalRead, _buffer.Length - _totalRead, SocketFlags.None, ReadPayloadCallBack, null);
        }

        private void ReadPayloadCallBack(IAsyncResult iar) {
            try {
                int read = this._socket.EndReceive(iar);

                if (read <= 0)
                    throw new SocketException((int)SocketError.ConnectionReset);

                _totalRead += read;
                //Report progress about receiving.
                ReceiveProgressChanged?.Invoke(this, _totalRead, _buffer.Length);

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
            }
            catch (Exception ex) {
                HandleDisconnect(ex);
            }
        }

        public void Send(byte[] bytes) {
            lock (_syncLock) {
                this._socket.Send(BitConverter.GetBytes(bytes.Length));
                this._socket.Send(bytes);
            }
        }

        public void Dispose() {
            Disconnect();
            _buffer = null;
            RemoteEndPoint = null;
            FloodDetector = null;

        }
    }
}
