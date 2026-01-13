using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ChatServer.Net.IO;

namespace ChatServer
{
    public class Client
    {
        public string Username { get; set; }
        public Guid UID { get; set; }
        public TcpClient ClientSocket { get; set; }

        public string _currentFileName;
        public long _expectedFileSize;
        public long _receivedBytes;

        FileStream _fs;

        PacketReader _packetReader;
        public Client(TcpClient client)
        {
            ClientSocket = client;
            UID = Guid.NewGuid();
            _packetReader = new PacketReader(ClientSocket.GetStream());

            var opcode = _packetReader.ReadByte();
            Username = _packetReader.ReadMessage();

            Console.WriteLine($"{DateTime.Now}: Client has connected with the username: {Username}");

            Task.Run(() => Process());
        }

        void Process()
        {
            while (true)
            {
                try
                {
                    var opcode = _packetReader.ReadByte();
                    switch (opcode)
                    {
                        case 5:
                            var message = _packetReader.ReadMessage();
                            Console.WriteLine($"{DateTime.Now}: {Username}: {message}");
                            Program.BroadcastMessage($"{DateTime.Now} {Username}:  {message}");
                            break;
                        case 15:
                            HandleFileStart();

                            Program.BroadcastFileStart(
                                Username,
                                _currentFileName,
                                _expectedFileSize
                            );
                            break;
                        case 16:
                            int chunkSize = _packetReader.ReadInt();
                            byte[] buffer = _packetReader.ReadBytes(chunkSize);

                            _fs.Write(buffer, 0, buffer.Length);
                            _receivedBytes += buffer.Length;

                            break;
                        case 17:
                            HandleFileEnd();
                            Program.BroadcastFileUploaded(
                                Username,
                                _currentFileName
                            );
                            break;
                        case 18:
                            string requestedFileName = _packetReader.ReadMessage();

                            SendFileDownload(requestedFileName);
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine($"{UID.ToString()}: has disconnected.");
                    Program.BroadcastDisconnect(UID.ToString());
                    ClientSocket.Close();
                    break;
                }

            }
        }

        public void SendFileUploaded(string username, string fileName)
        {
            var packetBuilder = new PacketBuilder();
            packetBuilder.WriteOpCode(18);
            packetBuilder.WriteMessage(username);
            packetBuilder.WriteMessage(fileName);
            ClientSocket.Client.Send(packetBuilder.GetPacketBytes());
        }

        private void SendFileDownload(string fileName)
        {
            try
            {
                string filePath = Path.Combine("Uploads", fileName);

                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"File not found: {fileName}");
                    return;
                }

                var fileInfo = new FileInfo(filePath);

                var startPacket = new PacketBuilder();
                startPacket.WriteOpCode(19); // File download start opcode
                startPacket.WriteMessage(fileName);
                startPacket.WriteLong(fileInfo.Length);
                ClientSocket.Client.Send(startPacket.GetPacketBytes());

                const int chunkSize = 64 * 1024;
                byte[] buffer = new byte[chunkSize];

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    int bytesRead;
                    while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        var chunkPacket = new PacketBuilder();
                        chunkPacket.WriteOpCode(20);
                        chunkPacket.WriteInt(bytesRead);
                        chunkPacket.WriteBytes(buffer, bytesRead);
                        ClientSocket.Client.Send(chunkPacket.GetPacketBytes());
                    }
                }

                var endPacket = new PacketBuilder();
                endPacket.WriteOpCode(21); // File end opcode
                ClientSocket.Client.Send(endPacket.GetPacketBytes());

                Console.WriteLine($"Sent file {fileName} to {Username}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending file: {ex.Message}");
            }
        }


        public void HandleFileStart()
        {
            _currentFileName = _packetReader.ReadMessage();
            _expectedFileSize = _packetReader.ReadLong();
            _receivedBytes = 0;

            Directory.CreateDirectory("Uploads");

            string path = Path.Combine("Uploads", _currentFileName);

            _fs = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: false);

            Console.WriteLine(
                $"{Username} started uploading {_currentFileName} ({_expectedFileSize} bytes)"
            );
        }

        public void HandleFileEnd()
        {
            _fs?.Flush();
            _fs?.Close();
            _fs = null;

            Console.WriteLine(
                $"{Username} finished uploading {_currentFileName}"
            );

            if (_receivedBytes != _expectedFileSize)
            {
                Console.WriteLine("File size mismatch");
            }

            _currentFileName = null;
            _expectedFileSize = 0;
            _receivedBytes = 0;
        }

    }
}
