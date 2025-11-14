using App2.Models;
using App2.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;

namespace App2.Views;

public sealed partial class ServerListControl : UserControl
{
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
}
