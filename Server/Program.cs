using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using ChatServer.Net.IO;

namespace ChatServer
{
    class Program
    {
        static TcpListener _listener;
        static List<Client> _users;

        static ConcurrentDictionary<string, byte[]> _fileStorage = new();
        public static string _fileStorageFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerFiles");

        private const long MAX_CACHE_SIZE = 500 * 1024 * 1024; // 500MB
        private static long _currentCacheSize = 0;

        public static void Main(string[] args)
        {
            Directory.CreateDirectory(_fileStorageFolder);
            _users = new List<Client>();
            _listener = new TcpListener(System.Net.IPAddress.Any, 9000);
            _listener.Start();

            while (true)
            {
                var client = new Client(_listener.AcceptTcpClient());
                _users.Add(client);

                /*Broadcast the connection to all users*/
                BroadcastConnection();

            }
        }

        static void BroadcastConnection()
        {
            foreach (var user in _users)
            {
                foreach (var usr in _users)
                {
                    //if (user != usr)
                    //{
                    var broadcastPacket = new Net.IO.PacketBuilder();
                    broadcastPacket.WriteOpCode(1);
                    broadcastPacket.WriteMessage(usr.Username);
                    broadcastPacket.WriteMessage(usr.UID.ToString());
                    user.ClientSocket.Client.Send(broadcastPacket.GetPacketBytes());
                    //}
                }

            }
        }

        public static void BroadcastMessage(string message)
        {
            foreach (var user in _users)
            {
                var msgPacket = new Net.IO.PacketBuilder();
                msgPacket.WriteOpCode(5);
                msgPacket.WriteMessage(message);
                user.ClientSocket.Client.Send(msgPacket.GetPacketBytes());
            }
        }

        public static void BroadcastFileNotification(string senderName, string fileName)
        {
            foreach (var client in _users)
            {
                var packetBuilder = new PacketBuilder();
                packetBuilder.WriteOpCode(15);  // File notification
                packetBuilder.WriteMessage(senderName);
                packetBuilder.WriteMessage(fileName);
                client.ClientSocket.Client.Send(packetBuilder.GetPacketBytes());
            }
        }

        // ✅ THÊM method này để gửi file streaming qua opcode 19-21
        public static void SendFileToClient(Client client, string fileName)
        {
            string filePath = Path.Combine(_fileStorageFolder, fileName);

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"{DateTime.Now}: File not found: {fileName}");
                return;
            }

            try
            {
                var fileInfo = new FileInfo(filePath);

                // Gửi opcode 19: Start download
                var startPacket = new PacketBuilder();
                startPacket.WriteOpCode(19);
                startPacket.WriteMessage(fileName);
                startPacket.WriteLong(fileInfo.Length);
                client.ClientSocket.Client.Send(startPacket.GetPacketBytes());

                // Gửi file chunks qua opcode 20
                const int chunkSize = 64 * 1024; // 64KB
                byte[] buffer = new byte[chunkSize];

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize))
                {
                    int bytesRead;
                    while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        var chunkPacket = new PacketBuilder();
                        chunkPacket.WriteOpCode(20);
                        chunkPacket.WriteInt(bytesRead);
                        chunkPacket.WriteBytes(buffer, bytesRead);
                        client.ClientSocket.Client.Send(chunkPacket.GetPacketBytes());
                    }
                }

                var endPacket = new PacketBuilder();
                endPacket.WriteOpCode(21);
                client.ClientSocket.Client.Send(endPacket.GetPacketBytes());

                Console.WriteLine($"{DateTime.Now}: Sent file {fileName} to {client.Username}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}: Error sending file: {ex.Message}");
            }
        }

        public static void BroadcastDisconnect(string uid)
        {

            var disconnectedUser = _users.Where(x => x.UID.ToString() == uid).FirstOrDefault();
            _users.Remove(disconnectedUser);

            foreach (var user in _users)
            {
                var broadcastPacket = new Net.IO.PacketBuilder();
                broadcastPacket.WriteOpCode(10);
                broadcastPacket.WriteMessage(uid);
                user.ClientSocket.Client.Send(broadcastPacket.GetPacketBytes());
            }

            BroadcastMessage($"{disconnectedUser.Username} has disconnected.");
        }
    }
}