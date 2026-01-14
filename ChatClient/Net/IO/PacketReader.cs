using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatClient.Net.IO
{
    public class PacketReader : BinaryReader
    {
        private NetworkStream _ns;

        public PacketReader(NetworkStream ns) : base(ns)
        {
            _ns = ns;
        }

        public string ReadMessage()
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

        public int ReadInt()
        {
            byte[] buffer = new byte[4];
            int read = 0;
            while (read < 4)
            {
                int bytes = _ns.Read(buffer, read, 4 - read);
                if (bytes == 0)
                    throw new Exception("Disconnected");
                read += bytes;
            }
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(buffer);
            return BitConverter.ToInt32(buffer, 0);
        }

        public byte[] ReadBytes(int length)
        {
            byte[] buffer = new byte[length];
            int read = 0;
            while (read < length)
            {
                int bytes = _ns.Read(buffer, read, length - read);
                if (bytes == 0)
                    throw new Exception("Disconnected");
                read += bytes;
            }
            return buffer;
        }

        public long ReadLong()
        {
            byte[] buffer = new byte[8];
            int read = 0;
            while (read < 8)
            {
                int bytes = _ns.Read(buffer, read, 8 - read);
                if (bytes == 0)
                    throw new Exception("Disconnected");
                read += bytes;
            }
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(buffer);
            return BitConverter.ToInt64(buffer, 0);
        }

        // THÊM METHOD NÀY
        public new byte ReadByte()
        {
            int b = _ns.ReadByte();
            if (b == -1)
                throw new Exception("Disconnected");
            return (byte)b;
        }
    }
}
