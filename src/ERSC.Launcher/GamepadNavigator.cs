using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ERSC.Launcher;

// Control-level controller actions. MainWindow decides what the face buttons mean; this handles the focused control.
public static class GamepadNavigator
{
    public static bool Move(bool forward) =>
        Keyboard.FocusedElement is UIElement element && element.MoveFocus(new TraversalRequest(forward ? FocusNavigationDirection.Next : FocusNavigationDirection.Previous));

    /// <summary>Left/right (or bumpers, when <paramref name="large"/>) on a slider or closed dropdown.</summary>
    public static bool Adjust(object? focused, int direction, bool large)
    {
        switch (focused)
        {
            case Slider slider:
                var small = slider.Maximum - slider.Minimum <= 20 ? 1 : 5;
                slider.Value = Math.Clamp(slider.Value + direction * (large ? small * 5 : small), slider.Minimum, slider.Maximum);
                return true;
            case ComboBox { IsDropDownOpen: false } combo when combo.Items.Count > 0:
                combo.SelectedIndex = Math.Clamp(combo.SelectedIndex + direction, 0, combo.Items.Count - 1);
                return true;
            default:
                return false;
        }
    }

    /// <summary>The A button on buttons, switches and dropdowns. Text fields are handled by the caller.</summary>
    public static bool Activate(object? focused)
    {
        switch (focused)
        {
            case CheckBox toggle:
                toggle.IsChecked = toggle.IsChecked != true;
                return true;
            case ButtonBase button when button.IsEnabled:
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
                return true;
            case ComboBox combo:
                combo.IsDropDownOpen = true;
                return true;
            default:
                return false;
        }
    }

    public static ComboBox? OpenComboFor(object? focused) => focused switch
    {
        ComboBoxItem item when ItemsControl.ItemsControlFromItemContainer(item) is ComboBox { IsDropDownOpen: true } combo => combo,
        ComboBox { IsDropDownOpen: true } combo => combo,
        _ => null
    };

    /// <summary>Moves the highlight inside an open dropdown by selecting and focusing the neighbouring item.</summary>
    public static void Step(ComboBox combo, int direction)
    {
        if (combo.Items.Count == 0) return;
        combo.SelectedIndex = Math.Clamp(combo.SelectedIndex + direction, 0, combo.Items.Count - 1);
        (combo.ItemContainerGenerator.ContainerFromIndex(combo.SelectedIndex) as ComboBoxItem)?.Focus();
    }
}

// Draws a gold outline around the focused control while a controller or keyboard is in use, and hides it for the mouse.
public sealed class FocusRing
{
    private readonly FrameworkElement _scope;
    private Adorner? _adorner;
    public bool Visible { get; private set; }

    public FocusRing(FrameworkElement scope)
    {
        _scope = scope;
        scope.AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) => Place(e.NewFocus)), true);
        scope.AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler((_, _) => Hide()), true);
        scope.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler((_, e) =>
        {
            if (e.Key is Key.Tab or Key.Up or Key.Down or Key.Left or Key.Right) Show();
        }), true);
    }

    public void Show()
    {
        Visible = true;
        Place(Keyboard.FocusedElement);
    }

    public void Hide()
    {
        Visible = false;
        Remove();
    }

    private void Place(IInputElement? focused)
    {
        Remove();
        if (!Visible || focused is not FrameworkElement element || element is Overlay || !_scope.IsAncestorOf(element)) return;
        element.BringIntoView(new Rect(-16, -48, element.ActualWidth + 32, element.ActualHeight + 96));
        var layer = AdornerLayer.GetAdornerLayer(element);
        if (layer is null) return;
        _adorner = new RingAdorner(element);
        layer.Add(_adorner);
    }

    private void Remove()
    {
        if (_adorner is null) return;
        AdornerLayer.GetAdornerLayer(_adorner.AdornedElement)?.Remove(_adorner);
        _adorner = null;
    }

    private sealed class RingAdorner : Adorner
    {
        private static readonly Pen Pen = new(MainWindow.GoldBrush, 2);
        public RingAdorner(UIElement element) : base(element) => IsHitTestVisible = false;
        protected override void OnRender(DrawingContext context)
        {
            var bounds = new Rect(AdornedElement.RenderSize);
            bounds.Inflate(4, 4);
            context.DrawRoundedRectangle(null, Pen, bounds, 6, 6);
        }
    }
}
