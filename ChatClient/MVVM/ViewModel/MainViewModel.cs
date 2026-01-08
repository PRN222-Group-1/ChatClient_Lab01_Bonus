using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ChatClient.MVVM.Core;
using ChatClient.MVVM.Model;
using ChatClient.Net;

namespace ChatClient.MVVM.ViewModel
{
    public class MainViewModel
    {
        public ObservableCollection<User> Users { get; set; }
        public ObservableCollection<string> Messages { get; set; }
        public RelayCommand ConnectToServerCommand { get; set; }
        public RelayCommand SendMessageCommand { get; set; }

        public string Username { get; set; }
        public string Message { get; set; }

        private Server _server;

        public MainViewModel()
        {
            Users = new ObservableCollection<User>();
            Messages = new ObservableCollection<string>();
            _server = new Server();
            _server.connectedEvent += UserConnected;
            _server.messageReceivedEvent += MessageReceived;
            _server.userDisconnectedEvent += UserDisconnected;
            //Use Relay to unable the button after clicked once
            ConnectToServerCommand = new RelayCommand(o => _server.ConnectToServer(Username),
                o => !string.IsNullOrEmpty(Username)
            );
            SendMessageCommand = new RelayCommand(o => _server.SendMessageToServer(Message),
                                o => !string.IsNullOrEmpty(Message)
            );
        }

        private void UserDisconnected()
        {
            var uid = _server.packetReader.ReadMessage();
            var user = Users.Where(x => x.UID == uid).FirstOrDefault();
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (user != null)
                {
                    Users.Remove(user);
                }
            });
        }

        private void MessageReceived()
        {
            var message = _server.packetReader.ReadMessage();
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(message);
            });
        }

        private void UserConnected()
        {
            var user = new User
            {
                Username = _server.packetReader.ReadMessage(),
                UID = _server.packetReader.ReadMessage()
            };

            if (!Users.Any(x => x.UID == user.UID))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Users.Add(user);
                });
            }
        }
    }
}
