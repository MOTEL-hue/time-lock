using System.Windows;
using System.Windows.Controls;

namespace TimeLock.App;

public static class Dialogs
{
    /// <summary>חלון קלט קטן. מחזיר null אם בוטל.</summary>
    public static string? Ask(Window? owner, string title, string prompt, bool secret = true)
    {
        var w = new Window
        {
            Title = title, Owner = owner, SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize,
            FlowDirection = FlowDirection.RightToLeft, MinWidth = 340
        };
        var sp = new StackPanel { Margin = new Thickness(16) };
        sp.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
        Control box = secret ? new PasswordBox() : new TextBox();
        sp.Children.Add(box);
        var ok = new Button { Content = "אישור", IsDefault = true, Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(18, 4, 18, 4), HorizontalAlignment = HorizontalAlignment.Left };
        ok.Click += (_, _) => w.DialogResult = true;
        sp.Children.Add(ok);
        w.Content = sp;
        w.Loaded += (_, _) => box.Focus();
        if (w.ShowDialog() != true) return null;
        return box is PasswordBox pb ? pb.Password : ((TextBox)box).Text;
    }

    public static void Info(Window? owner, string msg) =>
        MessageBox.Show(owner, msg, "TimeLock", MessageBoxButton.OK, MessageBoxImage.Information,
            MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

    public static void Error(Window? owner, string msg) =>
        MessageBox.Show(owner, msg, "TimeLock", MessageBoxButton.OK, MessageBoxImage.Warning,
            MessageBoxResult.OK, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

    public static bool Confirm(Window? owner, string msg) =>
        MessageBox.Show(owner, msg, "TimeLock", MessageBoxButton.YesNo, MessageBoxImage.Question,
            MessageBoxResult.No, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign) == MessageBoxResult.Yes;
}
