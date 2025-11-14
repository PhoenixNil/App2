using App2.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.ApplicationModel.DataTransfer;

namespace App2.Views;

public sealed partial class ControlPanelControl : UserControl
{
    public ControlPanelControl()
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
            typeof(ControlPanelControl), new PropertyMetadata(null));

    public event EventHandler<ElementTheme>? ThemeChangeRequested;

    private async void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (!ViewModel.IsRunning && ViewModel.SelectedServer == null)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "未选择服务器",
                Content = "请先选择一个服务器节点",
                CloseButtonText = "确定"
            };

            await dialog.ShowAsync();
            BtnStartStop.IsChecked = ViewModel.IsRunning;
            return;
        }

        try
        {
            await ViewModel.StartStopCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "启动失败",
                Content = ex.Message,
                CloseButtonText = "确定"
            };

            await dialog.ShowAsync();
        }

        BtnStartStop.IsChecked = ViewModel.StartStopButtonChecked;
    }

    private async void EditLocalPortMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var numberBox = new NumberBox
        {
            Header = "本地端口",
            Minimum = 1024,
            Maximum = 65535,
            Value = int.Parse(ViewModel.LocalPortText),
            ValidationMode = NumberBoxValidationMode.Disabled,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden
        };

        var rangeText = new TextBlock
        {
            Text = "有效范围：1024 - 65535",
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap
        };

        var errorText = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap
        };

        var contentPanel = new StackPanel { Spacing = 8 };
        contentPanel.Children.Add(numberBox);
        contentPanel.Children.Add(rangeText);
        contentPanel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "编辑本地端口",
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            Content = contentPanel
        };

        var portChanged = false;

        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (ViewModel.ValidateAndUpdateLocalPort(numberBox.Value, out var errorMessage))
            {
                portChanged = true;
            }
            else if (errorMessage != null)
            {
                errorText.Text = errorMessage;
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        };

        await dialog.ShowAsync();

        if (portChanged && ViewModel.IsRunning)
        {
            var reminderDialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "提示",
                Content = "端口号已更新，停止并重新启动服务后生效。",
                CloseButtonText = "知道了"
            };

            await reminderDialog.ShowAsync();
        }
    }

    private async void ViewLogsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var logText = ViewModel.GetLogsText();
        var hasLogs = ViewModel.HasLogs;

        var textBlock = new TextBlock
        {
            Text = logText,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13
        };

        var scrollViewer = new ScrollViewer
        {
            Content = textBlock,
            MaxHeight = 320,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "近期日志",
            Content = scrollViewer,
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close
        };

        if (hasLogs)
        {
            dialog.PrimaryButtonText = "复制全部";
            dialog.DefaultButton = ContentDialogButton.Primary;
            dialog.PrimaryButtonClick += (_, _) =>
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(logText);
                Clipboard.SetContent(dataPackage);
                Clipboard.Flush();
            };
        }

        await dialog.ShowAsync();
    }

    private void ThemeButton2Click(object sender, RoutedEventArgs e)
    {
        TestButton2TeachingTip.IsOpen = true;
    }

    private void GlobalRouteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.IsBypassChinaMode = false;
    }

    private void BypassChinaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.IsBypassChinaMode = true;
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag)
        {
            return;
        }

        if (!Enum.TryParse<ElementTheme>(tag, true, out var theme))
        {
            return;
        }

        ThemeChangeRequested?.Invoke(this, theme);
        TestButton2TeachingTip.IsOpen = false;
    }

    public void UpdateThemeButtonsState(ElementTheme actualTheme)
    {
        if (TestButton2TeachingTip.Content is not StackPanel panel)
        {
            return;
        }

        foreach (var child in panel.Children)
        {
            if (child is Button themeButton && themeButton.Tag is string tag &&
                Enum.TryParse<ElementTheme>(tag, true, out var theme))
            {
                if (theme == ElementTheme.Default)
                {
                    themeButton.IsEnabled = true;
                }
                else
                {
                    themeButton.IsEnabled = theme != actualTheme;
                }
            }
        }
    }
}
