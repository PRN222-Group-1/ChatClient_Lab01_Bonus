using System;
using System.Net.Sockets;
using ChatServer.Net.IO;

namespace ChatServer
{
    class Program
    {
        static TcpListener _listener;
        static List<Client> _users;
        static void Main(string[] args)
        {
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

        public static void BroadcastFileStart(string sender, string fileName, long fileSize)
        {
            foreach (var user in _users)
            {
                var packet = new PacketBuilder();
                packet.WriteOpCode(15);
                packet.WriteMessage(sender);
                packet.WriteMessage(fileName);
                packet.WriteLong(fileSize);

                user.ClientSocket.Client.Send(packet.GetPacketBytes());
            }
        }

        public static void BroadcastFileUploaded(string username, string fileName)
        {
            foreach (var user in _users)
            {
                try
                {
                    user.SendFileUploaded(username, fileName);
                }
                catch
                {
                }
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