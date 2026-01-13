using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ChatClient.MVVM.Core;
using ChatClient.MVVM.Model;
using ChatClient.MVVM.Model.ChatClient.MVVM.Model;
using ChatClient.Net;
using Newtonsoft.Json;

namespace ChatClient.MVVM.ViewModel
{

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly Server _server;
        private string _message;
        private string _username;
        private bool _isEmojiOpen;
        private string _pendingFilePath;
        private string _uploadProgress;

        public ObservableCollection<User> Users { get; }
        public ObservableCollection<MessageModel> Messages { get; }
        public ObservableCollection<Emojis> Emojis { get; }

        public RelayCommand ConnectToServerCommand { get; }
        public RelayCommand SendMessageCommand { get; }
        public RelayCommand InsertEmojisCommand { get; }
        public RelayCommand UploadFileCommand { get; }
        public RelayCommand RemoveFileCommand { get; }
        public RelayCommand ToggleEmojiPanelCommand { get; }
        public RelayCommand DownloadFileCommand { get; }

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

        public bool IsEmojiOpen
        {
            get => _isEmojiOpen;
            set
            {
                _isEmojiOpen = value;
                OnPropertyChanged(nameof(IsEmojiOpen));
            }
        }

        public string PendingFilePath
        {
            get => _pendingFilePath;
            set
            {
                _pendingFilePath = value;
                OnPropertyChanged(nameof(HasPendingFile));
                OnPropertyChanged(nameof(PendingFileName));
                OnPropertyChanged(nameof(PendingFileSize));
            }
        }

        public bool HasPendingFile => !string.IsNullOrEmpty(PendingFilePath);

        public string PendingFileName => string.IsNullOrEmpty(PendingFilePath) ? "" : Path.GetFileName(PendingFilePath);

        public string PendingFileSize
        {
            get
            {
                if (string.IsNullOrEmpty(PendingFilePath) || !File.Exists(PendingFilePath))
                    return "";

                var size = new FileInfo(PendingFilePath).Length;

                if (size < 1024) return $"{size} B";
                if (size < 1024 * 1024) return $"{size / 1024.0:F1} KB";
                return $"{size / (1024.0 * 1024.0):F1} MB";
            }
        }

        public string UploadProgress
        {
            get => _uploadProgress;
            set
            {
                _uploadProgress = value;
                OnPropertyChanged(nameof(UploadProgress));
                OnPropertyChanged(nameof(IsUploading));
            }
        }

        public bool IsUploading => !string.IsNullOrEmpty(UploadProgress);

        public event PropertyChangedEventHandler PropertyChanged;

        public MainViewModel()
        {
            Users = new ObservableCollection<User>();
            Messages = new ObservableCollection<MessageModel>();
            Emojis = new ObservableCollection<Emojis>();

            LoadEmojis();

            _server = new Server();
            _server.connectedEvent += UserConnected;
            _server.messageReceivedEvent += MessageReceived;
            _server.userDisconnectedEvent += UserDisconnected;
            _server.fileReceivedEvent += FileReceived;

            _server.fileUploadedEvent += (sender, fileName) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Messages.Add(new TextMessage
                    {
                        Text = $"{sender} uploaded file: {fileName}"
                    });
                });
            };


            ConnectToServerCommand = new RelayCommand(
                o => _server.ConnectToServer(Username ?? "hello"),
                o => !string.IsNullOrEmpty(Username)
            );

            UploadFileCommand = new RelayCommand(o =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog();
                if (dialog.ShowDialog() == true)
                {
                    PendingFilePath = dialog.FileName;
                }
            });

            RemoveFileCommand = new RelayCommand(o => PendingFilePath = null);

            SendMessageCommand = new RelayCommand(
                async o =>
                {
                    if (!string.IsNullOrWhiteSpace(Message))
                    {
                        _server.SendMessageToServer(Message);
                        Message = string.Empty;
                    }

                    if (!string.IsNullOrEmpty(PendingFilePath))
                    {
                        await _server.SendFileToServer(PendingFilePath, percent =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                UploadProgress = $"Uploading... {percent}%";
                            });
                        });

                        UploadProgress = null;
                        PendingFilePath = null;
                    }
                    
                },
                o => (!string.IsNullOrWhiteSpace(Message) || !string.IsNullOrEmpty(PendingFilePath)) && !IsUploading
            );

            InsertEmojisCommand = new RelayCommand(o =>
            {
                if (o is Emojis emoji)
                {
                    Message += emoji.Emoji;
                }
            });

            ToggleEmojiPanelCommand = new RelayCommand(o => IsEmojiOpen = !IsEmojiOpen);

            DownloadFileCommand = new RelayCommand(o =>
            {
                if (o is FileMessage fileMsg)
                {
                    var saveDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        FileName = fileMsg.FileName,
                        Filter = "All files (*.*)|*.*"
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        _server.RequestDownloadFile(fileMsg.FileName);
                    }
                }
            });
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void FileReceived(string sender, string fileName)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new FileMessage
                {
                    Sender = sender,
                    FileName = fileName
                });
            });
        }

        private void UserDisconnected()
        {
            var uid = _server.packetReader.ReadMessage();
            var user = Users.FirstOrDefault(x => x.UID == uid);

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
                Messages.Add(new TextMessage { Text = message });
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
                Application.Current.Dispatcher.Invoke(() => Users.Add(user));
            }
        }

        private void LoadEmojis()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "emojis.json");

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<EmojiList>(json);

                Emojis.Clear();
                foreach (var emoji in data.Emojis)
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