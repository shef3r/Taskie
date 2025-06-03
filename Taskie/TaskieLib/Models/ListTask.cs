﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Resources;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.UI.Notifications;

namespace TaskieLib
{
    public class ListTask : INotifyPropertyChanged
    {
        private DateTime _creationDate;
        private DateTime? _parentCreationDate;
        private string _name;
        private bool _isDone;
        private ObservableCollection<ListTask> _subTasks;
        private ObservableCollection<AttachmentMetadata> _attachments;

        // Add this field to store reminders locally
        private List<DateTimeOffset> _reminders = new List<DateTimeOffset>();

        public ListTask()
        {
            _creationDate = DateTime.UtcNow;
            _subTasks = new ObservableCollection<ListTask>();
            _attachments = new ObservableCollection<AttachmentMetadata>();
        }

        private const string ToastTagFormat = "{0}_{1}";

        [JsonIgnore]
        public IReadOnlyList<DateTimeOffset> Reminders
        {
            get
            {
                var now = DateTimeOffset.UtcNow;
                bool removed = _reminders.RemoveAll(r => r <= now) > 0;
                if (removed)
                {
                    OnPropertyChanged(nameof(Reminders));
                }
                return _reminders.AsReadOnly();
            }
            private set
            {
                if (!_reminders.SequenceEqual(value))
                {
                    _reminders = new List<DateTimeOffset>(value);
                    OnPropertyChanged(nameof(Reminders));
                }
            }
        }

        public void AddReminder(DateTimeOffset reminderDateTime, string listId)
        {
            if (reminderDateTime <= DateTimeOffset.UtcNow)
                throw new ArgumentException("Reminder time must be in the future", nameof(reminderDateTime));

            RemoveReminder(listId);
            ScheduleToastNotification(reminderDateTime, listId);

            // Local store logic
            _reminders.Clear();
            _reminders.Add(reminderDateTime);
            OnPropertyChanged(nameof(Reminders));
        }

        public void RemoveReminder(string listId)
        {
            try
            {
                var notifier = ToastNotificationManager.CreateToastNotifier();
                string tag = GetToastTag(listId);

                var scheduled = notifier.GetScheduledToastNotifications()
                    .Where(t => t.Tag == tag)
                    .ToList();
                foreach (var toast in scheduled)
                {
                    notifier.RemoveFromSchedule(toast);
                }

                ToastNotificationManager.History.Remove(tag, listId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing reminder: {ex.Message}");
            }

            // Local store logic
            if (_reminders.Count > 0)
            {
                _reminders.Clear();
                OnPropertyChanged(nameof(Reminders));
            }
        }

        public bool HasReminder(string listId)
        {
            try
            {
                var notifier = ToastNotificationManager.CreateToastNotifier();
                string tag = GetToastTag(listId);
                var toast = notifier.GetScheduledToastNotifications()
                    .FirstOrDefault(t => t.Tag == tag);
                return toast != null && toast.DeliveryTime > DateTimeOffset.UtcNow;
            }
            catch
            {
                return false;
            }
        }

        private void ScheduleToastNotification(DateTimeOffset reminderDateTime, string listId)
        {
            try
            {
                var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
                var textNodes = toastXml.GetElementsByTagName("text");

                textNodes[0].AppendChild(toastXml.CreateTextNode(
                    ResourceLoader.GetForCurrentView().GetString("ReminderGreeting")));
                textNodes[1].AppendChild(toastXml.CreateTextNode(Name));

                var toastElement = (XmlElement)toastXml.SelectSingleNode("//toast");
                toastElement?.SetAttribute("launch",
                    $"action=viewTask&creationDate={CreationDate:o}&listId={listId}");

                var scheduledToast = new ScheduledToastNotification(toastXml, reminderDateTime)
                {
                    Tag = GetToastTag(listId),
                    Group = listId
                };

                ToastNotificationManager.CreateToastNotifier().AddToSchedule(scheduledToast);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scheduling notification: {ex.Message}");
            }
        }

        private string GetToastTag(string listId)
            => string.Format(ToastTagFormat, _creationDate.Ticks, listId);
        

        [JsonIgnore]
        public ObservableCollection<AttachmentMetadata> Attachments {
            get => _attachments;
            private set {
                if (_attachments != value) {
                    _attachments = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("SubTasks")]
        public ObservableCollection<ListTask> SubTasks
        {
            get => _subTasks;
            set
            {
                if (_subTasks == value) return;
                _subTasks.Clear();
                if (value != null)
                {
                    foreach (var item in value)
                        _subTasks.Add(item);
                }
                OnPropertyChanged(nameof(SubTasks));
            }
        }

        [JsonPropertyName("CreationDate")]
        public DateTime CreationDate
        {
            get => _creationDate;
            set
            {
                if (_creationDate == value) return;
                _creationDate = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("ParentCreationDate")]
        public DateTime? ParentCreationDate
        {
            get => _parentCreationDate;
            set
            {
                if (_parentCreationDate == value) return;
                _parentCreationDate = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("Name")]
        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        [JsonPropertyName("IsDone")]
        public bool IsDone
        {
            get => _isDone;
            set
            {
                if (_isDone == value) return;
                _isDone = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    

        private const string AttachmentsRoot = "TaskAttachments";

        public async Task<AttachmentMetadata> AddAttachmentAsync(StorageFile file, string listId) {
            var root = ApplicationData.Current.LocalFolder;
            var taskFolder = await EnsureTaskFolderAsync(root, this.CreationDate, listId);
            var newFile = await file.CopyAsync(taskFolder,
                $"{Guid.NewGuid()}_{file.Name}", NameCollisionOption.GenerateUniqueName);
            var props = await newFile.GetBasicPropertiesAsync();
            var meta = new AttachmentMetadata {
                Id = Path.GetFileNameWithoutExtension(newFile.Name).Split('_')[0],
                FileName = file.Name,
                MimeType = file.ContentType,
                RelativePath = newFile.Path.Substring(root.Path.Length + 1)
            };
            Attachments.Add(meta);
            return meta;
        }

        public async Task RemoveAttachmentAsync(AttachmentMetadata attachment) {
            var root = ApplicationData.Current.LocalFolder;
            try {
                var file = await StorageFile.GetFileFromPathAsync(Path.Combine(root.Path, attachment.RelativePath));
                await file.DeleteAsync();
            }
            catch { }
            Attachments.Remove(attachment);
        }

        public async Task LoadAttachmentsAsync(string listId) {
            var root = ApplicationData.Current.LocalFolder;
            Attachments.Clear();
            try {
                var tasksRoot = await root.GetFolderAsync(AttachmentsRoot);
                var listFolder = await tasksRoot.GetFolderAsync(listId);
                var taskFolder = await listFolder.GetFolderAsync(this.CreationDate.Ticks.ToString());
                var files = await taskFolder.GetFilesAsync();
                foreach (var f in files) {
                    var props = await f.GetBasicPropertiesAsync();
                    var parts = f.Name.Split('_', 2);
                    Attachments.Add(new AttachmentMetadata {
                        Id = parts[0],
                        FileName = parts.Length > 1 ? parts[1] : f.Name,
                        MimeType = f.ContentType,
                        RelativePath = f.Path.Substring(root.Path.Length + 1)
                    });
                }
            }
            catch { }
        }

        private static async Task<StorageFolder> EnsureTaskFolderAsync(StorageFolder root, DateTime creation, string listId) {
            var tasksRoot = await root.CreateFolderAsync(AttachmentsRoot, CreationCollisionOption.OpenIfExists);
            var listFolder = await tasksRoot.CreateFolderAsync(listId, CreationCollisionOption.OpenIfExists);
            return await listFolder.CreateFolderAsync(creation.Ticks.ToString(), CreationCollisionOption.OpenIfExists);
        }
        
    }
}
