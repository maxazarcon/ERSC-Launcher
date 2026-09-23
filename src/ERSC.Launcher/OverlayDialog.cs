using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ERSC.Launcher;

// A modal layer drawn inside the main window. Unlike native message boxes, it can be driven by a controller.
public abstract class Overlay : Grid
{
    protected Overlay()
    {
        Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xC0, 0x0B, 0x0D, 0x0A));
        Focusable = true;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.Cycle);
    }

    public abstract bool Handle(GamepadButton button);
    public abstract void FocusInitial();

    protected static Border Card(UIElement child, double maxWidth) => new()
    {
        Background = MainWindow.PanelBrush, BorderBrush = MainWindow.DividerBrush, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8), Padding = new Thickness(24), MaxWidth = maxWidth, Margin = new Thickness(24),
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = child
    };
}

public sealed class OverlayDialog : Overlay
{
    private readonly TaskCompletionSource<int> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<Button> _buttons = [];
    private readonly int _cancel;
    private readonly int _primary;

    public Task<int> Result => _result.Task;

    public OverlayDialog(string title, string message, IReadOnlyList<string> buttons, int primary = 0, int cancel = -1)
    {
        _primary = primary;
        _cancel = cancel < 0 ? buttons.Count - 1 : cancel;
        var stack = new StackPanel();
        var heading = MainWindow.Label(title, 18, MainWindow.TextBrush, new Thickness(0, 0, 0, 10)); heading.FontWeight = FontWeights.SemiBold;
        stack.Children.Add(heading);
        stack.Children.Add(MainWindow.Label(message, 14, MainWindow.TextBrush, new Thickness(0, 0, 0, 20)));
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        for (var i = 0; i < buttons.Count; i++)
        {
            var index = i;
            var button = new Button { Content = buttons[i], Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0) };
            MainWindow.StyleButton(button, primary: i == primary);
            button.Click += (_, _) => Close(index);
            _buttons.Add(button); row.Children.Add(button);
        }
        stack.Children.Add(row);
        Children.Add(Card(stack, 520));
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(_cancel); e.Handled = true; } };
    }

    public void Close(int index) => _result.TrySetResult(index);
    public override void FocusInitial() => _buttons[_primary].Focus();

    public override bool Handle(GamepadButton button)
    {
        var focused = _buttons.FindIndex(b => b.IsKeyboardFocused);
        switch (button)
        {
            case GamepadButton.Left or GamepadButton.Up:
                _buttons[focused < 0 ? _primary : Math.Max(0, focused - 1)].Focus(); return true;
            case GamepadButton.Right or GamepadButton.Down:
                _buttons[focused < 0 ? _primary : Math.Min(_buttons.Count - 1, focused + 1)].Focus(); return true;
            case GamepadButton.A:
                if (focused < 0) { FocusInitial(); return true; }
                _buttons[focused].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, _buttons[focused])); return true;
            case GamepadButton.B:
                Close(_cancel); return true;
            default:
                return false;
        }
    }
}
