# ChatApp

ChatApp là một ứng dụng chat client-server đơn giản sử dụng giao thức TCP, được xây dựng bằng **C# và WPF**, tuân theo mẫu thiết kế MVVM và dựa trên hướng dẫn "How To Create a Chat App and Server" của Payload.

## Tính năng

- Nhắn tin thời gian thực giữa nhiều client qua TCP sử dụng `TcpClient` và `TcpListener`.
- Hệ thống packet tùy chỉnh với opcode, `PacketBuilder` để ghi dữ liệu và `PacketReader` để đọc dữ liệu nhị phân qua network stream.
- Quản lý người dùng trên server, bao gồm ID duy nhất (GUID), danh sách người dùng đã kết nối, và broadcast sự kiện tham gia/rời đi cho tất cả client.
- WPF client sử dụng MVVM (Model, View, ViewModel) với `RelayCommand`, `ObservableCollection`, và data binding cho danh sách người dùng và tin nhắn.
- Giao diện chat cơ bản: danh sách người dùng, lịch sử tin nhắn, ô nhập liệu, và nút gửi.

## Công nghệ sử dụng

- .NET 9 / C#
- WPF (XAML) cho giao diện desktop
- Raw TCP sockets (`TcpClient`, `TcpListener`)
- Mẫu thiết kế MVVM với data binding

## Cấu trúc dự án

- **`ChatClient`** – Ứng dụng WPF client (thư mục MVVM: Model, View, ViewModel, Core/RelayCommand, Net/IO cho networking và packet classes).
- **`ChatServer`** – Ứng dụng console server chấp nhận nhiều client, đọc packet, và broadcast sự kiện kết nối, tin nhắn, ngắt kết nối.

---

## Tổng Quan Kiến Trúc

### Kiến trúc Client (Mẫu MVVM)

```
┌─────────────────────────────────────────┐
│           WPF CLIENT                    │
├─────────────────────────────────────────┤
│                                         │
│  ┌──────────┐    ┌──────────────┐     │
│  │   VIEW   │◄──►│  VIEWMODEL   │     │
│  │  (XAML)  │    │   (Logic)    │     │
│  └──────────┘    └──────────────┘     │
│       ▲                 ▲              │
│       │                 │              │
│  Data Binding      Commands            │
│                         │              │
│              ┌──────────▼────────┐     │
│              │   Server.cs       │     │
│              │  (TCP Client)     │     │
│              └──────────┬────────┘     │
└─────────────────────────┼──────────────┘
                          │
                    TCP Socket
                          │
┌─────────────────────────▼──────────────┐
│        CHAT SERVER (Console)           │
│  ┌──────────────┐   ┌──────────────┐  │
│  │  Program.cs  │◄─►│  Client.cs   │  │
│  │   (Main)     │   │(Mỗi client)  │  │
│  └──────────────┘   └──────────────┘  │
└────────────────────────────────────────┘
```

### Phân Chia Trách Nhiệm

| Layer | File | Trách nhiệm |
|-------|------|-------------|
| **View** | `MainWindow.xaml` | Hiển thị UI, data binding, nhận input từ user |
| **ViewModel** | `MainViewModel.cs` | Business logic, quản lý state, commands |
| **Network** | `Server.cs` | Kết nối TCP, xây dựng/đọc packet |
| **Server** | `Program.cs` | Chấp nhận kết nối, quản lý danh sách client |
| **Server** | `Client.cs` | Xử lý giao tiếp với từng client riêng lẻ |

---

## Giao Thức Truyền Thông

### Hệ Thống Opcode

Ứng dụng sử dụng hệ thống **nhóm opcode theo chức năng** để dễ mở rộng và bảo trì:

| Opcode | Mục đích | Hướng | Dữ liệu |
|--------|----------|-------|---------|
| **0** | Kết nối ban đầu | Client → Server | Username |
| **1** | Broadcast user đã kết nối | Server → Clients | Username, UID |
| **5** | Tin nhắn chat | Hai chiều | Nội dung tin nhắn |
| **10** | User ngắt kết nối | Server → Clients | UID |

#### Tại sao dùng 0, 1, 5, 10 thay vì 1, 2, 3, 4?

Các opcode được nhóm theo chức năng để **dễ mở rộng trong tương lai**:

```
0-9:    Nhóm Kết nối
├── 0:  Kết nối ban đầu
├── 1:  Broadcast kết nối
├── 2-4: Dành cho tính năng xác thực sau này

5-14:   Nhóm Nhắn tin  
├── 5:  Tin nhắn công khai
├── 6-9: Dành cho tin nhắn riêng tư, truyền file

10-19:  Nhóm Ngắt kết nối
├── 10: User ngắt kết nối
├── 11-14: Dành cho tính năng kick/ban
```

**Lợi ích:**
- Dễ thêm tính năng mới mà không cần cơ cấu lại
- Code tự giải thích (opcode cho biết nhóm chức năng)
- Tương tự HTTP status code (1xx, 2xx, 3xx, v.v.)
- Tránh breaking changes khi mở rộng

### Cấu Trúc Packet

```
┌─────────┬──────────────┬─────────────────┐
│ 1 byte  │   4 bytes    │    N bytes      │
│ Opcode  │ Độ dài msg   │  Dữ liệu msg    │
└─────────┴──────────────┴─────────────────┘
```

**PacketBuilder** ghi dữ liệu:
```csharp
var packet = new PacketBuilder();
packet.WriteOpCode(5);              // 1 byte
packet.WriteMessage("Xin chào");    // 4 bytes (độ dài) + N bytes (dữ liệu)
client.Send(packet.GetPacketBytes());
```

**PacketReader** đọc dữ liệu:
```csharp
var opcode = reader.ReadByte();     // Đọc 1 byte
var message = reader.ReadMessage(); // Đọc 4 bytes độ dài, sau đó đọc message
```

---

## Luồng Giao Tiếp Chi Tiết

### Luồng 1: User Kết Nối

```
┌─────────────────────────────────────────────────────────────┐
│ 1. CLIENT (MainWindow.xaml)                                 │
│    User nhập username: "Alice"                              │
│    Click nút "Connect"                                      │
└────────────────────┬────────────────────────────────────────┘
                     │ Command Binding
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. VIEWMODEL (MainViewModel.cs)                             │
│    ConnectToServerCommand được kích hoạt                    │
│    Gọi: _server.ConnectToServer("Alice")                    │
└────────────────────┬────────────────────────────────────────┘
                     │ Method Call
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. CLIENT NETWORK (Server.cs)                               │
│    TcpClient kết nối tới server IP:PORT                     │
│    Xây dựng packet: [Opcode=0]["Alice"]                     │
│    Gửi packet qua TCP                                       │
│    Bắt đầu ReadPackets() trong background thread            │
└────────────────────┬────────────────────────────────────────┘
                     │ TCP Socket
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. SERVER (Program.cs)                                      │
│    AcceptTcpClient() chấp nhận kết nối                      │
│    Tạo object Client("Alice") mới                           │
│    Thêm vào danh sách _users                                │
│    Gọi BroadcastConnection()                                │
└────────────────────┬────────────────────────────────────────┘
                     │ Constructor Call
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ 5. SERVER (Client.cs)                                       │
│    Đọc packet: opcode=0, username="Alice"                   │
│    Tạo UID (Guid) duy nhất                                  │
│    Spawn Task.Run(() => Process())                          │
│    → Background thread liên tục đọc messages                │
└─────────────────────────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ 6. SERVER BROADCAST (Program.cs)                            │
│    BroadcastConnection():                                   │
│    Với mỗi user hiện có:                                    │
│      Xây dựng packet: [Opcode=1]["Alice"][UID]              │
│      Gửi cho tất cả client KHÁC                             │
└────────────────────┬────────────────────────────────────────┘
                     │ TCP Socket
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ 7. CÁC CLIENT KHÁC                                          │
│    ReadPackets() nhận opcode=1                              │
│    Kích hoạt connectedEvent                                 │
│    MainViewModel.UserConnected():                           │
│      Tạo object User                                        │
│      Thêm vào Users ObservableCollection                    │
│      UI tự động cập nhật qua data binding                   │
└─────────────────────────────────────────────────────────────┘
```

### Luồng 2: Gửi Tin Nhắn

```
┌─────────────────────────────────────────────────────────────┐
│ 1. CLIENT VIEW                                              │
│    User gõ: "Xin chào mọi người!"                           │
│    Click nút Send                                           │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. CLIENT VIEWMODEL                                         │
│    SendMessageCommand thực thi                              │
│    _server.SendMessageToServer("Xin chào mọi người!")       │
│    Xóa nội dung Message property                            │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. CLIENT NETWORK                                           │
│    PacketBuilder tạo: [Opcode=5]["Xin chào mọi người!"]     │
│    Gửi qua TCP socket                                       │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. SERVER (Client.cs - thread của Alice)                    │
│    Vòng lặp Process() đọc: opcode=5                         │
│    Đọc message: "Xin chào mọi người!"                       │
│    Ghi log: "Alice: Xin chào mọi người!"                    │
│    Gọi Program.BroadcastMessage("Xin chào mọi người!")      │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ 5. SERVER BROADCAST (Program.cs)                            │
│    BroadcastMessage():                                      │
│    Với MỌI user trong _users (bao gồm người gửi):           │
│      Xây dựng packet: [Opcode=5]["Xin chào mọi người!"]     │
│      Gửi tới client                                         │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ 6. TẤT CẢ CLIENTS                                           │
│    ReadPackets() nhận opcode=5                              │
│    Kích hoạt messageReceivedEvent                           │
│    MainViewModel.MessageReceived():                         │
│      Đọc nội dung tin nhắn                                  │
│      Messages.Add("Xin chào mọi người!")                    │
│      UI cập nhật ngay lập tức                               │
└─────────────────────────────────────────────────────────────┘
```

### Luồng 3: User Ngắt Kết Nối
```
┌─────────────────────────────────────────────────────────────┐
│ 1. CLIENT                                                   │
│    User đóng ứng dụng hoặc mất kết nối mạng                 │
│    TCP socket bị đóng                                       │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. SERVER (Client.cs - thread của Alice)                    │
│    Vòng lặp Process():                                      │
│      ReadByte() ném exception (stream đã đóng)              │
│    Khối catch:                                              │
│      Ghi log: "Alice đã ngắt kết nối"                       │
│      Program.BroadcastDisconnect(UID)                       │
│      ClientSocket.Close()                                   │
│      Thread kết thúc                                        │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. SERVER (Program.cs)                                      │
│    BroadcastDisconnect(UID):                                │
│      Tìm user theo UID                                      │
│      Xóa khỏi danh sách _users                              │
│      Với mỗi user còn lại:                                  │
│        Xây dựng packet: [Opcode=10][UID]                    │
│        Gửi tới client                                       │
│      BroadcastMessage("Alice đã ngắt kết nối")              │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. CÁC CLIENT KHÁC                                          │
│    ReadPackets() nhận opcode=10                             │
│    Kích hoạt userDisconnectedEvent                          │
│    MainViewModel.UserDisconnected():                        │
│      Đọc UID                                                │
│      Tìm user trong Users collection                        │
│      Xóa user                                               │
│      UI cập nhật (user biến mất khỏi danh sách)             │
└─────────────────────────────────────────────────────────────┘
```

---

## Kiến Trúc Đa Luồng (Multi-Threading)

### Mô Hình Thread của Server

```
Main Thread (Program.cs)
│
├─ while(true) { AcceptTcpClient() }  ← Chờ kết nối (blocking)
│
├─ Client 1 kết nối ──────────► Task.Run(() => Process())
│                                │
├─ Client 2 kết nối ──────────► Task.Run(() => Process())
│                                │
└─ Client 3 kết nối ──────────► Task.Run(() => Process())
                                 │
                ┌────────────────┴────────────────┐
                │                                 │
                ▼                                 ▼
        Thread riêng của Alice          Thread riêng của Bob
        while(true) {                   while(true) {
          Đọc messages                    Đọc messages
          Gọi Broadcast()                 Gọi Broadcast()
        }                               }
```

**Đặc điểm:**
- Mỗi client có **1 thread riêng** để đọc messages liên tục
- Main thread chỉ chịu trách nhiệm accept connections
- `_users` list là **shared state** giữa các threads

### Thread Model của Client

```
Main Thread (UI Thread)
│
├─ MainWindow UI rendering
│
└─ Task.Run(() => ReadPackets())  ← Background thread
                │
                ▼
        while(true) {
          Đọc opcode
          ├─ opcode=1 → Trigger connectedEvent
          ├─ opcode=5 → Trigger messageReceivedEvent  
          └─ opcode=10 → Trigger userDisconnectedEvent
        }
                │
                ▼
        Application.Dispatcher.Invoke()  ← Chuyển về UI thread
        ├─ Users.Add()
        └─ Messages.Add()
```

**UI Thread Safety:**
- Background thread đọc data từ network
- `Dispatcher.Invoke()` đảm bảo update UI trên main thread
- `ObservableCollection` tự động notify UI khi có thay đổi

---

## Chi Tiết Kỹ Thuật

### 1. Tại sao dùng Task.Run thay vì Thread?

```csharp
// Cách hiện tại - Modern approach
Task.Run(() => Process());

// Cách cũ - Legacy approach
new Thread(Process).Start();
```

**Lợi ích của Task.Run:**
- Sử dụng ThreadPool hiệu quả hơn
- Dễ quản lý exception
- Hỗ trợ async/await
- Tự động cleanup khi hoàn thành

### 2. Tại sao BroadcastMessage là static method?

```csharp
// Trong Client.cs (instance method)
Program.BroadcastMessage(message);  // ← Gọi static method

// Trong Program.cs
public static void BroadcastMessage(string message)
{
    foreach (var user in _users)  // ← Truy cập static _users
    {
        // Gửi message
    }
}
```

**Lý do:**
- Mỗi client chạy trong thread riêng
- Cần truy cập **shared state** (`_users` list)
- Static methods giúp truy cập từ bất kỳ thread nào
- Tránh phải pass reference qua constructor

### 3. Xử lý Exception khi Client ngắt kết nối

```csharp
void Process()
{
    while(true)
    {
        try
        {
            var opcode = _packetReader.ReadByte();
            // Xử lý messages...
        }
        catch (Exception ex)
        {
            // Stream đã đóng → Client disconnected
            Console.WriteLine($"{UID}: has disconnected");
            Program.BroadcastDisconnect(UID.ToString());
            ClientSocket.Close();
            throw;  // Exit thread
        }
    }
}
```

**Flow:**
1. Client đóng ứng dụng → TCP socket close
2. `ReadByte()` ném exception
3. Catch block cleanup và notify
4. `throw` để exit thread

### 4. Data Binding trong WPF MVVM

```csharp
// ViewModel
public ObservableCollection<string> Messages { get; set; }

private void MessageReceived()
{
    var message = _server.packetReader.ReadMessage();
    
    // Phải update trên UI thread
    Application.Current.Dispatcher.Invoke(() =>
    {
        Messages.Add(message);  // ← UI tự động update
    });
}
```

```xaml
<!-- View - XAML -->
<ListBox ItemsSource="{Binding Messages}" />
```

**Cơ chế:**
- `ObservableCollection` implement `INotifyCollectionChanged`
- Khi `Add()` được gọi, event được raised
- WPF tự động refresh UI
- Data binding 2-way giữa View và ViewModel

---

## Câu Hỏi Thường Gặp (FAQ)

### Q1: Tại sao không dùng SignalR thay vì raw TCP?

**Trả lời:** Dự án này nhằm mục đích học tập:
- Hiểu rõ cách TCP socket hoạt động
- Tự xây dựng protocol từ đầu
- Kiểm soát hoàn toàn packet format
- SignalR che giấu quá nhiều chi tiết low-level

### Q2: Server có thread-safe không?

**Trả lời:** Không hoàn toàn:
- `_users` list được truy cập từ nhiều threads
- Không có lock/mutex → có thể race condition
- Chấp nhận được cho demo/learning project
- Production code cần dùng `ConcurrentBag` hoặc `lock`

### Q3: Làm sao thêm private message?

**Trả lời:**
```csharp
// Thêm opcode mới
const byte OPCODE_PRIVATE_MESSAGE = 6;

// Server
case 6:
    var targetUID = _packetReader.ReadMessage();
    var privateMsg = _packetReader.ReadMessage();
    SendToSpecificUser(targetUID, privateMsg);
    break;
```

### Q4: Có thể deploy server lên cloud không?

**Trả lời:** Có, nhưng cần:
- Đổi IP hardcode thành config
- Mở port trên firewall
- Sử dụng static IP hoặc domain
- Cân nhắc dùng SSL/TLS cho security

---

## Tài Liệu Tham Khảo

- [How To Create a Chat App and Server Tutorial WPF C#](https://youtu.be/I-Xmp-mulz4) - Payload (YouTube)
- [Microsoft Docs - TcpClient Class](https://docs.microsoft.com/dotnet/api/system.net.sockets.tcpclient)
- [Microsoft Docs - MVVM Pattern](https://docs.microsoft.com/dotnet/architecture/maui/mvvm)
- [TCP Protocol Basics](https://en.wikipedia.org/wiki/Transmission_Control_Protocol)

---

## License

Dự án này được tạo ra cho mục đích học tập và không có license cụ thể.

---

## Đóng Góp

Mọi đóng góp đều được chào đón! Hãy tạo Pull Request hoặc Issue nếu bạn có ý tưởng cải thiện.
