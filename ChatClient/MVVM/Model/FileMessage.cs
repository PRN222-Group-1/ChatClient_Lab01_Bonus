using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChatClient.MVVM.Model.ChatClient.MVVM.Model;

namespace ChatClient.MVVM.Model
{
    public class FileMessage : MessageModel
    {
        private string _fileName;
        private string _fileExtension;
        private string _fileIcon;
        private string _sender;
        private string _filePath;
        private long _fileSize;
        private DateTime _timestamp;

        public string FileName
        {
            get => _fileName;
            set
            {
                _fileName = value;
                OnPropertyChanged(nameof(FileName));
            }
        }

        public string FileExtension
        {
            get => _fileExtension;
            set
            {
                _fileExtension = value;
                OnPropertyChanged(nameof(FileExtension));
            }
        }

        public string FileIcon
        {
            get => _fileIcon;
            set
            {
                _fileIcon = value;
                OnPropertyChanged(nameof(FileIcon));
            }
        }

        public string Sender
        {
            get => _sender;
            set
            {
                _sender = value;
                OnPropertyChanged(nameof(Sender));
            }
        }

        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged(nameof(FilePath));
            }
        }

        public long FileSize
        {
            get => _fileSize;
            set
            {
                _fileSize = value;
                OnPropertyChanged(nameof(FileSize));
            }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
            }
        }

        private int _downloadProgress;
        private bool _isDownloading;
        private string _downloadStatus;
        public int DownloadProgress
        {
            get => _downloadProgress;
            set
            {
                _downloadProgress = value;
                OnPropertyChanged(nameof(DownloadProgress));
                OnPropertyChanged(nameof(DownloadProgressRemaining));
            }
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                _isDownloading = value;
                OnPropertyChanged(nameof(IsDownloading));
                OnPropertyChanged(nameof(FileExtension));
            }
        }

        public string DownloadStatus
        {
            get => _downloadStatus;
            set
            {
                _downloadStatus = value;
                OnPropertyChanged(nameof(DownloadStatus));
            }
        }

        public int DownloadProgressRemaining => 100 - DownloadProgress;


        // Helper method để lấy icon phù hợp dựa trên extension
        public static string GetFileIcon(string extension)
        {
            return extension?.ToLower() switch
            {
                ".pdf" => "📄",
                ".doc" or ".docx" => "📝",
                ".xls" or ".xlsx" => "📊",
                ".ppt" or ".pptx" => "📽️",
                ".zip" or ".rar" or ".7z" => "📦",
                ".jpg" or ".jpeg" or ".png" or ".gif" => "🖼️",
                ".mp3" or ".wav" or ".flac" => "🎵",
                ".mp4" or ".avi" or ".mkv" => "🎬",
                ".txt" => "📋",
                _ => "📎"
            };
        }
        public string DownloadProgressText => $"{DownloadProgress}%";



    }
}
