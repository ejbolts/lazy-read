using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LazyRead;

public sealed class SampleWindow : Window
{
    public SampleWindow()
    {
        Title = "Lazy Read — Sample document";
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Width = 950; Height = 630; Left = 120; Top = 120;
        var grid = new Grid { Margin = new Thickness(32) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        var title = new TextBlock { Text = "A little less reading. A little more listening.", FontSize = 27, FontWeight = FontWeights.SemiBold };
        var hint = new TextBlock { Text = "Highlight a sentence below, then press your reading shortcut. Right-click remains available.", Margin = new Thickness(0, 12, 0, 24), Foreground = new SolidColorBrush(Color.FromRgb(158, 175, 187)) };
        var text = new TextBox { Text = "Lazy Read gives your selected text a voice.\n\nThis is a sample document for testing the reading shortcut. Nothing here is private.\n\nYou can keep working while the local model prepares your audio. Pause, resume, or stop whenever you like.\n\nconst greeting = \"Hello from Lazy Read\";\nconsole.log(greeting);", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontSize = 20 };
        System.Windows.Automation.AutomationProperties.SetName(text, "Sample document text");
        Grid.SetRow(hint, 1); Grid.SetRow(text, 2); grid.Children.Add(title); grid.Children.Add(hint); grid.Children.Add(text); Content = grid;
        Closed += (_, _) => Application.Current.Shutdown();
    }
}
