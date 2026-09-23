using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ERSC.Launcher;

// Text entry for controller users: a key grid moved with the D-pad. Physical keyboards also type into it.
public sealed class OnScreenKeyboard : Overlay
{
    private const int Columns = 10;
    private static readonly string[] Letters = ["1234567890", "qwertyuiop", "asdfghjkl-", "zxcvbnm_.@"];
    private static readonly string[] Symbols = ["1234567890", "!#$%^&*()+", "=[]{};:'\"~", ",<>/?\\|`€£"];
    private static readonly string[] Actions = ["Shift", "?123", "Space", "Delete", "Done"];

    private readonly TaskCompletionSource<string?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Button[,] _keys = new Button[Letters.Length, Columns];
    private readonly Button[] _actions = new Button[Actions.Length];
    private readonly TextBlock _preview = new() { FontSize = 18, TextWrapping = TextWrapping.Wrap, MinHeight = 26 };
    private readonly bool _password;
    private string _text;
    private bool _shift;
    private bool _symbols;
    private int _row = 1;
    private int _column;

    public Task<string?> Result => _result.Task;
    public string Text => _text;

    public OnScreenKeyboard(string title, string text, bool password)
    {
        _text = text; _password = password;
        var stack = new StackPanel();
        var heading = MainWindow.Label(title, 18, MainWindow.TextBrush, new Thickness(0, 0, 0, 10)); heading.FontWeight = FontWeights.SemiBold;
        stack.Children.Add(heading);
        stack.Children.Add(new Border
        {
            Background = MainWindow.BackgroundBrush, BorderBrush = MainWindow.MutedBrush, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 16), Child = _preview
        });
        _preview.Foreground = MainWindow.TextBrush;

        var grid = new Grid();
        for (var c = 0; c < Columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var r = 0; r <= Letters.Length; r++) grid.RowDefinitions.Add(new RowDefinition());
        for (var r = 0; r < Letters.Length; r++)
            for (var c = 0; c < Columns; c++)
            {
                var (row, column) = (r, c);
                var key = MakeKey(() => Append(Current(row, column)));
                Grid.SetRow(key, r); Grid.SetColumn(key, c); grid.Children.Add(key);
                _keys[r, c] = key;
            }
        for (var i = 0; i < Actions.Length; i++)
        {
            var index = i;
            var key = MakeKey(() => Act(index));
            Grid.SetRow(key, Letters.Length); Grid.SetColumn(key, i * 2); Grid.SetColumnSpan(key, 2); grid.Children.Add(key);
            _actions[i] = key;
        }
        stack.Children.Add(grid);
        stack.Children.Add(MainWindow.Label("A type · X delete · Y shift · ☰ done · B cancel", 12, MainWindow.MutedBrush, new Thickness(0, 14, 0, 0)));
        Children.Add(Card(stack, 640));

        PreviewTextInput += (_, e) =>
        {
            foreach (var ch in e.Text.Where(ch => !char.IsControl(ch))) _text += ch;
            e.Handled = true; Refresh();
        };
        PreviewKeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Back: Backspace(); break;
                case Key.Enter: Finish(_text); break;
                case Key.Escape: Finish(null); break;
                case Key.Space: _text += " "; Refresh(); break;
                default: return;
            }
            e.Handled = true;
        };
        Refresh();
    }

    public override void FocusInitial() => Focus();

    public override bool Handle(GamepadButton button)
    {
        switch (button)
        {
            case GamepadButton.Up: _row = Math.Max(0, _row - 1); break;
            case GamepadButton.Down: _row = Math.Min(Letters.Length, _row + 1); break;
            case GamepadButton.Left: _column = _row == Letters.Length ? Math.Max(0, (_column / 2 - 1) * 2) : Math.Max(0, _column - 1); break;
            case GamepadButton.Right: _column = _row == Letters.Length ? Math.Min(Columns - 2, (_column / 2 + 1) * 2) : Math.Min(Columns - 1, _column + 1); break;
            case GamepadButton.A: (_row == Letters.Length ? _actions[_column / 2] : _keys[_row, _column]).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); return true;
            case GamepadButton.X: Backspace(); return true;
            case GamepadButton.Y: _shift = !_shift; break;
            case GamepadButton.Start: Finish(_text); return true;
            case GamepadButton.B: Finish(null); return true;
            default: return false;
        }
        Refresh();
        return true;
    }

    private static Button MakeKey(Action press)
    {
        var key = new Button { Focusable = false, Margin = new Thickness(3), MinWidth = 40, MinHeight = 40 };
        MainWindow.StyleButton(key);
        key.Padding = new Thickness(4);
        key.Click += (_, _) => press();
        return key;
    }

    private string Current(int row, int column)
    {
        var ch = (_symbols ? Symbols : Letters)[row][column].ToString();
        return _shift ? ch.ToUpperInvariant() : ch;
    }

    private void Append(string ch)
    {
        _text += ch;
        if (_shift && !_symbols) _shift = false; // Shift applies to one letter, like a phone keyboard.
        Refresh();
    }

    private void Act(int index)
    {
        switch (Actions[index])
        {
            case "Shift": _shift = !_shift; break;
            case "?123": _symbols = !_symbols; break;
            case "Space": _text += " "; break;
            case "Delete": Backspace(); return;
            default: Finish(_text); return;
        }
        Refresh();
    }

    private void Backspace()
    {
        if (_text.Length > 0) _text = _text[..^1];
        Refresh();
    }

    private void Finish(string? value) => _result.TrySetResult(value);

    private void Refresh()
    {
        // Mask a password except for its last character so the typed letter can be checked.
        _preview.Text = (_password && _text.Length > 0 ? new string('•', _text.Length - 1) + _text[^1] : _text) + "▏";
        for (var r = 0; r < Letters.Length; r++)
            for (var c = 0; c < Columns; c++)
            {
                _keys[r, c].Content = Current(r, c);
                Highlight(_keys[r, c], r == _row && c == _column);
            }
        _actions[0].Content = _shift ? "SHIFT" : "Shift";
        _actions[1].Content = _symbols ? "abc" : "?123";
        for (var i = 0; i < _actions.Length; i++) Highlight(_actions[i], _row == Letters.Length && i == _column / 2);
    }

    private static void Highlight(Button key, bool selected)
    {
        key.Background = selected ? MainWindow.GoldBrush : MainWindow.PanelBrush;
        key.Foreground = selected ? MainWindow.BackgroundBrush : MainWindow.TextBrush;
    }
}
