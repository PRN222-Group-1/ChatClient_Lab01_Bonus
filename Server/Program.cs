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
        static string _fileStorageFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerFiles");

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

        public static void BroadcastFile(string sender, string fileName, byte[] fileData)
        {

            if (_currentCacheSize + fileData.Length > MAX_CACHE_SIZE)
            {
                Console.WriteLine("Cache full, clearing old files...");
                _fileStorage.Clear();
                _currentCacheSize = 0;
            }

            string uniqueFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(fileName)}";

            _fileStorage[uniqueFileName] = fileData;

            _currentCacheSize += fileData.Length;

            try
            {
                string filePath = Path.Combine(_fileStorageFolder, fileName);
                File.WriteAllBytes(filePath, fileData);
                Console.WriteLine($"File saved to disk: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving file: {ex.Message}");
            }

            foreach (var user in _users)
            {
                try
                {
                    var filePacket = new Net.IO.PacketBuilder();
                    filePacket.WriteOpCode(15); // File received notification
                    filePacket.WriteMessage(sender);
                    filePacket.WriteMessage(uniqueFileName);
                    user.ClientSocket.Client.Send(filePacket.GetPacketBytes());

                    Console.WriteLine($"Broadcast file notification to {user.Username}: {fileName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error broadcasting to {user.Username}: {ex.Message}");
                }
            }
        }
        public static void SendFileToClient(Client recipient, string fileName)
        {
            if (!_fileStorage.TryGetValue(fileName, out byte[] fileData))
            {
                string filePath = Path.Combine(_fileStorageFolder, fileName);
                if (File.Exists(filePath))
                {
                    fileData = File.ReadAllBytes(filePath);
                    _fileStorage[fileName] = fileData;
                }
                else
                {
                    Console.WriteLine($"File not found: {fileName}");
                    return;
                }
            }

            try
            {
                var startPacket = new Net.IO.PacketBuilder();
                startPacket.WriteOpCode(19);
                startPacket.WriteMessage(fileName);
                startPacket.WriteLong(fileData.Length);
                recipient.ClientSocket.Client.Send(startPacket.GetPacketBytes());

                // Send file in chunks
                int chunkSize = 64 * 1024;
                int totalChunks = (int)Math.Ceiling(fileData.Length / (double)chunkSize);

                for (int i = 0; i < totalChunks; i++)
                {
                    int offset = i * chunkSize;
                    int size = Math.Min(chunkSize, fileData.Length - offset);

                    var chunkPacket = new Net.IO.PacketBuilder();
                    chunkPacket.WriteOpCode(20);
                    chunkPacket.WriteInt(size);
                    chunkPacket.WriteBytes(fileData, offset, size);
                    recipient.ClientSocket.Client.Send(chunkPacket.GetPacketBytes());

                    Thread.Sleep(1);
                }

                var completePacket = new Net.IO.PacketBuilder();
                completePacket.WriteOpCode(21);
                recipient.ClientSocket.Client.Send(completePacket.GetPacketBytes());

                Console.WriteLine($"File sent to {recipient.Username}: {fileName} ({fileData.Length} bytes)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending file: {ex.Message}");
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