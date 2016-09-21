using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpSocketLib
{
    public class PacketReceivedArgs
    {
        public byte[] Data { get; set; }
        public int Length { get; set; }

        public PacketReceivedArgs(byte[] data) {
            this.Data = data;
            this.Length = data.Length;
        }
    }
}
