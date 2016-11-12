using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace TcpSocketLib.Serialization
{
    public class PacketWriter : BinaryWriter
    {
        private MemoryStream _ms;
        private BinaryFormatter _bf;

        public PacketWriter()
            : base() {
            _ms = new MemoryStream();
            _bf = new BinaryFormatter();
            OutStream = _ms;
        }

        public void WriteObject<T>(T obj) {
            _bf.Serialize(_ms, obj);
        }

        public void Write(Stream stream)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                byte[] streamData = ms.ToArray();
                int sizeData = streamData.Length;
                Write(sizeData);
                Write(streamData);
            }
        }

        public byte[] Data => _ms.ToArray();
    }
}
