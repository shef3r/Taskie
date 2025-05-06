using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Core;
using Windows.ApplicationModel.Core;
using Microsoft.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TaskieLib {
    public partial class Tools {
        public static string[] GetSystemEmojis() {
            List<string> emojis = new List<string>();

            var emojiRanges = new (int Start, int End)[]
            {
                (0x1F300, 0x1F5FF),
                (0x1F680, 0x1F6FF),
                (0x1F900, 0x1F9FF),
                (0x1FA70, 0x1FAFF),
                (0x1F1E6, 0x1F1FF),
                (0x1F3FB, 0x1F3FF),
                (0x200D,   0x200D),
                (0xFE0F,   0xFE0F),
            };

            foreach (var (start, end) in emojiRanges) {
                for (int codePoint = start; codePoint <= end; codePoint++) {
                    if (char.IsSurrogate((char)codePoint))
                        continue;

                    try {
                        string emoji = char.ConvertFromUtf32(codePoint);
                        emojis.Add(emoji);
                    }
                    catch { }
                }
            }

            return emojis.ToArray();
        }

        public static void SetThemeForTitleBar(string theme) {
            bool isLightTheme;

            switch (theme) {
                case "Light":
                    isLightTheme = true;
                    break;
                case "Dark":
                    isLightTheme = false;
                    break;
                case "System":
                    var uiSettings = new UISettings();
                    var backgroundColor = uiSettings.GetColorValue(UIColorType.Background);
                    isLightTheme = backgroundColor == Colors.White;
                    break;
                default:
                    goto case "System";
            }

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;

            var grayColor = Color.FromArgb(255, 128, 128, 128);

            var blackForeground = Color.FromArgb(255, 0, 0, 0);
            var blackPressedForeground = Color.FromArgb(255, 96, 96, 96);

            var whiteForeground = Color.FromArgb(255, 255, 255, 255);
            var whitePressedForeground = Color.FromArgb(255, 192, 192, 192);

            titleBar.ButtonForegroundColor = isLightTheme ? blackForeground : whiteForeground;
            titleBar.ButtonHoverForegroundColor = isLightTheme ? blackForeground : whiteForeground;
            titleBar.ButtonPressedForegroundColor = isLightTheme ? blackPressedForeground : whitePressedForeground;
            titleBar.ButtonInactiveForegroundColor = grayColor;
            titleBar.InactiveForegroundColor = grayColor;
            titleBar.ForegroundColor = isLightTheme ? blackForeground : whiteForeground;

            titleBar.BackgroundColor = Colors.Transparent;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = Colors.Transparent;
            titleBar.ButtonPressedBackgroundColor = Colors.Transparent;
            titleBar.InactiveBackgroundColor = Colors.Transparent;
        }



        public static void SetTheme(string theme) {
            try {
                switch (theme) {
                    case "Light":
                        Application.Current.RequestedTheme = ApplicationTheme.Light;
                        SetThemeForTitleBar(theme);
                        break;
                    case "Dark":
                        Application.Current.RequestedTheme = ApplicationTheme.Dark;
                        SetThemeForTitleBar(theme);
                        break;
                    default:
                        break;
                }
            }
            catch { }
        }

        public static async void RemoveAttachmentsFromList(string id) {
            await ApplicationData.Current.LocalFolder.CreateFolderAsync("TaskAttachments", CreationCollisionOption.OpenIfExists);
            StorageFolder folder = await ApplicationData.Current.LocalFolder.GetFolderAsync("TaskAttachments");
            if (Directory.Exists(Path.Combine(folder.Path, id))) {
                try {
                    StorageFolder attachmentFolder = await folder.GetFolderAsync(id);
                    await attachmentFolder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
                catch { }
            }
        }

        public partial class IncrementalEmojiSource : ObservableCollection<string>, ISupportIncrementalLoading // source for emojis in the "Change emoji" dialog
        {
            private readonly string[] allEmojis;
            private int currentIndex = 0;
            private const int BatchSize = 30;

            public IncrementalEmojiSource(string[] emojis) {
                allEmojis = emojis;
            }

            public bool HasMoreItems => currentIndex < allEmojis.Length;

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count) {
                return InternalLoadMoreItemsAsync(count).AsAsyncOperation();
            }

            private async Task<LoadMoreItemsResult> InternalLoadMoreItemsAsync(uint count) {
                await Task.Delay(50);

                int itemsToLoad = Math.Min(BatchSize, allEmojis.Length - currentIndex);
                for (int i = 0; i < itemsToLoad; i++) {
                    Add(allEmojis[currentIndex++]);
                }

                return new LoadMoreItemsResult { Count = (uint)itemsToLoad };
            }
        }
    }
}