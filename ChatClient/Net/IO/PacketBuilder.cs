using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ChatClient.Net.IO
{
    public class PacketBuilder
    {
        MemoryStream _ms;
        public PacketBuilder()
        {
            _ms = new MemoryStream();
        }

        public void WriteOpCode (byte opcode)
        {
            //reserve 1 byte for opcode
            _ms.WriteByte(opcode);
        }

        public void WriteMessage (string msg)
        {
            //reserve 4 bytes for length of the message
            var msgBytes = Encoding.UTF8.GetBytes(msg);
            _ms.Write(BitConverter.GetBytes(msgBytes.Length));
            _ms.Write(msgBytes);
        }

        public byte[] GetPacketBytes()
        {
            return _ms.ToArray();
        }

        public void WriteInt(int value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            _ms.Write(bytes, 0, bytes.Length);
        }

        public void WriteLong(long value)
        {
            var bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            _ms.Write(bytes, 0, bytes.Length);
        }

        public void WriteBytes(byte[] buffer, int length)
        {
            _ms.Write(buffer, 0, length);
        }

    }
}
