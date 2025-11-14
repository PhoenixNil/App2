using App2.Models;
using App2.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;

namespace App2.Views;

public sealed partial class ServerListControl : UserControl
{
    public ServerListControl()
    {
        InitializeComponent();
    }

    public MainWindowViewModel? ViewModel
    {
        get => (MainWindowViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainWindowViewModel),
            typeof(ServerListControl), new PropertyMetadata(null));

    private async void BtnImportSS_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var tbSSUrl = new TextBox
        {
            PlaceholderText = "粘贴 SS 链接 (ss://...)",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 120
        };

        var hint = new TextBlock
        {
            Text = "支持标准 ss:// 链接。",
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(hint);
        stack.Children.Add(tbSSUrl);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "从 SS 链接导入",
            Content = stack,
            PrimaryButtonText = "导入",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            var ssUrl = tbSSUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(ssUrl))
            {
                args.Cancel = true;
                tbSSUrl.Focus(FocusState.Programmatic);
                return;
            }

            var entry = ViewModel.ParseSSUrl(ssUrl);
            if (entry is null)
            {
                args.Cancel = true;
                tbSSUrl.Focus(FocusState.Programmatic);
                return;
            }

            dialog.Tag = entry;
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.Tag is ServerEntry entry)
        {
            ViewModel.AddServer(entry);
        }
    }

    private async void BtnAddManual_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var dialog = CreateServerDialog("手动添加服务器", null);
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.Tag is ServerEntry entry)
        {
            ViewModel.AddServer(entry);
        }
    }

    private async void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedServer == null)
        {
            return;
        }

        var dialog = CreateServerDialog("编辑服务器", ViewModel.SelectedServer.Clone());
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.Tag is ServerEntry entry)
        {
            ViewModel.UpdateServer(ViewModel.SelectedServer, entry);
        }
    }

    private async void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedServer == null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "确认删除",
            Content = $"确定要删除 {ViewModel.SelectedServer.Name}?",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            PrimaryButtonStyle = (Style)Application.Current.Resources["DangerAccentButtonStyle"],
            DefaultButton = ContentDialogButton.None
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.RemoveServer(ViewModel.SelectedServer);
        }
    }

    private ContentDialog CreateServerDialog(string title, ServerEntry? existing)
    {
        var tbName = new TextBox { PlaceholderText = "别名", Text = existing?.Name ?? string.Empty };
        var tbHost = new TextBox { PlaceholderText = "服务器地址", Text = existing?.Host ?? string.Empty };
        var tbPort = new TextBox { PlaceholderText = "端口", Text = existing?.Port.ToString() ?? string.Empty };
        var tbPassword = new TextBox { PlaceholderText = "密码", Text = existing?.Password ?? string.Empty };

        var cbMethod = new ComboBox
        {
            ItemsSource = new[]
            {
                "aes-128-gcm",
                "aes-256-gcm",
                "chacha20-ietf-poly1305",
                "2022-blake3-aes-256-gcm",
                "2022-blake3-aes-128-gcm",
                "2022-blake3-chacha20-poly1305"
            },
            PlaceholderText = "加密方式",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        if (existing is null)
        {
            cbMethod.SelectedIndex = -1;
        }
        else
        {
            cbMethod.SelectedItem = existing.Method;
        }

        var hintText = new TextBlock
        {
            Text = "注意：SS2022 密钥需要符合 Base64 长度要求。",
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap
        };

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(tbName);
        stack.Children.Add(tbHost);
        stack.Children.Add(tbPort);
        stack.Children.Add(tbPassword);
        stack.Children.Add(cbMethod);
        stack.Children.Add(hintText);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = stack,
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (ViewModel is null)
            {
                args.Cancel = true;
                return;
            }

            try
            {
                var method = cbMethod.SelectedItem as string ?? "aes-256-gcm";
                var entry = ViewModel.CreateServerEntry(
                    tbName.Text,
                    tbHost.Text,
                    tbPort.Text,
                    tbPassword.Text,
                    method);

                dialog.Tag = entry;
            }
            catch (ArgumentException)
            {
                args.Cancel = true;
                if (string.IsNullOrWhiteSpace(tbHost.Text))
                {
                    tbHost.Focus(FocusState.Programmatic);
                }
                else
                {
                    tbPort.Focus(FocusState.Programmatic);
                }
            }
        };

        return dialog;
    }

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

        var matches = ViewModel.Servers
            .Where(server => !string.IsNullOrEmpty(server.Name) &&
                server.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

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
