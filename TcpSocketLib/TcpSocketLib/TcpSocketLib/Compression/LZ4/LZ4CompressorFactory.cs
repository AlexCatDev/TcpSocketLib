﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TcpSocketLib.Compression.LZ4
{
    public static class LZ4CompressorFactory
    {
        public static ILZ4Compressor CreateNew()
        {
            if (IntPtr.Size == 4)
                return new LZ4Compressor32();
            return new LZ4Compressor64();
        }
    }
}
