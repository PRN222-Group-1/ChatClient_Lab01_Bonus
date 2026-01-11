using System.Text;

namespace ChatServer.Net.IO
{
    public class PacketBuilder
    {
        MemoryStream _ms;
        public PacketBuilder()
        {
            _ms = new MemoryStream();
        }

        public void WriteOpCode(byte opcode)
        {
            //reserve 1 byte for opcode
            _ms.WriteByte(opcode);
        }

        public void WriteMessage(string msg)
        {
            //reseave 4 bytes for length of the message
            var msgBytes = Encoding.UTF8.GetBytes(msg);
            _ms.Write(BitConverter.GetBytes(msgBytes.Length));
            _ms.Write(msgBytes);

        }

        public byte[] GetPacketBytes()
        {
            return _ms.ToArray();
        }
    }
}
