# ChatApp
ChatApp is a simple TCP‑based chat client and server built with **C# and WPF**, following the MVVM pattern and based on Payload’s “How To Create a Chat App and Server” tutorial.

## Features

- Real‑time messaging between multiple clients over TCP using `TcpClient` and `TcpListener`.
- Custom packet system with opcodes, a `PacketBuilder` for writing, and a `PacketReader` for reading binary data over the network stream.
- User management on the server, including unique IDs (GUID), connected‑user list, and broadcast of join/leave events to all clients.
- WPF client using MVVM (Model, View, ViewModel) with `RelayCommand`, `ObservableCollection`, and data binding for users and messages.
- Basic chat UI: user list, message history, input box, and send button; can be combined with the author’s “modern chat UI” tutorial for a more polished design.

## Technology stack

- .NET 9. / C#  
- WPF (XAML) for the desktop UI  
- Raw TCP sockets (`TcpClient`, `TcpListener`).

## Project structure

- `ChatClient` – WPF client application (MVVM folders: Model, View, ViewModel, Core/RelayCommand, Net/IO for networking and packet classes).
- `ChatServer` – console server application that accepts multiple clients, reads packets, and broadcasts connection, message, and disconnect events.

Tutorials and References:
[1] How To Create a Chat App and Server Tutorial WPF C#, YouTube, 4.3 years ago. [Video]. Available: https://youtu.be/I-Xmp-mulz4
