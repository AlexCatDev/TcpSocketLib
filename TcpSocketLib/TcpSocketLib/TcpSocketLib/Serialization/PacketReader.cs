using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace TcpSocketLib.Serialization
{
    public class PacketReader : BinaryReader
    {
        private BinaryFormatter _bf;

        public PacketReader(byte[] data)
            : base(new MemoryStream(data)) {
            _bf = new BinaryFormatter();
        }

        public Stream ReadStream()
        {
            int streamSize = ReadInt32();
            byte[] streamData = ReadBytes(streamSize);
            using(MemoryStream ms = new MemoryStream(streamData))
            {
                return ms;
            }
        }

        public T ReadObject<T>() {
            return (T)_bf.Deserialize(BaseStream);
        }
    }
}
