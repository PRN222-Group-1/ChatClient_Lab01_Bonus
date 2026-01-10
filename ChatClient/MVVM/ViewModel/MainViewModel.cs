using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Resources;
using System.Windows;
using System.Windows.Input;
using ChatClient.MVVM.Core;
using ChatClient.MVVM.Model;
using ChatClient.Net;
using Newtonsoft.Json;

namespace ChatClient.MVVM.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<User> Users { get; set; }
        public ObservableCollection<string> Messages { get; set; }
        public ObservableCollection<Emojis> Emojis { get; set; }
        public RelayCommand ConnectToServerCommand { get; set; }
        public RelayCommand SendMessageCommand { get; set; }
        public RelayCommand InsertEmojisCommand { get; set; }

        private Server _server;

        private string _message;
        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                OnPropertyChanged(nameof(Message));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _username;
        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged(nameof(Username));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isEmojiOpen;
        public bool IsEmojiOpen
        {
            get => _isEmojiOpen;
            set
            {
                _isEmojiOpen = value;
                OnPropertyChanged(nameof(IsEmojiOpen));
            }
        }

        public RelayCommand ToggleEmojiPanelCommand { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public MainViewModel()
        {

            Users = new ObservableCollection<User>();
            Messages = new ObservableCollection<string>();
            Emojis = new ObservableCollection<Emojis>();
            LoadEmojis();

            _server = new Server();
            _server.connectedEvent += UserConnected;
            _server.messageReceivedEvent += MessageReceived;
            _server.userDisconnectedEvent += UserDisconnected;

            //Use Relay to unable the button after clicked once
            ConnectToServerCommand = new RelayCommand(o => _server.ConnectToServer(Username ?? "hello"),
                o => !string.IsNullOrEmpty(Username)
            );

            SendMessageCommand = new RelayCommand(
                o =>
                {
                    _server.SendMessageToServer(Message);
                    Message = string.Empty;
                },
                o => !string.IsNullOrWhiteSpace(Message)
            );

            InsertEmojisCommand = new RelayCommand(o =>
            {
                if (o is Emojis emoji)
                {
                    Message += emoji.Emoji;
                }
            });

            ToggleEmojiPanelCommand = new RelayCommand(o =>
            {
                IsEmojiOpen = !IsEmojiOpen;
            });
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
        private void LoadEmojis()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "emojis.json");
            try
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "emojis.json");
                var json = File.ReadAllText(path);

                var data = JsonConvert.DeserializeObject<EmojiList>(json);

                var emojis = data.Emojis;

                Emojis.Clear();

                foreach (var emoji in emojis)
                {
                    Emojis.Add(emoji);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load emojis. {ex} - {path}");
            }
        }
    }

}
