using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using ChatClient.Net.IO;

namespace ChatClient.Net
{
    public class Server
    {
        TcpClient _client;
        public PacketReader packetReader;

        public event Action connectedEvent;
        public event Action messageReceivedEvent;
        public event Action userDisconnectedEvent;

        public Server ()
        {
            _client = new TcpClient();
        }

        public void ConnectToServer (string username)
        {
            if(!_client.Connected)
            {
                _client.Connect("127.0.0.1", 9000);
                packetReader = new PacketReader(_client.GetStream());
                
                if(!string.IsNullOrEmpty(username))
                {
                    var connectPacket = new PacketBuilder();
                    connectPacket.WriteOpCode(0);
                    connectPacket.WriteMessage(username);
                    _client.Client.Send(connectPacket.GetPacketBytes());

                }
                ReadPackets(); 
            }
        }

        private void ReadPackets()
        {
            Task.Run(() =>
            {
                while(true)
                {
                    var opcode = packetReader.ReadByte();
                    switch(opcode)
                    {
                        case 1: //User Connected
                            connectedEvent?.Invoke();
                            break;
                        case 5: //User sent message
                            messageReceivedEvent?.Invoke();
                            break;
                        case 10: //User disconnected
                            userDisconnectedEvent?.Invoke();
                            break;
                        default:
                            Console.WriteLine("Unknown opcode received: " + opcode);
                            break;
                    }
                }
            });
        }

        public void SendMessageToServer (string message)
        {
            var messagePacket = new PacketBuilder();

            messagePacket.WriteOpCode(5); //Message opcode
            messagePacket.WriteMessage(message);
            _client .Client.Send(messagePacket.GetPacketBytes());
        }
    }
}
