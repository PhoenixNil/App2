using App2.Models;
using App2.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections;
using System.Linq;
using Microsoft.UI.Xaml.Input;

namespace App2.Views;

public sealed partial class ServerListControl : UserControl
{
    private ServerEntry? _draggedItem;

    public ServerListControl()
    {
        InitializeComponent();
    }

    public ServerListViewModel? ViewModel
    {
        get => (ServerListViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ServerListViewModel),
            typeof(ServerListControl), new PropertyMetadata(null));

    private void ServerSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (ViewModel is null || args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        var query = sender.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            sender.ItemsSource = null;
            return;
        }

        var matches = ViewModel.SearchServers(query);

        sender.ItemsSource = matches;
    }

    private void ServerSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (args.SelectedItem is ServerEntry server)
        {
            ViewModel.SelectedServer = server;
            sender.Text = server.Name;
        }
    }

    private void ServerSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (args.ChosenSuggestion is ServerEntry chosenServer)
        {
            ViewModel.SelectedServer = chosenServer;
            return;
        }

        var query = args.QueryText?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            return;
        }

        var match = ViewModel.Servers.FirstOrDefault(server =>
            string.Equals(server.Name, query, StringComparison.OrdinalIgnoreCase));

        match ??= ViewModel.Servers.FirstOrDefault(server =>
            !string.IsNullOrEmpty(server.Name) &&
            server.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
            ViewModel.SelectedServer = match;
            sender.Text = match.Name;
        }
    }

    private void ServersListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        // 保存被拖拽的项（这里只处理单项拖拽）
        _draggedItem = e.Items?.FirstOrDefault() as ServerEntry;
    }

    private void ServersListView_DragOver(object sender, DragEventArgs e)
    {
        // 明确这是移动操作，允许放置
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        e.Handled = true;
    }

    private void ServersListView_Drop(object sender, DragEventArgs e)
    {
        if (_draggedItem is null)
            return;

        // 找到放置目标：通过 OriginalSource 的 DataContext 获取目标项（若在空白处放置则为 null）
        var element = e.OriginalSource as FrameworkElement;
        var targetItem = element?.DataContext as ServerEntry;

        // 获取底层集合（ItemsSource）
        if (ServersListView.ItemsSource is IList collection)
        {
            var oldIndex = collection.IndexOf(_draggedItem);
            var newIndex = -1;

            if (targetItem != null)
            {
                newIndex = collection.IndexOf(targetItem);
            }
            else
            {
                // 如果在空白区域放下，设为末尾
                newIndex = collection.Count - 1;
            }

            if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
            {
                // 对于 ObservableCollection，这里可以 RemoveAt/Insert 或者调用 Move（如果可用）
                // 先尝试调用 Move（如果集合实现了 IList<T> 和具有 Move 方法）
                try
                {
                    // 通用做法：Remove 然后 Insert，保持变更通知
                    collection.RemoveAt(oldIndex);
                    collection.Insert(newIndex, _draggedItem);
                }
                catch (Exception)
                {
                    // 保险回退：不做处理
                }
            }
        }

        // 清理
        _draggedItem = null;
        e.Handled = true;
    }
}
