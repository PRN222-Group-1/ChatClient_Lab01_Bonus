using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
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
        public event Action<string, string> fileUploadedEvent;

        private string _downloadFilePath;
        private string _downloadFileName;
        private long _downloadExpectedSize;
        private long _downloadReceivedBytes;
        private FileStream _downloadFileStream;

        public event Action<string, int> downloadProgressEvent;  // fileName, percent
        public event Action<string> downloadCompleteEvent;  // fileName



        public Server()
        {
            _client = new TcpClient();
        }

        public void ConnectToServer(string username)
        {
            if (_client.Connected) return;

            _client.Connect("127.0.0.1", 9000);
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

        public void SendMessageToServer(string message)
        {
            try
            {
                var messagePacket = new PacketBuilder();
                messagePacket.WriteOpCode(5);
                messagePacket.WriteMessage(message);
                _client.Client.Send(messagePacket.GetPacketBytes());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }

        public async Task SendFileToServer(string filePath, Action<int> onProgress)
        {
            try
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending file: {ex.Message}");
            }
        }

        public void RequestDownloadFile(string fileName)
        {
            try
            {
                var downloadPacket = new PacketBuilder();
                downloadPacket.WriteOpCode(18);
                downloadPacket.WriteMessage(fileName);
                _client.Client.Send(downloadPacket.GetPacketBytes());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error requesting file: {ex.Message}");
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
                            case 15:
                                var sender = packetReader.ReadMessage();
                                var fileName = packetReader.ReadMessage();
                                fileReceivedEvent?.Invoke(sender, fileName);
                                break;
                            case 16:
                                int size = packetReader.ReadInt();
                                packetReader.ReadBytes(size);
                                break;
                            case 17:
                                break;
                            case 19:  // Download file start
                                _downloadFileName = packetReader.ReadMessage();
                                _downloadExpectedSize = packetReader.ReadLong();
                                _downloadReceivedBytes = 0;

                                if (!string.IsNullOrEmpty(_downloadFilePath))
                                {
                                    _downloadFileStream = new FileStream(
                                        _downloadFilePath,
                                        FileMode.Create,
                                        FileAccess.Write,
                                        FileShare.None,
                                        64 * 1024
                                    );
                                    Console.WriteLine($"Started downloading {_downloadFileName}");
                                }
                                break;

                            case 20:
                                if (_downloadFileStream != null)
                                {
                                    int sizeFile = packetReader.ReadInt();
                                    byte[] data = packetReader.ReadBytes(sizeFile);
                                    _downloadFileStream.Write(data, 0, data.Length);
                                    _downloadReceivedBytes += data.Length;

                                    int percent = (int)((_downloadReceivedBytes * 100) / _downloadExpectedSize);
                                    downloadProgressEvent?.Invoke(_downloadFileName, percent); 
                                }
                                break;

                            case 21: 
                                _downloadFileStream?.Flush();
                                _downloadFileStream?.Close();
                                _downloadFileStream = null;

                                Console.WriteLine($"Download complete: {_downloadFileName} ({_downloadReceivedBytes} bytes)");
                                downloadCompleteEvent?.Invoke(_downloadFileName);

                                _downloadFileName = null;
                                _downloadFilePath = null;
                                _downloadExpectedSize = 0;
                                _downloadReceivedBytes = 0;
                                break;


                            default:
                                Console.WriteLine($"Unknown opcode received: {opcode}");
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

        public void SetDownloadPath(string filePath)
        {
            _downloadFilePath = filePath;
        }


        private async Task SendFileChunks(string filePath, long totalSize, Action<int> onProgress)
        {
            long sentBytes = 0;
            var buffer = new byte[ChunkSize];

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize, useAsync: true);

            int bytesRead;
            while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                var chunkPacket = new PacketBuilder();
                chunkPacket.WriteOpCode(16);
                chunkPacket.WriteInt(bytesRead);
                chunkPacket.WriteBytes(buffer, bytesRead);
                _client.Client.Send(chunkPacket.GetPacketBytes());

                sentBytes += bytesRead;
                int percent = (int)((sentBytes * 100) / totalSize);
                onProgress?.Invoke(percent);

                await Task.Delay(1);
            }
        }
    }
}