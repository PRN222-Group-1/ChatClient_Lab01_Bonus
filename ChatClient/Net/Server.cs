using System.Net.Sockets;
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

        public Server()
        {
            _client = new TcpClient();
        }

        public void ConnectToServer(string username)
        {
            if (!_client.Connected)
            {
                _client.Connect("165.232.167.179", 9000);
                packetReader = new PacketReader(_client.GetStream());

                if (!string.IsNullOrEmpty(username))
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
                try
                {
                    while (_client.Connected)
                    {
                        var opcode = packetReader.ReadByte();
                        switch (opcode)
                        {
                            case 1:
                                connectedEvent?.Invoke();
                                break;
                            case 5: 
                                messageReceivedEvent?.Invoke(); 
                                break;
                            case 10: 
                                userDisconnectedEvent?.Invoke(); 
                                break;
                            default: 
                                Console.WriteLine("Unknown opcode received: " + opcode);
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Connection lost: {ex.Message}");
                    _client?.Close();
                }
            });
        }


        public void SendMessageToServer(string message)
        {
            try
            {
                var messagePacket = new PacketBuilder();

                messagePacket.WriteOpCode(5); //Message opcode
                messagePacket.WriteMessage(message);
                _client.Client.Send(messagePacket.GetPacketBytes());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending message: " + ex.Message);
            }
        }

        public void DisconnectFromServer(string uid)
        {
            var disconnectPacket = new PacketBuilder();

            disconnectPacket.WriteOpCode(10); //Disconnect opcode
            disconnectPacket.WriteMessage(uid);
            _client.Client.Send(disconnectPacket.GetPacketBytes());
        }
    }
}
