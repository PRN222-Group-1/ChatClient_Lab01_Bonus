using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatServer.Net.IO
{
    public class PacketReader
    {
        private readonly NetworkStream _ns;

        public PacketReader(NetworkStream ns)
        {
            _ns = ns;
        }

        public byte ReadByte()
        {
            int b = _ns.ReadByte();
            if (b == -1)
                throw new Exception("Disconnected");

            return (byte)b;
        }

        public int ReadInt()
        {
            byte[] buffer = ReadExact(4);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(buffer);

            return BitConverter.ToInt32(buffer, 0);
        }

        public long ReadLong()
        {
            byte[] buffer = ReadExact(8);

            if (!BitConverter.IsLittleEndian)
                Array.Reverse(buffer);

            return BitConverter.ToInt64(buffer, 0);
        }

        public byte[] ReadBytes(int length)
        {
            return ReadExact(length);
        }

        public string ReadMessage()
        {
            int length = ReadInt();
            byte[] buffer = ReadExact(length);
            return Encoding.UTF8.GetString(buffer);
        }

        private byte[] ReadExact(int length)
        {
            byte[] buffer = new byte[length];
            int totalRead = 0;

            while (totalRead < length)
            {
                int read = _ns.Read(buffer, totalRead, length - totalRead);
                if (read == 0)
                    throw new Exception("Disconnected");

                totalRead += read;
            }

            return buffer;
        }
    }
}
