using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using ChatClient.Net.IO;

namespace ChatClient.Net
{
    public class Server
    {
        private const int ChunkSize = 64 * 1024;
        private readonly TcpClient _client;

        public PacketReader packetReader;

        public event Action connectedEvent;
        public event Action messageReceivedEvent;
        public event Action userDisconnectedEvent;
        public event Action<string, string> fileReceivedEvent;

        public event Action<string, int> downloadProgressEvent;
        public event Action<string> downloadCompleteEvent;

        // ===== DOWNLOAD STATE =====
        private string _downloadSavePath;
        private string _currentDownloadFile;
        private long _expectedDownloadSize;
        private long _downloadedBytes;
        private FileStream _downloadStream;

        public Server()
        {
            _client = new TcpClient();
        }

        public void ConnectToServer(string username)
        {
            if (_client.Connected) return;

            _client.Connect("127.0.0.1", 9000);
            packetReader = new PacketReader(_client.GetStream());

            var connectPacket = new PacketBuilder();
            connectPacket.WriteOpCode(0);
            connectPacket.WriteMessage(username);
            _client.Client.Send(connectPacket.GetPacketBytes());

            ReadPackets();
        }

        public void SendMessageToServer(string message)
        {
            var packet = new PacketBuilder();
            packet.WriteOpCode(5);
            packet.WriteMessage(message);
            _client.Client.Send(packet.GetPacketBytes());
        }

        public async Task SendFileToServer(string filePath, Action<int> onProgress)
        {
            var fileInfo = new FileInfo(filePath);

            var startPacket = new PacketBuilder();
            startPacket.WriteOpCode(15);
            startPacket.WriteMessage(fileInfo.Name);
            startPacket.WriteLong(fileInfo.Length);
            _client.Client.Send(startPacket.GetPacketBytes());

            await SendFileChunks(filePath, fileInfo.Length, onProgress);

            var endPacket = new PacketBuilder();
            endPacket.WriteOpCode(17);
            _client.Client.Send(endPacket.GetPacketBytes());
        }

        // ===== DOWNLOAD REQUEST =====
        public void RequestDownloadFile(string fileName, string savePath)
        {
            _downloadSavePath = savePath;

            var packet = new PacketBuilder();
            packet.WriteOpCode(18);
            packet.WriteMessage(fileName);
            _client.Client.Send(packet.GetPacketBytes());
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

                            case 25:
                                {
                                    var sender = packetReader.ReadMessage();
                                    var fileName = packetReader.ReadMessage();
                                    fileReceivedEvent?.Invoke(sender, fileName);
                                    break;
                                }

                            // ===== DOWNLOAD START =====
                            case 30:
                                {
                                    _currentDownloadFile = packetReader.ReadMessage();
                                    _expectedDownloadSize = packetReader.ReadLong();
                                    _downloadedBytes = 0;

                                    var directory = Path.GetDirectoryName(_downloadSavePath);
                                    var finalPath = Path.Combine(directory, _currentDownloadFile);

                                    _downloadStream = new FileStream(
                                        finalPath,
                                        FileMode.Create,
                                        FileAccess.Write,
                                        FileShare.None,
                                        ChunkSize
                                    );

                                    break;
                                }


                            // ===== DOWNLOAD CHUNK =====
                            case 31:
                                {
                                    int size = packetReader.ReadInt();
                                    byte[] data = packetReader.ReadBytes(size);

                                    _downloadStream.Write(data, 0, data.Length);
                                    _downloadedBytes += data.Length;

                                    int percent = (int)(_downloadedBytes * 100 / _expectedDownloadSize);
                                    downloadProgressEvent?.Invoke(_currentDownloadFile, percent);
                                    break;
                                }

                            // ===== DOWNLOAD COMPLETE =====
                            case 32:
                                {
                                    _downloadStream.Flush();
                                    _downloadStream.Close();
                                    _downloadStream = null;

                                    downloadCompleteEvent?.Invoke(_currentDownloadFile);

                                    _currentDownloadFile = null;
                                    _downloadSavePath = null;
                                    _expectedDownloadSize = 0;
                                    _downloadedBytes = 0;
                                    break;
                                }

                            default:
                                break;
                        }
                    }
                }
                catch
                {
                    _client.Close();
                }
            });
        }

        private async Task SendFileChunks(string filePath, long totalSize, Action<int> onProgress)
        {
            long sentBytes = 0;
            var buffer = new byte[ChunkSize];

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);

            int bytesRead;
            while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                var packet = new PacketBuilder();
                packet.WriteOpCode(16);
                packet.WriteInt(bytesRead);
                packet.WriteBytes(buffer, bytesRead);
                _client.Client.Send(packet.GetPacketBytes());

                sentBytes += bytesRead;
                onProgress?.Invoke((int)(sentBytes * 100 / totalSize));
            }
        }
    }
}
