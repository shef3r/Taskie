﻿using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskieLib;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.System;
using Windows.UI;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Taskie {
    public sealed partial class TaskPage : Page {
        private List<ListTask> tasks;

        public TaskPage() {
            this.InitializeComponent();
            ActualThemeChanged += TaskPage_ActualThemeChanged;
            ListTools.ListRenamedEvent += ListRenamed;
        }

        #region Click handlers

        private async void CustomizeList_Click(object sender, RoutedEventArgs e) {
            StackPanel panel = new StackPanel() { Margin = new Thickness(2) };

            StackPanel bgbtnpanel = new StackPanel() {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new FontIcon() { Glyph = "\uE70F", FontSize = 14, Margin = new Thickness(0, 0, 10, 0) },
                    new TextBlock() { Text = resourceLoader.GetString("ChangeListBackground"), FontSize = 14 }
                }
            };
            HyperlinkButton button = new HyperlinkButton() {
                Content = bgbtnpanel,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };
            button.IsEnabled = await Settings.CheckIfProAsync();

            button.Click += async (sender, args) => {
                FileOpenPicker openPicker = new FileOpenPicker();
                openPicker.ViewMode = PickerViewMode.Thumbnail;
                openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                openPicker.FileTypeFilter.Add(".jpg");
                openPicker.FileTypeFilter.Add(".jpeg");
                openPicker.FileTypeFilter.Add(".png");
                openPicker.CommitButtonText = resourceLoader.GetString("SetAsBackground");
                StorageFile file = await openPicker.PickSingleFileAsync();
                if (file != null) {
                    await ListTools.ChangeListBackground(listId, file);

                    using (var stream = await file.OpenAsync(FileAccessMode.Read)) {
                        var bitmapImage = new BitmapImage();
                        await bitmapImage.SetSourceAsync(stream);
                        bgImage.Source = bitmapImage;
                    }

                    AnimateOpacity(bgImage);
                }
            };
            panel.Children.Add(button);


            Expander fontExpander = new Expander() {
                Header = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { new FontIcon() { Glyph = "\uE8D2", Margin = new Thickness(0, 0, 10, 0) }, new TextBlock() { Text = resourceLoader.GetString("ChangeFont") } } },
                Width = 300
            };
            ListView fontChooser = new ListView() {
                SelectionMode = ListViewSelectionMode.Single,
                Height = 300,
                Width = 250,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            fontChooser.IsEnabled = await Settings.CheckIfProAsync();
            fontChooser.SelectionChanged += (s, a) => {
                ListTools.ChangeListFont(listId, (fontChooser.SelectedItem as ListViewItem).Tag.ToString());
                testname.FontFamily = new FontFamily((fontChooser.SelectedItem as ListViewItem).Tag.ToString());
            };

            fontExpander.Content = fontChooser;

            foreach (string font in Microsoft.Graphics.Canvas.Text.CanvasTextFormat.GetSystemFontFamilies()) {
                ListViewItem subfont = new ListViewItem() { Tag = font, Content = font, FontFamily = new FontFamily(font) };
                fontChooser.Items.Add(subfont);
            }
            ListMetadata data = ListTools.ReadList(listId).Metadata;
            fontChooser.SelectedItem = data.TitleFont;
            panel.Children.Add(fontExpander);


            Expander emojiExpander = new Expander() {
                Header = new StackPanel() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { new FontIcon() { Glyph = "\uE899", Margin = new Thickness(0, 0, 10, 0) }, new TextBlock() { Text = resourceLoader.GetString("ChangeEmoji") } } },
                Width = 300,
                Margin = new Thickness(0, 10, 0, 0)
            };
            var emojiSource = new Tools.IncrementalEmojiSource(Tools.GetSystemEmojis());
            GridView content = new GridView {
                ItemsSource = emojiSource,
                ItemTemplate = (DataTemplate)Application.Current.Resources["EmojiBlock"],
                SelectionMode = ListViewSelectionMode.Single,
                Width = 250,
                HorizontalAlignment = HorizontalAlignment.Center,
                Height = 200,
            };
            content.ItemsPanel = (ItemsPanelTemplate)Application.Current.Resources["WrapGridPanel"];
            content.SelectedItem = data.Emoji;
            content.SelectionChanged += (sender, args) => {
                if (listId.Replace(".json", null) != null && (sender as GridView).SelectedItem != null) {
                    ListTools.ChangeListEmoji(listId.Replace(".json", null), (sender as GridView).SelectedItem.ToString());
                }
            };
            emojiExpander.Content = content;
            panel.Children.Add(emojiExpander);
            Flyout flyout = new Flyout();
            flyout.Content = panel;
            flyout.ShowAt(topoptions, new FlyoutShowOptions() { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
        }
        private void RenameTask_Click(object sender, RoutedEventArgs e) {
            MenuFlyoutItem menuFlyoutItem = (MenuFlyoutItem)sender;
            var note = menuFlyoutItem.DataContext as ListTask;

            TextBox input = new TextBox() {
                PlaceholderText = resourceLoader.GetString("TaskName"),
                Text = note.Name,
                Margin = new Thickness(-10),
                Width = NameBox.ActualWidth + 40,
                MaxWidth = 400
            };

            Flyout flyout = new Flyout() {
                Content = input,
                Placement = FlyoutPlacementMode.Left
            };

            input.KeyDown += (s, args) => { if (args.Key == VirtualKey.Enter) { flyout.Hide(); } };

            flyout.Closed += (s, args) => {
                string newName = input.Text;
                if (!string.IsNullOrEmpty(newName) && newName != note.Name) {
                    note.Name = newName;

                    (ListMetadata data, List<ListTask> tasks) = ListTools.ReadList(listId);

                    int index = tasks.FindIndex(task => task.CreationDate == note.CreationDate);
                    tasks[index] = note;
                    ListTools.SaveList(listId, tasks, data);
                }
            };
            if (menuFlyoutItem.Tag is Button button) {
                flyout.ShowAt(button);
            }
        }

        private void DeleteTask_Click(object sender, RoutedEventArgs e) {
            ListTask taskToDelete = (sender as MenuFlyoutItem).DataContext as ListTask;
            (ListMetadata metadata, List<ListTask> tasks) = ListTools.ReadList(listId);
            int index = tasks.FindIndex(task => task.CreationDate == taskToDelete.CreationDate);
            if (index != -1) {
                tasks.RemoveAt(index);
                ListTools.SaveList(listId, tasks, metadata);
                taskListView.Items.Remove(taskToDelete);
            }
            ListTools.SaveList(listId, tasks, metadata);
        }

        private void RenameList_Click(object sender, RoutedEventArgs e) {
            TextBox input = new TextBox() {
                PlaceholderText = resourceLoader.GetString("ListName"),
                Text = listname,
                Width = NameBox.ActualWidth + 55,
                MaxWidth = 400
            };
            Flyout flyout = new Flyout() {
                Content = new StackPanel() {
                    Children =
            {
                input
            },
                    Margin = new Thickness(-10),
                }
            };
            input.KeyDown += (s, args) => { if (args.Key == VirtualKey.Enter) { flyout.Hide(); } };
            flyout.Closed += (s, args) => {
                string text = input.Text;
                if (!string.IsNullOrEmpty(text)) {
                    ListTools.RenameList(listId, text);
                    listname = text;
                    testname.Text = listname;
                }
                flyout.Hide();
            };
            flyout.ShowAt(topoptions, new FlyoutShowOptions() { Placement = FlyoutPlacementMode.Left });
        }

        private void DeleteList_Click(object sender, RoutedEventArgs e) {
            ListTools.DeleteList(listId);
        }

        private async void ExportList_Click(object sender, RoutedEventArgs e) {
            try {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () => {
                    FileSavePicker savePicker = new FileSavePicker {
                        DefaultFileExtension = ".json",
                        SuggestedStartLocation = PickerLocationId.Desktop,
                        SuggestedFileName = listname
                    };
                    savePicker.FileTypeChoices.Add("JSON", new List<string>() { ".json" });

                    StorageFile file = await savePicker.PickSaveFileAsync();
                    if (file != null) {
                        CachedFileManager.DeferUpdates(file);
                        string content = ListTools.GetTaskFileContent(listId);
                        await FileIO.WriteTextAsync(file, content);

                        FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                    }
                    else { }
                });
            }
            catch (Exception ex) { Debug.WriteLine("[List export] Exception occured: " + ex.Message); }
        }

        private void RenameSubTask_Click(object sender, RoutedEventArgs e) {
            MenuFlyoutItem menuFlyoutItem = (MenuFlyoutItem)sender;
            var subTask = menuFlyoutItem.DataContext as ListTask;
            if (subTask == null) {
                return;
            }

            (ListMetadata meta, List<ListTask> tasks) = ListTools.ReadList(listId);

            TextBox input = new TextBox() {
                PlaceholderText = resourceLoader.GetString("TaskName"),
                Text = subTask.Name,
                Margin = new Thickness(-10),
                Width = NameBox.ActualWidth + 65,
                MaxWidth = 400
            };

            Flyout flyout = new Flyout() {
                Content = input,
                Placement = FlyoutPlacementMode.Left
            };

            input.KeyDown += (s, args) => { if (args.Key == VirtualKey.Enter) { flyout.Hide(); } };

            flyout.Closed += (s, args) => {
                if (!string.IsNullOrEmpty(input.Text)) {
                    int index = tasks.FindIndex(task => task.CreationDate == subTask?.ParentCreationDate);
                    if (index > -1) {
                        ListTask parentTask = tasks[index];

                        ListTask taskToRemove = parentTask.SubTasks.FirstOrDefault(t => t.CreationDate == subTask.CreationDate);
                        if (taskToRemove != null) {
                            parentTask.SubTasks.FirstOrDefault(t => t.CreationDate == subTask.CreationDate).Name = input.Text;
                            tasks[index] = parentTask;
                            (taskListView.Items[index] as ListTask).SubTasks.FirstOrDefault(t => t.CreationDate == subTask.CreationDate).Name = input.Text;
                        }
                        else {
                        }
                    }
                }
            };

            if (menuFlyoutItem.Tag is Button button) {
                flyout.ShowAt(button);
            }
        }

        private void DeleteSubTask_Click(object sender, RoutedEventArgs e) {
            ListTask subTask = (sender as MenuFlyoutItem)?.DataContext as ListTask;
            if (subTask == null) {
                return;
            }

            (ListMetadata meta, List<ListTask> tasks) = ListTools.ReadList(listId);

            int index = tasks.FindIndex(task => task.CreationDate == subTask?.ParentCreationDate);
            if (index > -1) {
                ListTask parentTask = tasks[index];

                ListTask taskToRemove = parentTask.SubTasks.FirstOrDefault(t => t.CreationDate == subTask.CreationDate);
                if (taskToRemove != null) {
                    if (parentTask.SubTasks.Count == 1 && parentTask.SubTasks.Last().IsDone == true) {
                        ChangeProgressBarValue(parentTask, false, 0);
                    }
                    parentTask.SubTasks.Remove(taskToRemove);
                    tasks[index] = parentTask;
                    (taskListView.Items[index] as ListTask).SubTasks = parentTask.SubTasks;
                }
                else {
                }
            }
            else {
            }
            ListTools.SaveList(listId, tasks, meta);
        }

        private async void CompactOverlay_Click(object sender, RoutedEventArgs e) {
            AppWindow window = await AppWindow.TryCreateAsync();
            window.Presenter.RequestPresentation(AppWindowPresentationKind.CompactOverlay);

            window.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            window.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            if (Settings.Theme == "Dark") {
                window.TitleBar.ButtonForegroundColor = Colors.White;
            }
            else if (Settings.Theme == "Light") {
                window.TitleBar.ButtonForegroundColor = Colors.Black;
            }
            window.Closed += AWClosed;
            Frame frame = new Frame();
            frame.Navigate(typeof(TaskPage), listId.Replace(".json", null));
            ListTools.isAWOpen = true;
            ElementCompositionPreview.SetAppWindowContent(window, frame);
            window.TitleBar.ExtendsContentIntoTitleBar = true;
            cobtn.Visibility = Visibility.Collapsed;
            await window.TryShowAsync();
            this.Frame.Navigate(typeof(COClosePage));
        }

        #endregion

        #region Double click and right click handlers

        private void TaskNameText_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) {
            TextBlock textBlock = (TextBlock)sender;
            var note = textBlock.DataContext as ListTask;

            TextBox input = new TextBox() {
                PlaceholderText = resourceLoader.GetString("TaskName"),
                Text = note.Name,
                Margin = new Thickness(-10),
                Width = NameBox.ActualWidth + 40,
                MaxWidth = 400
            };

            Flyout flyout = new Flyout() {
                Content = input,
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
            };

            input.KeyDown += (s, args) => { if (args.Key == VirtualKey.Enter) { flyout.Hide(); } };

            flyout.Closed += (s, args) => {
                string newName = input.Text;
                if (!string.IsNullOrEmpty(newName) && newName != note.Name) {
                    note.Name = newName;

                    (ListMetadata metadata, List<ListTask> tasks) = ListTools.ReadList(listId);
                    int index = tasks.FindIndex(task => task.CreationDate == note.CreationDate);
                    tasks[index] = note;
                    ListTools.SaveList(listId, tasks, metadata);
                }
            };
            flyout.ShowAt(sender as TextBlock);
        }

        private void SubTaskNameText_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) {
            TextBlock textBlock = (TextBlock)sender;
            var subTask = textBlock.DataContext as ListTask;
            if (subTask == null) {
                return;
            }

            (ListMetadata meta, List<ListTask> tasks) = ListTools.ReadList(listId);

            TextBox input = new TextBox() {
                PlaceholderText = resourceLoader.GetString("TaskName"),
                Text = subTask.Name,
                Margin = new Thickness(-10),
                Width = NameBox.ActualWidth + 65,
                MaxWidth = 400
            };

            Flyout flyout = new Flyout() {
                Content = input,
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
            };

            input.KeyDown += (s, args) => { if (args.Key == VirtualKey.Enter) { flyout.Hide(); } };

            flyout.Closed += (s, args) => {
                if (!string.IsNullOrEmpty(input.Text)) {
                    int index = tasks.FindIndex(task => task.CreationDate == subTask?.ParentCreationDate);
                    if (index > -1) {
                        ListTask parentTask = tasks[index];

                        ListTask taskToRemove = parentTask.SubTasks.FirstOrDefault(t => t.CreationDate == subTask.CreationDate);
                        if (taskToRemove != null) {
                            parentTask.SubTasks.FirstOrDefault(t => t.CreationDate == subTask.CreationDate).Name = input.Text;
                            tasks[index] = parentTask;
                            (taskListView.Items[index] as ListTask).SubTasks.FirstOrDefault(t => t.CreationDate == subTask.CreationDate).Name = input.Text;
                        }
                        else {
                        }
                    }
                }
            };

            flyout.ShowAt(sender as TextBlock);
        }

        private void testname_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) {
            TextBox input = new TextBox() {
                PlaceholderText = resourceLoader.GetString("ListName"),
                Text = listname,
                Width = NameBox.ActualWidth + 55,
                MaxWidth = 400
            };
            Flyout flyout = new Flyout() {
                Content = new StackPanel() {
                    Children =
            {
                input
            },
                    Margin = new Thickness(-10),
                }
            };
            input.KeyDown += (s, args) => { if (args.Key == VirtualKey.Enter) { flyout.Hide(); } };
            flyout.Closed += (s, args) => {
                string text = input.Text;
                if (!string.IsNullOrEmpty(text)) {
                    ListTools.RenameList(listId, text);
                    listname = text;
                    testname.Text = listname;
                }
                flyout.Hide();
            };
            flyout.ShowAt(sender as TextBlock, new FlyoutShowOptions() { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft });
        }

        private async void TPage_Loaded(object sender, RoutedEventArgs e) {
            await Task.Run(async () => {
                if (File.Exists(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "bg_" + listId))) {
                    var file = await ApplicationData.Current.LocalFolder.GetFileAsync("bg_" + listId);
                    var uri = new Uri(file.Path);
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () => {
                        var bitmapImage = new BitmapImage(uri);
                        bgImage.Source = bitmapImage;
                        AnimateOpacity(bgImage);
                    });
                }
            });
        }

        private void testname_Loaded(object sender, RoutedEventArgs e) {
            (ListMetadata metadata, List<ListTask> tasks) = ListTools.ReadList(listId);
            try {
                testname.FontFamily = new FontFamily(metadata.TitleFont);
            }
            catch {
                testname.FontFamily = new FontFamily("Segoe UI Variable");
                metadata.TitleFont = "Segoe UI Variable";
            }
        }

        private void NameBox_Loaded(object sender, RoutedEventArgs e) {
            ChangeWidth(sender);
        }

        private void topoptions_Loaded(object sender, RoutedEventArgs e) {
            if (ListTools.isAWOpen) {
                if (Settings.Theme == "System") {
                    if (Application.Current.RequestedTheme == ApplicationTheme.Dark) {
                        TPage.Background = new SolidColorBrush { Color = Color.FromArgb(255, 33, 33, 33) };
                    }
                    else if (Application.Current.RequestedTheme == ApplicationTheme.Light) {
                        TPage.Background = new SolidColorBrush { Color = Colors.White };
                    }
                }
                else {
                    if (Settings.Theme == "Dark") {
                        TPage.Background = new SolidColorBrush { Color = Color.FromArgb(255, 33, 33, 33) };
                    }
                    else if (Settings.Theme == "Light") {
                        TPage.Background = new SolidColorBrush { Color = Colors.White };
                    }
                }
                topoptions.Visibility = Visibility.Collapsed;
                cobtn.Visibility = Visibility.Collapsed;
            }
        }

        private void MenuFlyoutItem_Loaded(object sender, RoutedEventArgs e) {
            if (ListTools.isAWOpen) {
                (sender as MenuFlyoutItem).Visibility = Visibility.Collapsed;
            }
        }

        private void TaskThreeDots_Loaded(object sender, RoutedEventArgs e) {
            tasks = ListTools.ReadList(listId).Tasks;
            Button button = sender as Button;
            (button.Flyout as MenuFlyout).Items[0].Tag = button;
            ListTask boundTask = button.DataContext as ListTask;
            ListTask task;
            try {
                task = tasks.FirstOrDefault(t => t.CreationDate == boundTask.CreationDate);
                if (task == null) {
                    return;
                }
                MenuFlyout flyout = button.Flyout as MenuFlyout;
                UpdateFlyoutMenu(flyout, task, button);
            }
            catch (Exception ex) { Debug.WriteLine("[TaskThreeDots_Loaded] Exception occured: " + ex.Message); }
        }

        private void Progress_Loaded(object sender, RoutedEventArgs e) {
            HideShowProgress(sender as Windows.UI.Xaml.Controls.ProgressBar);
        }

        private void SubTaskThreeDots_Loaded(object sender, RoutedEventArgs e) {
            ((sender as Button).Flyout as MenuFlyout).Items[0].Tag = sender;
        }

        #endregion

        #region Utility methods

        private void AnimateOpacity(UIElement element) {
            var animation = new DoubleAnimation {
                From = 0,
                To = 0.4,
                Duration = new Duration(TimeSpan.FromSeconds(1))
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, "Opacity");
            storyboard.Begin();
        }

        private void ChangeWidth(object sender) {
            foreach (ListTask task in taskListView.Items) {
                var item = taskListView.ContainerFromItem(task) as ListViewItem;
                if (item != null) {
                    var taskNameText = FindDescendant<TextBlock>(item, "TaskNameText");

                    if (taskNameText != null) {
                        taskNameText.Width = (sender as Rectangle).ActualWidth;
                    }
                }
            }
        }

        private void ChangeProgressBarValue(object dataContext, bool addOne = false, int overrideTotal = -1) {
            var listView = this.FindName("taskListView") as ListView;
            if (listView == null)
                return;
            foreach (var item in listView.Items) {
                if ((item as ListTask).CreationDate == (dataContext as ListTask).CreationDate) {
                    var container = listView.ContainerFromItem(item) as ListViewItem;
                    if (container == null)
                        continue;

                    var expander = FindVisualChild<Expander>(container, "rootGrid");
                    if (expander == null)
                        continue;

                    var headerGrid = expander.Header as Grid;
                    if (headerGrid == null)
                        continue;

                    var progressBar = headerGrid.Children
                        .OfType<Windows.UI.Xaml.Controls.ProgressBar>()
                        .FirstOrDefault();

                    if (progressBar != null) {
                        progressBar.Value = (double)((new ProgressConverter()).Convert(SubTaskNumber(expander, addOne, overrideTotal), typeof(double), null, null));
                    }
                }
            }
        }

        private T FindVisualChild<T>(DependencyObject obj, string name) where T : DependencyObject {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++) {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is FrameworkElement && ((FrameworkElement)child).Name == name) {
                    return (T)child;
                }

                var childOfChild = FindVisualChild<T>(child, name);
                if (childOfChild != null) {
                    return childOfChild;
                }
            }
            return null;
        }

        private T FindDescendant<T>(DependencyObject parent, string name) where T : FrameworkElement {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childCount; i++) {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T frameworkElement && frameworkElement.Name == name) {
                    return frameworkElement;
                }

                var result = FindDescendant<T>(child, name);
                if (result != null) {
                    return result;
                }
            }

            return null;
        }

        private (int, int) SubTaskNumber(Expander expander, bool addOne = false, int overrideTotal = -1) {
            int total = 0;
            int completed = 0;

            if (expander.Content is StackPanel stackPanel) {
                foreach (var child in stackPanel.Children) {
                    if (child is ListView listView) {
                        foreach (var item in listView.Items) {
                            var container = listView.ContainerFromItem(item) as ListViewItem;
                            if (container != null) {
                                var checkBox = FindDescendant<CheckBox>(container, "SubTaskCheckBox");
                                if (checkBox != null) {
                                    total++;
                                    if (checkBox.IsChecked == true) {
                                        completed++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (addOne)
                total++;
            if (overrideTotal != -1)
                total = overrideTotal;
            return (total, completed);
        }

        public ResourceLoader resourceLoader = ResourceLoader.GetForCurrentView();

        public string listname { get; set; }
        public string listId { get; set; }

        private static SemaphoreSlim _updateSemaphore = new SemaphoreSlim(1, 1);

        private void UpdateFlyoutMenu(MenuFlyout flyout, ListTask task, Button btn) {
            flyout.Items.Remove(flyout.Items.FirstOrDefault(item => (item as MenuFlyoutItem)?.Tag?.ToString() == "Reminder"));

            if (!task.HasReminder()) {
                var addReminderItem = new MenuFlyoutItem {
                    Icon = new SymbolIcon(Symbol.Calendar),
                    Text = resourceLoader.GetString("AddReminder/Text"),
                    Tag = "Reminder"
                };
                Button addReminderBtn = new Button();
                addReminderBtn.Margin = new Thickness(0, 5, 0, 0);
                addReminderBtn.Content = resourceLoader.GetString("AddReminder/Text");
                addReminderBtn.Width = 250;

                CalendarDatePicker datePicker = new CalendarDatePicker();

                datePicker.Date = DateTime.Now;
                datePicker.MinDate = DateTime.Now;
                datePicker.Width = 250;

                TimePicker timePicker = new TimePicker();
                timePicker.Time = DateTime.Now.TimeOfDay;
                timePicker.Width = 250;
                timePicker.Margin = new Thickness(0, 5, 0, 0);

                StackPanel stackPanel = new StackPanel();
                stackPanel.Orientation = Orientation.Vertical;
                stackPanel.HorizontalAlignment = HorizontalAlignment.Stretch;

                stackPanel.Children.Add(datePicker);
                stackPanel.Children.Add(timePicker);
                stackPanel.Children.Add(addReminderBtn);

                Flyout timeChooser = new Flyout();
                timeChooser.Content = stackPanel;
                addReminderBtn.Click += (s, args) => {
                    DateTime date = new DateTime(datePicker.Date.Value.Year, datePicker.Date.Value.Month, datePicker.Date.Value.Day, timePicker.Time.Hours, timePicker.Time.Minutes, timePicker.Time.Seconds);
                    if (date > DateTime.Now) { task.AddReminder(date, listId); }
                    timeChooser.Hide();
                };

                addReminderItem.Click += (s, args) => {
                    timeChooser.ShowAt(btn, new FlyoutShowOptions() { Placement = FlyoutPlacementMode.Bottom });
                };

                flyout.Items.Add(addReminderItem);
            }
            else {
                var removeReminderItem = new MenuFlyoutItem {
                    Icon = new SymbolIcon(Symbol.Delete),
                    Text = resourceLoader.GetString("RemoveReminder/Text"),
                    Tag = "Reminder"
                };

                removeReminderItem.Click += (s, args) => {
                    task.RemoveReminder();
                    UpdateFlyoutMenu(flyout, task, btn);
                };

                flyout.Items.Add(removeReminderItem);
            }
        }

        private void HideShowProgress(Windows.UI.Xaml.Controls.ProgressBar progressBar) {
            if (progressBar.Value == 0) {
                progressBar.Visibility = Visibility.Collapsed;
            }
            else {
                progressBar.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region Other events

        private void AWClosed(AppWindow sender, AppWindowClosedEventArgs args) {
            ListTools.isAWOpen = false;
            cobtn.Visibility = Visibility.Visible;
            this.Frame.Navigate(typeof(EmptyPage));
        }

        private void ListRenamed(string oldname, string newname, string emoji) {
            testname.Text = newname;
        }

        private void TaskPage_ActualThemeChanged(FrameworkElement sender, object args) {
            if (ListTools.isAWOpen) {
                if (Settings.Theme == "System") {
                    if (Application.Current.RequestedTheme == ApplicationTheme.Dark) {
                        TPage.Background = new SolidColorBrush { Color = Color.FromArgb(255, 33, 33, 33) };
                    }
                    else if (Application.Current.RequestedTheme == ApplicationTheme.Light) {
                        TPage.Background = new SolidColorBrush { Color = Colors.White };
                    }
                }
                else {
                    if (Settings.Theme == "Dark") {
                        TPage.Background = new SolidColorBrush { Color = Color.FromArgb(255, 33, 33, 33) };
                    }
                    else if (Settings.Theme == "Light") {
                        TPage.Background = new SolidColorBrush { Color = Colors.White };
                    }
                }
            }

            Brush bg = Application.Current.Resources["LayerFillColorDefaultBrush"] as Brush;
            addTaskRect.Fill = bg;

            foreach (var item in taskListView.Items) {
                var container = taskListView.ContainerFromItem(item) as ListViewItem;
                if (container != null) {
                    var rootGrid = FindVisualChild<Grid>(container, "rootGrid");
                    if (rootGrid != null) {
                        rootGrid.Background = bg;
                    }
                }
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e) {
            listId = e.Parameter.ToString();
            (ListMetadata data, List<ListTask> tasks) = ListTools.ReadList(listId);
            if (e.Parameter != null) {
                string name = data.Name;
                testname.Text = name;
                listname = name;
            }
            base.OnNavigatedTo(e);

            await Task.Run(async () => {
                if (tasks != null && data != null) {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () => {
                        foreach (ListTask task in tasks) {
                            taskListView.Items.Add(task);
                        }
                    });
                }
            });
        }

        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) {
            if (!string.IsNullOrEmpty(args.QueryText)) {
                (ListMetadata metadata, List<ListTask> tasks) = ListTools.ReadList(listId);
                ListTask task = new ListTask() {
                    Name = args.QueryText,
                    CreationDate = DateTime.Now,
                    IsDone = false
                };
                tasks.Add(task);
                sender.Text = string.Empty;
                taskListView.Items.Add(task);
                ListTools.SaveList(listId, tasks, metadata);
            }
        }

        private void TaskStateChanged(object sender, RoutedEventArgs e) {
            ListTask tasktoChange = (sender as CheckBox).DataContext as ListTask;
            (ListMetadata data, List<ListTask> tasks) = ListTools.ReadList(listId);
            try {
                int index = tasks.FindIndex(task => task.CreationDate == tasktoChange.CreationDate);
                if (index != -1) {
                    tasktoChange.IsDone = (bool)(sender as CheckBox).IsChecked;
                    tasks[index] = tasktoChange;
                    ListTools.SaveList(listId, tasks, data);
                }
            }
            catch (Exception ex) { Debug.WriteLine("[Task state change] Exception occured: " + ex.Message); }

        }

        private void NameBox_SizeChanged(object sender, SizeChangedEventArgs e) {
            ChangeWidth(sender);
        }

        private void AutoSuggestBox_KeyDown(object sender, KeyRoutedEventArgs e) {
            if (e.Key == Windows.System.VirtualKey.Enter && !string.IsNullOrEmpty((sender as AutoSuggestBox).Text)) {
                (ListMetadata metadata, List<ListTask> tasks) = ListTools.ReadList(listId);
                ListTask task = new ListTask() {
                    Name = (sender as AutoSuggestBox).Text,
                    CreationDate = DateTime.Now,
                    IsDone = false
                };
                tasks.Add(task);
                taskListView.Items.Add(task);
                (sender as AutoSuggestBox).Text = string.Empty;
                ListTools.SaveList(listId, tasks, metadata);
            }
        }

        private void TaskAdded_Grid(object sender, RoutedEventArgs e) {
            ChangeWidth(NameBox);
            if ((sender as Expander).DataContext is ListTask listtask) {
                if (listtask.SubTasks != null && listtask.SubTasks.Count > 0) {
                    (sender as Expander).IsExpanded = true; // NAASTY workaround cause the tasks only count in the progressbar when its expanded..
                }
            }
        }

        private async void SubTaskStateChanged(object sender, RoutedEventArgs e) {
            var checkBox = sender as CheckBox;
            if (checkBox == null)
                return;

            var taskToChange = checkBox.DataContext as ListTask;
            if (taskToChange == null)
                return;

            try {
                await _updateSemaphore.WaitAsync();

                var currentList = ListTools.ReadList(listId);
                var tasks = currentList.Tasks;

                foreach (var task in tasks) {
                    var subTask = task.SubTasks.FirstOrDefault(st => st.CreationDate == taskToChange.CreationDate);
                    if (subTask != null) {
                        ChangeProgressBarValue(task);
                        subTask.IsDone = checkBox.IsChecked ?? false;
                        break;
                    }
                }
                ListTools.SaveList(listId, tasks, currentList.Metadata);
            }
            catch (Exception ex) { Debug.WriteLine("[Subtask state change] Exception occured: " + ex.Message); }
            finally {
                _updateSemaphore.Release();
            }
        }

        private void Progress_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) {
            HideShowProgress(sender as Windows.UI.Xaml.Controls.ProgressBar);
        }

        private void SubTaskBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) {
            (ListMetadata meta, List<ListTask> tasklist) = ListTools.ReadList(listId);
            ListTask parent = sender.DataContext as ListTask;
            int index = tasklist.FindIndex(task => task.CreationDate == parent?.CreationDate);
            if (parent != null) {

                if (!string.IsNullOrEmpty(sender.Text)) {
                    ListTask task2add = new ListTask {
                        CreationDate = DateTime.Now,
                        ParentCreationDate = parent.CreationDate,
                        IsDone = false,
                        Name = sender.Text,
                        SubTasks = new ObservableCollection<ListTask>()
                    };
                    try {
                        parent.SubTasks.Add(task2add);
                    }
                    catch {
                        if (parent.SubTasks == null) {
                            parent.SubTasks = new ObservableCollection<ListTask> { task2add };
                        }
                    }
                    ChangeProgressBarValue(parent, true);
                }
            }
            Debug.WriteLine(index);
            if (index > -1) {
                tasklist[index] = parent;
                ListTools.SaveList(listId, tasklist, meta);
            }
        }

        #endregion

    }
}