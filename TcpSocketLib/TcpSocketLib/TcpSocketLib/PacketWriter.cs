using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace TcpSocketLib
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

        public byte[] Data => _ms.GetBuffer();
    }
}
