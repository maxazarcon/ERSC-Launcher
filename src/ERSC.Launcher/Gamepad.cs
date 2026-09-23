using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ERSC.Launcher;

public enum GamepadButton { Up, Down, Left, Right, A, B, X, Y, Start, Back, LeftShoulder, RightShoulder }

// Polls XInput controllers. Steam Input presents PlayStation, Switch and Deck controls as XInput devices
// for non-Steam shortcuts, so this covers the controllers people use in Big Picture mode.
public sealed class Gamepad : IDisposable
{
    private const short StickThreshold = 16000;
    private const short ScrollDeadzone = 8000;
    private static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(90);
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(1);
    private static readonly GamepadButton[] Repeating = [GamepadButton.Up, GamepadButton.Down, GamepadButton.Left, GamepadButton.Right, GamepadButton.LeftShoulder, GamepadButton.RightShoulder];

    private readonly DispatcherTimer _timer;
    private readonly Dictionary<GamepadButton, DateTime> _held = [];
    private DateTime _nextProbe;
    private bool _available = true;
    private bool _ignoreHeld = true;

    public event Action<GamepadButton>? Pressed;
    public event Action<bool>? ConnectionChanged;
    /// <summary>Raised each poll while the right stick is pushed, from -1 (down) to 1 (up).</summary>
    public event Action<double>? Scrolled;
    public bool Connected { get; private set; }
    public bool Enabled { get; set; } = true;

    public Gamepad(Dispatcher dispatcher)
    {
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Input, (_, _) => Poll(), dispatcher);
        _timer.Start();
    }

    private void Poll()
    {
        if (!_available || !Enabled) { Release(); return; }
        var now = DateTime.UtcNow;
        if (!Connected && now < _nextProbe) return;
        var down = new HashSet<GamepadButton>();
        var any = false;
        short scroll = 0;
        for (var index = 0; index < 4; index++)
        {
            if (!TryGetState(index, out var state)) continue;
            any = true;
            var pad = state.Gamepad;
            Map(pad.Buttons, down);
            if (pad.ThumbLY > StickThreshold) down.Add(GamepadButton.Up);
            if (pad.ThumbLY < -StickThreshold) down.Add(GamepadButton.Down);
            if (pad.ThumbLX < -StickThreshold) down.Add(GamepadButton.Left);
            if (pad.ThumbLX > StickThreshold) down.Add(GamepadButton.Right);
            if (Math.Abs((int)pad.ThumbRY) > Math.Abs((int)scroll)) scroll = pad.ThumbRY;
            if (!Connected) break; // While probing, one controller is enough to switch to full-rate polling.
        }
        if (any != Connected) { Connected = any; ConnectionChanged?.Invoke(any); }
        if (!any) { _nextProbe = now + ProbeInterval; Release(); return; }
        if (Math.Abs((int)scroll) >= ScrollDeadzone) Scrolled?.Invoke(scroll / 32768.0);

        foreach (var released in _held.Keys.Where(b => !down.Contains(b)).ToArray()) _held.Remove(released);
        if (_ignoreHeld)
        {
            // A button still held from Big Picture (the press that started us) must not act on this window.
            foreach (var button in down) _held[button] = DateTime.MaxValue;
            _ignoreHeld = false;
            return;
        }
        foreach (var button in down)
        {
            if (!_held.TryGetValue(button, out var next))
            {
                _held[button] = now + RepeatDelay;
                Pressed?.Invoke(button);
            }
            else if (Repeating.Contains(button) && now >= next)
            {
                _held[button] = now + RepeatInterval;
                Pressed?.Invoke(button);
            }
        }
    }

    private void Release()
    {
        _held.Clear();
        _ignoreHeld = true;
    }

    private static void Map(ushort buttons, HashSet<GamepadButton> down)
    {
        if ((buttons & 0x0001) != 0) down.Add(GamepadButton.Up);
        if ((buttons & 0x0002) != 0) down.Add(GamepadButton.Down);
        if ((buttons & 0x0004) != 0) down.Add(GamepadButton.Left);
        if ((buttons & 0x0008) != 0) down.Add(GamepadButton.Right);
        if ((buttons & 0x0010) != 0) down.Add(GamepadButton.Start);
        if ((buttons & 0x0020) != 0) down.Add(GamepadButton.Back);
        if ((buttons & 0x0100) != 0) down.Add(GamepadButton.LeftShoulder);
        if ((buttons & 0x0200) != 0) down.Add(GamepadButton.RightShoulder);
        if ((buttons & 0x1000) != 0) down.Add(GamepadButton.A);
        if ((buttons & 0x2000) != 0) down.Add(GamepadButton.B);
        if ((buttons & 0x4000) != 0) down.Add(GamepadButton.X);
        if ((buttons & 0x8000) != 0) down.Add(GamepadButton.Y);
    }

    private bool TryGetState(int index, out XInputState state)
    {
        state = default;
        try { return XInput14(index, out state) == 0; }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        try { return XInput91(index, out state) == 0; }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { _available = false; return false; }
    }

    public void Dispose() => _timer.Stop();

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInput14(int userIndex, out XInputState state);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    private static extern int XInput91(int userIndex, out XInputState state);
}
