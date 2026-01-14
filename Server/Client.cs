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

        private readonly object _socketLock = new object();
        private readonly object _uploadLock = new object();
        private volatile bool _isDisconnected = false;

        private string _uploadingFileName;
        private long _uploadingFileSize;
        private MemoryStream _uploadingFileData;
        private string _uploadSender;
        private DateTime _uploadStartTime;

        private const int UPLOAD_TIMEOUT_SECONDS = 600; // 10 phút cho file lớn
        private const long MAX_FILE_SIZE = 2L * 1024 * 1024 * 1024; // 2GB 

        PacketReader _packetReader;

        public Client(TcpClient client)
        {
            ClientSocket = client;
            UID = Guid.NewGuid();

            // Tăng buffer size cho file lớn
            ClientSocket.ReceiveBufferSize = 256 * 1024; // 256KB
            ClientSocket.SendBufferSize = 256 * 1024;

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
                    byte opcode = _packetReader.ReadByte();

                    // Log để debug
                    if (opcode != 5 && opcode != 16) // Không log quá nhiều cho message và chunk
                    {
                        Console.WriteLine($"{DateTime.Now}: {Username} - Received opcode: {opcode}");
                    }

                    switch (opcode)
                    {
                        case 5: // Message
                            var message = _packetReader.ReadMessage();
                            Console.WriteLine($"{DateTime.Now}: {Username}: {message}");
                            Program.BroadcastMessage($"{DateTime.Now} {Username}: {message}");
                            break;

                        case 15: // Upload start
                            lock (_uploadLock)
                            {
                                string unsafeFileName = _packetReader.ReadMessage();
                                _uploadingFileSize = _packetReader.ReadLong();

                                Console.WriteLine($"{DateTime.Now}: {Username} upload request - Size: {_uploadingFileSize} bytes, Name: {unsafeFileName}");

                                if (_uploadingFileSize > MAX_FILE_SIZE)
                                {
                                    Console.WriteLine($"{DateTime.Now}: {Username} tried to upload file too large: {_uploadingFileSize} bytes");
                                    SendUploadError("File size exceeds maximum allowed size");
                                    break;
                                }

                                if (_uploadingFileSize <= 0)
                                {
                                    Console.WriteLine($"{DateTime.Now}: {Username} invalid file size: {_uploadingFileSize}");
                                    SendUploadError("Invalid file size");
                                    break;
                                }

                                // block lỡ bị /.../filename
                                _uploadingFileName = SanitizeFileName(unsafeFileName);

                                int initialCapacity = (int)Math.Min(_uploadingFileSize, int.MaxValue);
                                _uploadingFileData = new MemoryStream(initialCapacity);

                                _uploadSender = Username;
                                _uploadStartTime = DateTime.Now;

                                Console.WriteLine($"{DateTime.Now}: {Username} starting upload: {_uploadingFileName} ({_uploadingFileSize} bytes)");
                            }
                            break;

                        case 16: // Upload chunk
                            lock (_uploadLock)
                            {
                                if (_uploadingFileData == null)
                                {
                                    Console.WriteLine($"{DateTime.Now}: {Username} sent chunk but no upload in progress");
                                    try
                                    {
                                        int skipSize = _packetReader.ReadInt();
                                        _packetReader.ReadBytes(skipSize);
                                    }
                                    catch { }
                                    break;
                                }

                                // timeout
                                if ((DateTime.Now - _uploadStartTime).TotalSeconds > UPLOAD_TIMEOUT_SECONDS)
                                {
                                    Console.WriteLine($"{DateTime.Now}: Upload timeout: {_uploadingFileName}");
                                    CleanupUpload();
                                    SendUploadError("Upload timeout");
                                    break;
                                }

                                int chunkSize = _packetReader.ReadInt();

                                if (chunkSize <= 0 || chunkSize > 10 * 1024 * 1024) // Max 10MB per chunk
                                {
                                    Console.WriteLine($"{DateTime.Now}: Invalid chunk size: {chunkSize}");
                                    CleanupUpload();
                                    SendUploadError("Invalid chunk size");
                                    break;
                                }

                                byte[] chunk = _packetReader.ReadBytes(chunkSize);
                                _uploadingFileData.Write(chunk, 0, chunk.Length);

                                int percent = (int)((_uploadingFileData.Length * 100) / _uploadingFileSize);

                                // Log progress mỗi 10%
                                if (percent % 10 == 0 && _uploadingFileData.Length > 0)
                                {
                                    long lastLog = (_uploadingFileData.Length / (_uploadingFileSize / 10)) * (_uploadingFileSize / 10);
                                    if (Math.Abs(_uploadingFileData.Length - lastLog) < chunkSize)
                                    {
                                        Console.WriteLine($"{DateTime.Now}: {Username} upload progress: {percent}%");
                                    }
                                }

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
                            lock (_uploadLock)
                            {
                                if (_uploadingFileData != null && !string.IsNullOrEmpty(_uploadSender) && !string.IsNullOrEmpty(_uploadingFileName))
                                {
                                    Console.WriteLine($"{DateTime.Now}: {Username} completed upload: {_uploadingFileName} ({_uploadingFileData.Length} bytes)");

                                    // Kiểm tra kích thước cuối cùng
                                    if (_uploadingFileData.Length != _uploadingFileSize)
                                    {
                                        Console.WriteLine($"{DateTime.Now}: Warning - Expected {_uploadingFileSize} bytes but got {_uploadingFileData.Length} bytes");
                                    }

                                    Program.BroadcastFile(_uploadSender, _uploadingFileName, _uploadingFileData.ToArray());

                                    CleanupUpload();
                                }
                                else
                                {
                                    Console.WriteLine($"{DateTime.Now}: {Username} sent upload complete but upload was not initialized properly");
                                    CleanupUpload();
                                }
                            }
                            break;

                        case 18: // Download request
                            var requestedFileName = _packetReader.ReadMessage();
                            Console.WriteLine($"{DateTime.Now}: {Username} requesting download: {requestedFileName}");
                            Program.SendFileToClient(this, requestedFileName);
                            break;

                        default:
                            break;
                    }
                }
                catch (IOException ioEx)
                {
                    Console.WriteLine($"{DateTime.Now}: {Username} ({UID}): Network error: {ioEx.Message}");
                    Disconnect();
                    break;
                }
                catch (ObjectDisposedException)
                {
                    Console.WriteLine($"{DateTime.Now}: {Username} ({UID}): Connection closed");
                    Disconnect();
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.Now}: {Username} ({UID}): Exception: {ex.GetType().Name} - {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");

                    // Cleanup upload nếu có lỗi
                    lock (_uploadLock)
                    {
                        CleanupUpload();
                    }

                    Disconnect();
                    break;
                }
            }
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return $"file_{Guid.NewGuid()}.bin";
            }

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
            // Đã ở trong lock rồi nên không cần lock lại
            if (_uploadingFileData != null)
            {
                try
                {
                    _uploadingFileData.Dispose();
                }
                catch { }
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
                if (_isDisconnected) return;

                _isDisconnected = true;

                // Cleanup upload nếu đang trong quá trình upload
                lock (_uploadLock)
                {
                    CleanupUpload();
                }

                Console.WriteLine($"{DateTime.Now}: {Username} ({UID}) has disconnected.");

                try
                {
                    Program.BroadcastDisconnect(UID.ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.Now}: Error broadcasting disconnect: {ex.Message}");
                }

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
            }
        }
    }
}