using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TcpSocketLib
{
    public class TcpSocket : IDisposable
    {
        //The maximum size of a single receive.
        public const int BUFFER_SIZE = 8192;
        //The size of the size header.
        public const int SIZE_BUFFER_LENGTH = 4;

        public delegate void PacketReceivedEventHandler(TcpSocket sender, PacketReceivedArgs PacketReceivedArgs);
        public delegate void FloodDetectedEventHandler(TcpSocket sender);
        public delegate void ClientStateChangedEventHandler(TcpSocket sender, Exception Message, bool Connected);
        public delegate void ReceiveProgressChangedEventHandler(TcpSocket sender, int Received, int BytesToReceive);
        public delegate void SendProgressChangedEventHandler(TcpSocket sender, int Send);

        public event ReceiveProgressChangedEventHandler ReceiveProgressChanged;
        public event PacketReceivedEventHandler PacketReceived;
        public event ClientStateChangedEventHandler ClientStateChanged;
        public event FloodDetectedEventHandler FloodDetected;
        public event SendProgressChangedEventHandler SendProgressChanged;

        public object UserState { get; set; }
        public bool Running { get; private set; }
        public int MaxPacketSize { get; set; }

        //Allow to receive null length packets (Can be used as keep alive packet)
        public bool AllowZeroLengthPackets { get; set; }

        public EndPoint RemoteEndPoint { get; private set; }
        public FloodDetector FloodDetector { get; set; }

        private Socket socket;
        private Stopwatch stopWatch;
        private object syncLock;

        private byte[] buffer;
        private int payloadSize;
        private int totalPayloadSize;
        private MemoryStream payloadStream;

        long now = 0;
        long last = 0;
        long time = 0;
        int receiveRate = 0;

        public TcpSocket(Socket socket, int MaxPacketSize) {
            if (socket != null) {
                this.socket = socket;
                this.MaxPacketSize = MaxPacketSize;
                RemoteEndPoint = socket.RemoteEndPoint;

                Setup();
            } else {
                throw new InvalidOperationException("Socket is null");
            }
        }

        public TcpSocket(int MaxPacketSize = int.MaxValue) {
            this.MaxPacketSize = MaxPacketSize;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;

            Setup();
        }

        private void Setup() {
            AllowZeroLengthPackets = false;
            Running = false;
            syncLock = new object();

            InitBuffer();
        }

        public void Start() {
            if (Running == false) {
                Running = true;
                ClientStateChanged?.Invoke(this, null, Running);
                stopWatch = Stopwatch.StartNew();
                now = stopWatch.ElapsedMilliseconds;
                last = now;

                BeginRead();

            } else if (Running == true) {
                throw new InvalidOperationException("Client already running");
            }
        }

        private void InitBuffer() {
            //Create a new instance of a byte array based on the buffer size
            buffer = new byte[BUFFER_SIZE];
        }

        public void Disconnect(bool reuseSocket) {
            HandleDisconnect(new Exception("Manual disconnect"), reuseSocket);
        }

        public void Connect(string IP, int Port)
        {
            try
            {
                socket.Connect(IP, Port);
                Start();
            }
            catch
            {
                throw;
            }
        }

        private void BeginRead() {
            try {
                //Attempt to receive the buffer size
                socket.BeginReceive(buffer, 0, SIZE_BUFFER_LENGTH, 0,
                    ReadSizeCallBack, null);
            }catch (NullReferenceException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (Exception ex) {
                HandleDisconnect(ex, false);
            }
        }

        /// <summary>
        /// This callback is for handling the receive of the size header.
        /// </summary>
        /// <param name="ar"></param>
        private void ReadSizeCallBack(IAsyncResult ar) {
            try {
                //Attempt to end the read
                var read = socket.EndReceive(ar);

                /*An exception should be thrown on EndReceive if the connection was lost.
                However, that is not always the case, so we check if read is zero or less.
                Which means disconnection.
                If there is a disconnection, we throw an exception*/
                if (read <= 0) {
                    throw new SocketException((int)SocketError.ConnectionAborted);
                }

                //If we didn't receive the full buffer size, something is lagging behind.
                if (read < SIZE_BUFFER_LENGTH) {
                    //Calculate how much is missing.
                    var left = SIZE_BUFFER_LENGTH - read;

                    //Wait until there is at least that much avilable.
                    while (socket.Available < left) {
                        Thread.Sleep(1);
                    }

                    //Use the synchronous receive since the data is close behind and shouldn't take much time.
                    socket.Receive(buffer, read, left, 0);
                }

                //Get the converted int value for the payload size from the received data
                payloadSize = BitConverter.ToInt32(buffer, 0);
                totalPayloadSize = payloadSize;

                if (payloadSize > MaxPacketSize)
                    throw new ProtocolViolationException($"Payload size exeeded max allowed [{payloadSize} > {MaxPacketSize}]");
                else if(payloadSize == 0) {
                    if (AllowZeroLengthPackets) {
                        BeginRead();
                        PacketReceived?.Invoke(this, new PacketReceivedArgs(new byte[0]));
                    }
                    else
                        throw new ProtocolViolationException("Zero-length packets are now set to be allowed!");
                }

                /*Get the initialize size we will read
                 * If its not more than the buffer size, we'll just use the full length*/
                var initialSize = payloadSize > BUFFER_SIZE ? BUFFER_SIZE :
                    payloadSize;

                //Initialize a new MemStream to receive chunks of the payload
                this.payloadStream = new MemoryStream();

                //Start the receive loop of the payload
                socket.BeginReceive(buffer, 0, initialSize, 0,
                    ReceivePayloadCallBack, null);
            }
            catch (NullReferenceException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (Exception ex) {
                HandleDisconnect(ex, false);
            }
        }

        private void ReceivePayloadCallBack(IAsyncResult ar) {
            try {
                //Attempt to finish the async read.
                var read = socket.EndReceive(ar);

                //Same as above
                if (read <= 0) {
                    throw new SocketException((int)SocketError.ConnectionAborted);
                }

                CheckFlood();

                //Subtract what we read from the payload size.
                payloadSize -= read;

                //Write the data to the payload stream.
                payloadStream.Write(buffer, 0, read);

                ReceiveProgressChanged?.Invoke(this, (int)payloadStream.Length, totalPayloadSize);

                //If there is more data to receive, keep the loop going.
                if (payloadSize > 0) {
                    //See how much data we need to receive like the initial receive.
                    int receiveSize = payloadSize > BUFFER_SIZE ? BUFFER_SIZE :
                        payloadSize;
                    socket.BeginReceive(buffer, 0, receiveSize, 0,
                        ReceivePayloadCallBack, null);
                }
                else //If we received everything
                {
                    //Close the payload stream
                    payloadStream.Close();

                    //Get the full payload
                    byte[] payload = payloadStream.ToArray();

                    //Dispose the stream
                    payloadStream = null;

                    //Start reading
                    BeginRead();
                    //Call the event method
                    PacketReceived?.Invoke(this, new PacketReceivedArgs(payload));
                }
            }
            catch (NullReferenceException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (Exception ex) {
                HandleDisconnect(ex, false);
            }
        }

        private void CheckFlood() {
            if (FloodDetector != null) {
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
        }

        private void HandleDisconnect(Exception ex, bool reuseSocket) {
            Running = false;
            socket.Disconnect(reuseSocket);
            if(ex!=null)
                ClientStateChanged?.Invoke(this, ex, Running);
            else
                ClientStateChanged?.Invoke(this, new Exception("None"), Running);
        }

        public void UnsubscribeEvents() {
            PacketReceived = null;
            FloodDetected = null;
            ClientStateChanged = null;
            SendProgressChanged = null;
            ReceiveProgressChanged = null;
        }

        private void SendData(byte[] data)
        {
            lock (syncLock)
            {
                socket.Send(BitConverter.GetBytes(data.Length));
                socket.Send(data);
                SendProgressChanged?.Invoke(this, data.Length + SIZE_BUFFER_LENGTH);
            }
        }

        public void SendPacket(byte[] bytes) {
            SendData(bytes);
        }

        public void Close() {
            socket.Close();
        }

        public void Dispose() {
            Close();
            buffer = null;
            RemoteEndPoint = null;
            FloodDetector = null;
            UserState = null;
            if(payloadStream!=null)
            payloadStream.Dispose();

            UnsubscribeEvents();
        }
    }
}
