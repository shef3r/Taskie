﻿using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TaskieLib;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Resources;
using Windows.Security.Credentials.UI;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.Foundation;

namespace Taskie
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Windows.Foundation.Size(600, 500));
            InitializeComponent();
            SetupTitleBar();
            SetupNavigationMenu();
            CheckSecurity();
            DeterminePro();
            CheckOnboarding();
            ProFlyout();
            ListTools.RefreshRepetitiveLists();
            Navigation.Height = rectlist.ActualHeight;
            ListTools.ListCreatedEvent += UpdateLists;
            ListTools.ListDeletedEvent += ListDeleted;
            ListTools.ListRenamedEvent += ListRenamed;
            ListTools.ListEmojiChangedEvent += ListEmojiChanged;
            ListTools.AWOpenEvent += Tools_AWOpenEvent;
            ListTools.AWClosedEvent += Tools_AWClosedEvent;
            ActualThemeChanged += MainPage_ActualThemeChanged;

            if (App.Current is App app && !string.IsNullOrEmpty(app.ToastActivationArgument))
            {
                HandleToastActivation(app.ToastActivationArgument);
            }
        }

        public void HandleToastActivation(string argument)
        {
            // Extract the task ID from the argument
            var listId = argument.Split('=')[1];
            System.Diagnostics.Debug.WriteLine($"Toast notification activated. List ID: {listId}");

            // Navigate to the specific task page
            contentFrame.Navigate(typeof(TaskPage), listId);
            foreach (ListViewItem item in Navigation.Items)
            {
                if (item.Tag.ToString().Replace(".json", null) == listId)
                {
                    Navigation.SelectedItem = item;
                    break;
                }
            }
        }


        // nagging users to buy Pro
        private void ProFlyout()
        {
            if (!Settings.isPro && (new Random()).Next(1, 25) == 16)
            {
                protip.Target = UpdateButton;
                protip.IsOpen = true;
            }
        }

#if DEBUG
        public bool shouldShowOOBE = false;
#else
        public bool shouldShowOOBE = true;
#endif
        private async void CheckOnboarding()
        {
            if (!Settings.Launched && shouldShowOOBE)
            {
                ContentDialog dialog = new ContentDialog();
                Frame frame = new Frame();
                dialog.BorderBrush = Application.Current.Resources["ProBG"] as LinearGradientBrush;
                dialog.BorderThickness = new Thickness(2);
                frame.Navigate(typeof(OOBEDialogPage));
                dialog.Content = frame;
                dialog.DefaultButton = ContentDialogButton.Primary;
                dialog.PrimaryButtonText = resourceLoader.GetString("GetStarted");
                dialog.PrimaryButtonClick += (sender, args) => { };
                dialog.SecondaryButtonText = resourceLoader.GetString("Exit");
                dialog.SecondaryButtonClick += (sender, args) => { Application.Current.Exit(); };
                await dialog.ShowAsync();
            }
            Settings.Launched = true;
        }

        private void Tools_AWClosedEvent() // Executes when Taskie Mini is closed.
        {
            Navigation.Visibility = Visibility.Visible;
            Navigation.SelectedItem = null;
            AddItemBtn.Visibility = Visibility.Visible;
            searchbox.IsEnabled = true;
        }

        private void Tools_AWOpenEvent() // Executes when Taskie Mini is opened.
        {
            AddItemBtn.Visibility = Visibility.Collapsed;
            searchbox.IsEnabled = false;
            Navigation.Visibility = Visibility.Collapsed;
        }

        public void DeterminePro() // Locks down features for free users.
        {
            if (Settings.isPro)
            { 
                proText.Visibility = Visibility.Visible;
                BottomRow.Height = new GridLength(65);
                UpdateButton.Visibility = Visibility.Collapsed;
            }
            if (!Settings.isPro && ListTools.GetLists().Count() > 2)
            {
                AddItemBtn.IsEnabled = false;
                NewListBtnIcon.Foreground = new SolidColorBrush(Colors.White);
                NewListBtnIcon.Opacity = 0.7;
                NewListBtnText.Foreground = new SolidColorBrush(Colors.White);
                NewListBtnText.Opacity = 0.7;
            }
            else
            {
                AddItemBtn.IsEnabled = true;
                NewListBtnIcon.Foreground = new SolidColorBrush(Colors.Black);
                NewListBtnText.Foreground = new SolidColorBrush(Colors.Black);
                NewListBtnText.Opacity = 1;
                NewListBtnIcon.Opacity = 1;
            }
        }

        public async void CheckSecurity() // checks if it's possible to ask for Windows Hello authentication
        {
            UserConsentVerifierAvailability availability = await UserConsentVerifier.CheckAvailabilityAsync();
            if ((availability != UserConsentVerifierAvailability.Available && Settings.isAuthUsed) || (Settings.isAuthUsed && !Settings.isPro))
            {
                Settings.isAuthUsed = false;
                ContentDialog contentDialog = new ContentDialog() { Title = resourceLoader.GetString("AuthDisabledTitle"), Content = resourceLoader.GetString("AuthDisabledDescription"), PrimaryButtonText = "OK" };
                await contentDialog.ShowAsync();
            }
            else if (Settings.isAuthUsed)
            {
                Navigation.Visibility = Visibility.Collapsed;
                UserConsentVerificationResult consent = await UserConsentVerifier.RequestVerificationAsync(resourceLoader.GetString("LoginMessage"));
                if (consent != UserConsentVerificationResult.Verified)
                {
                    Application.Current.Exit();
                }
                Navigation.Visibility = Visibility.Visible;
            }
        }


        private void MainPage_ActualThemeChanged(FrameworkElement sender = null, object args = null)
        {
            (rect1.Fill as AcrylicBrush).TintColor = (Color)Application.Current.Resources["SystemAltHighColor"];
            (rect1.Fill as AcrylicBrush).FallbackColor = (Color)Application.Current.Resources["SystemAltLowColor"];
            (rect2.Fill as SolidColorBrush).Color = (Color)Application.Current.Resources["SystemAltLowColor"];
        }

        public ResourceLoader resourceLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();

        private void ListRenamed(string listID, string newname)
        {
            Debug.WriteLine("List got renamed:" + listID);
            foreach (var item in Navigation.Items)
            {
                if (item is ListViewItem navigationItem)
                {
                    if (navigationItem.Tag.ToString().Replace(".json", null) == listID)
                    {
                        ListMetadata metadata = ListTools.ReadList(listID).Metadata;
                        StackPanel content = new StackPanel();
                        content.Orientation = Orientation.Horizontal;
                        content.VerticalAlignment = VerticalAlignment.Center;
                        content.Children.Add(new FontIcon() { Glyph = metadata.Emoji, FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe UI Emoji"), FontSize = 14 });
                        content.Children.Add(new TextBlock() { Text = newname, Margin = new Thickness(12, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2 });
                        navigationItem.Content = content;
                        break;
                    }
                }
            }
        }


        private void ListEmojiChanged(string listID, string emoji)
        {
            Debug.WriteLine("List emoji changed:" + listID);
            foreach (var item in Navigation.Items)
            {
                if (item is ListViewItem navigationItem)
                {
                    if (navigationItem.Tag.ToString().Replace(".json", null) == listID)
                    {
                        ListMetadata metadata = ListTools.ReadList(listID).Metadata;
                        StackPanel content = new StackPanel();
                        content.Orientation = Orientation.Horizontal;
                        content.VerticalAlignment = VerticalAlignment.Center;
                        content.Children.Add(new FontIcon() { Glyph = emoji, FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe UI Emoji"), FontSize = 14 });
                        content.Children.Add(new TextBlock() { Text = metadata.Name, Margin = new Thickness(12, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2 });
                        navigationItem.Content = content;
                        break;
                    }
                }
            }
        }

        private void ListDeleted(string listID)
        {
            Debug.WriteLine("List deleted: " + listID);
            contentFrame.Content = new StackPanel();
            Navigation.SelectedItem = null;
            foreach (var item in Navigation.Items)
            {
                if (item is ListViewItem navigationItem)
                {
                    if (navigationItem.Tag.ToString().Replace(".json", null) == listID)
                    {
                        Navigation.Items.Remove(item);
                        break;
                    }
                }
            }
            DeterminePro();
        }

        private void SetupTitleBar()
        {
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            Window.Current.SetTitleBar(TTB);
        }

        private void SetupNavigationMenu()
        {
            foreach ((string listName, string listID) in TaskieLib.ListTools.GetLists())
            {
                ListMetadata metadata = ListTools.ReadList(listID.Replace(".json", null)).Metadata;
                Debug.WriteLine(JsonConvert.SerializeObject(metadata));
                StackPanel content = new StackPanel();
                content.Orientation = Orientation.Horizontal;
                content.VerticalAlignment = VerticalAlignment.Center;
                content.Children.Add(new FontIcon() { Glyph = metadata.Emoji ?? "📋", FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe UI Emoji"), FontSize = 14 });
                content.Children.Add(new TextBlock { Text = listName, Margin = new Thickness(12, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, Width = 80 });
                Navigation.Items.Add(new ListViewItem() { Tag = listID, Content = content, HorizontalContentAlignment = HorizontalAlignment.Left });
                AddRightClickMenu(Navigation.Items.Last() as ListViewItem);
            }
            DeterminePro();
        }

        private void AddRightClickMenu(ListViewItem item)
        {
            item.RightTapped += OpenRightClickList;
        }

        private void OpenRightClickList(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            MenuFlyout flyout = new MenuFlyout();
            flyout.Items.Add(new MenuFlyoutItem() { Icon = new SymbolIcon(Symbol.Rename), Text = resourceLoader.GetString("RenameList/Text"), Tag = (sender as ListViewItem).Tag });
            flyout.Items.Add(new MenuFlyoutItem() { Icon = new SymbolIcon(Symbol.Delete), Text = resourceLoader.GetString("DeleteList/Text"), Tag = (sender as ListViewItem).Tag });
            flyout.Items.Add(new MenuFlyoutItem() { Icon = new SymbolIcon(Symbol.Save), Text = resourceLoader.GetString("ExportList/Text"), Tag = (sender as ListViewItem).Tag });
            flyout.Items.Add(new MenuFlyoutItem() { Icon = new SymbolIcon(Symbol.Emoji), Text = resourceLoader.GetString("ChangeEmoji"), Tag = (sender as ListViewItem).Tag });
            flyout.Items.Add(GetRepeatMenuFlyoutItem((sender as ListViewItem).Tag));
            (flyout.Items[0] as MenuFlyoutItem).Click += RenameList_Click;
            (flyout.Items[1] as MenuFlyoutItem).Click += DeleteList_Click;
            (flyout.Items[2] as MenuFlyoutItem).Click += ExportList_Click;
            (flyout.Items[3] as MenuFlyoutItem).Click += ChangeEmoji_Click;
            flyout.ShowAt(sender as ListViewItem);
        }

        private MenuFlyoutItemBase GetRepeatMenuFlyoutItem(object tag)
        {
           var (metadata, tasks) = ListTools.ReadList(tag.ToString().Replace(".json", null));
            if (metadata.RepeatTime != null)
            {
                MenuFlyoutItem item = new MenuFlyoutItem() { Icon = new SymbolIcon(Symbol.Cancel), Text = resourceLoader.GetString("StopRepeating"), Tag = tag };
                item.Click += (sender, args) =>
                {
                    metadata.RepeatTime = null;
                    metadata.LastRepeatTime = null;
                    ListTools.SaveList(tag.ToString().Replace(".json", null), tasks, metadata);
                };
                return item;
            }
            else
            {
                MenuFlyoutItem item = new MenuFlyoutItem() { Icon = new SymbolIcon(Symbol.RepeatAll), Text = resourceLoader.GetString("RepeatList"), Tag = tag };
                item.Click += async (sender, args) =>
                {
                    ContentDialog dialog = new ContentDialog();
                    StackPanel stackPanel = new StackPanel();
                    StackPanel mainPanel = new StackPanel();
                    stackPanel.Orientation = Orientation.Horizontal;
                    NumberBox input = new NumberBox() { Value = 24 };
                    stackPanel.Children.Add(input);
                    stackPanel.Children.Add(new TextBlock() { Text = resourceLoader.GetString("EveryXHours"), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                    mainPanel.Children.Add(new TextBlock() { Text = resourceLoader.GetString("RepeatListDescription"), TextWrapping = TextWrapping.Wrap, MaxLines = 3, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 10), VerticalAlignment = VerticalAlignment.Center });
                    mainPanel.Children.Add(stackPanel);
                    mainPanel.Orientation = Orientation.Vertical;
                    dialog.Content = mainPanel;
                    dialog.Title = resourceLoader.GetString("RepeatList");
                    dialog.DefaultButton = ContentDialogButton.Primary;
                    dialog.PrimaryButtonText = "OK";
                    dialog.SecondaryButtonText = resourceLoader.GetString("Cancel");
                    dialog.PrimaryButtonClick += (sender, args) =>
                    {
                        if (input.Value > 0)
                        {
                            metadata.RepeatTime = new TimeSpan(0, (int)input.Value, 0, 0);
                            metadata.LastRepeatTime = DateTime.Now;
                            ListTools.SaveList(tag.ToString().Replace(".json", null), tasks, metadata);
                        }
                    };
                    await dialog.ShowAsync();
                };
                return item;
            }
        }

        public class IncrementalEmojiSource : ObservableCollection<string>, ISupportIncrementalLoading // source for emojis in the "Change emoji" dialog
        {
            private readonly string[] allEmojis;
            private int currentIndex = 0;
            private const int BatchSize = 50;

            public IncrementalEmojiSource(string[] emojis)
            {
                allEmojis = emojis;
            }

            public bool HasMoreItems => currentIndex < allEmojis.Length;

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return InternalLoadMoreItemsAsync(count).AsAsyncOperation();
            }

            private async Task<LoadMoreItemsResult> InternalLoadMoreItemsAsync(uint count)
            {
                await Task.Delay(50);

                int itemsToLoad = Math.Min(BatchSize, allEmojis.Length - currentIndex);
                for (int i = 0; i < itemsToLoad; i++)
                {
                    Add(allEmojis[currentIndex++]);
                }

                return new LoadMoreItemsResult { Count = (uint)itemsToLoad };
            }
        }

        private void ChangeEmoji_Click(object sender, RoutedEventArgs e)
        {
            var emojiSource = new IncrementalEmojiSource(Tools.GetSystemEmojis());

            GridView content = new GridView
            {
                Tag = (sender as MenuFlyoutItem).Tag,
                ItemsSource = emojiSource,
                ItemTemplate = (DataTemplate)Resources["EmojiBlock"],
                SelectionMode = ListViewSelectionMode.Single,
                Width = 250,
                Height = 300,
            };

            content.ItemsPanel = (ItemsPanelTemplate)Resources["WrapGridPanel"];

            Flyout flyout = new Flyout
            {
                Content = content
            };

            flyout.ShowAt(AddItemBtn);


            content.SelectionChanged += (sender, args) => {
                if ((sender as GridView).Tag.ToString().Replace(".json", null) != null && (sender as GridView).SelectedItem != null)
                {
                    ListTools.ChangeListEmoji((sender as GridView).Tag.ToString().Replace(".json", null), (sender as GridView).SelectedItem.ToString());
                }
                flyout.Hide();
            };
        }


        private async void ExportList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    FileSavePicker savePicker = new FileSavePicker
                    {
                        DefaultFileExtension = ".json",
                        SuggestedStartLocation = PickerLocationId.Desktop,
                        SuggestedFileName = (sender as MenuFlyoutItem)?.Tag?.ToString() ?? string.Empty
                    };
                    savePicker.FileTypeChoices.Add("JSON", new List<string>() { ".json" });

                    StorageFile file = await savePicker.PickSaveFileAsync();
                    if (file != null)
                    {
                        CachedFileManager.DeferUpdates(file);

                        string content = ListTools.GetTaskFileContent((sender as MenuFlyoutItem)?.Tag?.ToString() ?? string.Empty);
                        await FileIO.WriteTextAsync(file, content);

                        FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                    }
                });
            }
            catch { }
        }


        private async void RenameList_Click(object sender, RoutedEventArgs e)
        {
            string listname = ListTools.ReadList(((sender as MenuFlyoutItem).Tag as string).Replace(".json", null)).Metadata.Name;
            TextBox input = new TextBox() { PlaceholderText = resourceLoader.GetString("ListName"), Text = listname };
            ContentDialog dialog = new ContentDialog() { Title = resourceLoader.GetString("RenameList/Text"), PrimaryButtonText = "OK", SecondaryButtonText = resourceLoader.GetString("Cancel"), Content = input };
            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string text = input.Text;
                try
                {
                    if (string.IsNullOrEmpty(text)) {
                        throw new Exception("No list name");
                    }
                    else {
                        ListTools.RenameList(((sender as MenuFlyoutItem).Tag as string).Replace(".json", null), text);
                    }
                }
                catch {
                    tipwrongname.Target = Navigation;
                    tipwrongname.PreferredPlacement = TeachingTipPlacementMode.TopRight;
                    tipwrongname.IsOpen = true;
                }
                listname = text;
                ListRenamed(((sender as MenuFlyoutItem).Tag as string).Replace(".json", null), text);
            }
        }

        private void DeleteList_Click(object sender, RoutedEventArgs e)
        {
            string listname = (sender as MenuFlyoutItem).Tag as string;
            ListTools.DeleteList(listname.Replace(".json", null));
            ListDeleted(((sender as MenuFlyoutItem).Tag.ToString().Replace(".json", null)));
            DeterminePro();
        }

        private void UpdateLists(string listID, string listName)
        {
            Navigation.Items.Clear();
            SetupNavigationMenu();
            contentFrame.Content = null;
            DeterminePro();
        } // Resets the main view

        private void AddList(object sender, RoutedEventArgs e)
        {
            string listName = ListTools.CreateList(resourceLoader.GetString("NewList"));
            UpdateLists(null, resourceLoader.GetString("NewList"));
            foreach (ListViewItem item in Navigation.Items)
            {
                if (item.Tag.ToString().Contains(listName))
                { Navigation.SelectedItem = item; break; }
            }
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            AppWindow window = await AppWindow.TryCreateAsync();
            window.Title = resourceLoader.GetString("SettingsText/Text");
            Frame settingsContent = new Frame();
            settingsContent.Navigate(typeof(SettingsPage));
            window.TitleBar.ExtendsContentIntoTitleBar = true;
            window.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            ElementCompositionPreview.SetAppWindowContent(window, settingsContent);
            window.Closed += SettingsWindowClosed;
            (sender as Button).IsEnabled = false;
            if (Settings.Theme == "Dark")
            {
                window.TitleBar.ButtonForegroundColor = Windows.UI.Colors.White;
                window.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Colors.White;

            }
            else if (Settings.Theme == "Light")
            {
                window.TitleBar.ButtonForegroundColor = Windows.UI.Colors.Black;
                window.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Colors.Black;
            }
            await window.TryShowAsync();
        }

        private void SettingsWindowClosed(AppWindow sender, AppWindowClosedEventArgs args)
        {
            SettingsButton.IsEnabled = true;
        } // disallows for >1 settings window at a time

        private void Navigation_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        { // opens a list
            ListView NavList = sender as ListView;
            var selectedItem = NavList.SelectedItem as ListViewItem;
            if (selectedItem != null && selectedItem.Tag is string tag)
            {
                contentFrame.Navigate(typeof(TaskPage), tag.Replace(".json", null));
            }
        }

        private async void UpgradeButton_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog();
            Frame frame = new Frame();
            dialog.BorderBrush = Application.Current.Resources["ProBG"] as LinearGradientBrush;
            dialog.BorderThickness = new Thickness(2);
            frame.Navigate(typeof(UpgradeDialogContentPage));
            dialog.Content = frame;
            dialog.DefaultButton = ContentDialogButton.Primary;
            dialog.PrimaryButtonText = string.Format(resourceLoader.GetString("UpgradeFor"), "$0.99"); // placeholder
            dialog.PrimaryButtonClick += Dialog_UpgradeAction;
            dialog.SecondaryButtonText = resourceLoader.GetString("Cancel");
            await dialog.ShowAsync();
        }

        private async void Dialog_UpgradeAction(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // DEBUG UPGRADE OPTION
            if (!Settings.isPro)
            {
                ToastContentBuilder builder = new ToastContentBuilder()
                    .AddText(resourceLoader.GetString("successfulUpgrade"))
                    .AddText(resourceLoader.GetString("successfulUpgradeSub"));
                builder.Show();
                Settings.isPro = true;
                await CoreApplication.RequestRestartAsync("Pro status changed.");
            }
        }

        private void rectlist_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Navigation.Height = rectlist.ActualHeight;
        }

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(sender.Text))
            { sender.IsSuggestionListOpen = false; sender.ItemsSource = new List<string>(); }
            else
            {
                sender.ItemsSource = Array.FindAll<(string, string)>(ListTools.GetLists(), s => s.Item1.ToLower().Contains(sender.Text.ToLower())).Select(t => t.Item1).ToArray();
            }
        }

        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            sender.IsSuggestionListOpen = true;
        }

        private void searchbox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            try
            {
                string foundItem = "";
                foreach ((string name, string id) item in ListTools.GetLists())
                {
                    if (args.SelectedItem.ToString() == item.name)
                    {
                        foundItem = item.id.Replace(".json", null);
                    }
                }
                if (!string.IsNullOrEmpty(foundItem))
                {
                    contentFrame.Navigate(typeof(TaskPage), foundItem.Replace(".json", null));
                    foreach (ListViewItem item in Navigation.Items) {
                        if (item.Tag.ToString().Replace(".json", null) == foundItem) {
                            Navigation.SelectedItem = item;
                        }
                    }
                }

                sender.Text = "";
                searchbox.ItemsSource = new List<string>();
            }
            catch { }
        }
    }
}
