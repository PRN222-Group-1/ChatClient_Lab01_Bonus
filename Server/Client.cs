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

        // Thêm lock để tránh race condition
        private readonly object _socketLock = new object();
        private volatile bool _isDisconnected = false;

        private string _uploadingFileName;
        private long _uploadingFileSize;
        private MemoryStream _uploadingFileData;
        private string _uploadSender;
        private DateTime _uploadStartTime;

        private const int UPLOAD_TIMEOUT_SECONDS = 300;
        private const long MAX_FILE_SIZE = 1200 * 1024 * 1024; // 1.2GB

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
            while (!_isDisconnected)
            {
                try
                {
                    var opcode = _packetReader.ReadByte();
                    switch (opcode)
                    {
                        case 5:
                            var message = _packetReader.ReadMessage();
                            Console.WriteLine($"{DateTime.Now}: {Username}: {message}");
                            Program.BroadcastMessage($"{DateTime.Now} {Username}: {message}");
                            break;

                        case 15: // Upload start
                            string unsafeFileName = _packetReader.ReadMessage();
                            _uploadingFileSize = _packetReader.ReadLong();

                            if (_uploadingFileSize > MAX_FILE_SIZE)
                            {
                                Console.WriteLine($"{DateTime.Now}: {Username} tried to upload file too large: {_uploadingFileSize} bytes");
                                SendUploadError("File size exceeds maximum allowed size");
                                break;
                            }

                            _uploadingFileName = SanitizeFileName(unsafeFileName);
                            _uploadingFileData = new MemoryStream();
                            _uploadSender = Username;
                            _uploadStartTime = DateTime.Now;

                            Console.WriteLine($"{DateTime.Now}: {Username} starting upload: {_uploadingFileName} ({_uploadingFileSize} bytes)");
                            break;

                        case 16: // Upload chunk
                            if (_uploadingFileData != null)
                            {
                                if ((DateTime.Now - _uploadStartTime).TotalSeconds > UPLOAD_TIMEOUT_SECONDS)
                                {
                                    Console.WriteLine($"{DateTime.Now}: Upload timeout: {_uploadingFileName}");
                                    CleanupUpload();
                                    SendUploadError("Upload timeout");
                                    break;
                                }

                                int chunkSize = _packetReader.ReadInt();
                                byte[] chunk = _packetReader.ReadBytes(chunkSize);
                                _uploadingFileData.Write(chunk, 0, chunk.Length);

                                int percent = (int)((_uploadingFileData.Length * 100) / _uploadingFileSize);

                                // Kiểm tra nếu vượt quá kích thước dự kiến
                                if (_uploadingFileData.Length > _uploadingFileSize)
                                {
                                    Console.WriteLine($"{DateTime.Now}: Upload size mismatch for {_uploadingFileName}");
                                    CleanupUpload();
                                    SendUploadError("File size mismatch");
                                    break;
                                }
                            }
                            break;

                        case 17: // Upload complete
                            if (_uploadingFileData != null && !string.IsNullOrEmpty(_uploadSender) && !string.IsNullOrEmpty(_uploadingFileName))
                            {
                                Console.WriteLine($"{DateTime.Now}: {Username} completed upload: {_uploadingFileName}");

                                Program.BroadcastFile(_uploadSender, _uploadingFileName, _uploadingFileData.ToArray());

                                CleanupUpload();
                            }
                            else
                            {
                                Console.WriteLine($"{DateTime.Now}: {Username} sent upload complete but upload was not initialized properly");
                                CleanupUpload();
                            }
                            break;

                        case 18: // Download request
                            var requestedFileName = _packetReader.ReadMessage();
                            Console.WriteLine($"{DateTime.Now}: {Username} requesting download: {requestedFileName}");
                            Program.SendFileToClient(this, requestedFileName);
                            break;

                        default:
                            Console.WriteLine($"{DateTime.Now}: Unknown opcode {opcode} from {Username}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.Now}: {Username} ({UID}): Exception occurred: {ex.Message}");
                    Disconnect();
                    break;
                }
            }
        }

        private string SanitizeFileName(string fileName)
        {
            string safeName = Path.GetFileName(fileName);

            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                safeName = safeName.Replace(c, '_');
            }

            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = $"file_{Guid.NewGuid()}.bin";
            }

            return safeName;
        }

        private void CleanupUpload()
        {
            if (_uploadingFileData != null)
            {
                _uploadingFileData.Dispose();
                _uploadingFileData = null;
            }
            _uploadingFileName = null;
            _uploadingFileSize = 0;
            _uploadSender = null;
        }

        private void SendUploadError(string errorMessage)
        {
            try
            {
                lock (_socketLock)
                {
                    if (_isDisconnected) return;

                    var packetBuilder = new PacketBuilder();
                    packetBuilder.WriteOpCode(19); // Error opcode
                    packetBuilder.WriteMessage(errorMessage);
                    ClientSocket.Client.Send(packetBuilder.GetPacketBytes());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}: Failed to send error to {Username}: {ex.Message}");
            }
        }

        private void Disconnect()
        {
            lock (_socketLock)
            {
                if (_isDisconnected) return; // Tránh disconnect nhiều lần

                _isDisconnected = true;

                CleanupUpload();

                Console.WriteLine($"{DateTime.Now}: {Username} ({UID}) has disconnected.");
                Program.BroadcastDisconnect(UID.ToString());

                try
                {
                    ClientSocket?.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.Now}: Error closing socket for {Username}: {ex.Message}");
                }
            }
        }

        public void SendFileUploaded(string username, string fileName)
        {
            try
            {
                lock (_socketLock)
                {
                    if (_isDisconnected) return;

                    var packetBuilder = new PacketBuilder();
                    packetBuilder.WriteOpCode(18);
                    packetBuilder.WriteMessage(username);
                    packetBuilder.WriteMessage(fileName);
                    ClientSocket.Client.Send(packetBuilder.GetPacketBytes());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now}: Failed to send file notification to {Username}: {ex.Message}");
                Disconnect();
            }
        }

    }
}