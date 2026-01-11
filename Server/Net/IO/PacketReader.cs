using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatServer.Net.IO
{
    public class PacketReader : BinaryReader
    {
        private NetworkStream _ns;
        public PacketReader(NetworkStream ns) : base(ns)
        {
            _ns = ns;
        }

        public string ReadMessage ()
        {
            int length = ReadInt32();

            byte[] buffer = new byte[length];

            int totalRead = 0;

            while (totalRead < length)
            {
                int read = _ns.Read(buffer, totalRead, length - totalRead);
                if (read == 0)
                    return null;
                totalRead += read;
            }

            return Encoding.UTF8.GetString(buffer);
        }
     }
}
