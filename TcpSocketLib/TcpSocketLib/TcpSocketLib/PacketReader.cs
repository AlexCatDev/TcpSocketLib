using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace TcpSocketLib
{
    public class PacketReader : BinaryReader
    {
        private BinaryFormatter _bf;
        public PacketReader(byte[] data)
            : base(new MemoryStream(data)) {
            _bf = new BinaryFormatter();
        }

        public T ReadObject<T>() {
            return (T)_bf.Deserialize(BaseStream);
        }
    }
}
