using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using RugbyLeague.Game.Graphics;

namespace RugbyLeague.Game.Input;

public readonly record struct Pointer(int Id, Vector2 Position, bool IsDown, bool WasDown)
{
    public bool Pressed => IsDown && !WasDown;
    public bool Released => !IsDown && WasDown;
}

/// <summary>
/// Unifies touch, mouse and keyboard. Touch is the primary input on phones and tablets; the mouse
/// mirrors it for desktop debugging. All positions are converted into virtual units.
/// </summary>
public sealed class InputState
{
    private readonly VirtualViewport _viewport;
    private readonly Dictionary<int, Vector2> _previousPositions = new();
    private readonly HashSet<int> _previousDown = new();
    private readonly List<Pointer> _pointers = new();

    private MouseState _mouse;
    private MouseState _previousMouse;
    private KeyboardState _keyboard;
    private KeyboardState _previousKeyboard;

    public InputState(VirtualViewport viewport)
    {
        _viewport = viewport;
    }

    private readonly List<(int Id, Vector2 Position, bool Down)> _pendingTouches = new();
    private readonly Dictionary<int, Vector2> _heldTouches = new();
    private bool _platformTouchSource;

    /// <summary>
    /// Platform heads that deliver touches themselves (iOS) push them here in screen pixels.
    /// Once a touch arrives this way the touch panel and mouse are ignored.
    /// </summary>
    public void InjectTouch(int id, Vector2 screenPosition, bool down)
    {
        _pendingTouches.Add((id, screenPosition, down));
        _platformTouchSource = true;
    }

    public IReadOnlyList<Pointer> Pointers => _pointers;

    public Vector2 Pointer { get; private set; }
    public Vector2 PointerDelta { get; private set; }
    public bool PointerDown { get; private set; }
    public bool PointerPressed { get; private set; }
    public bool PointerReleased { get; private set; }
    public bool PointerMoved { get; private set; }

    /// <summary>Wheel or pinch zoom amount; screens that support scrolling consume this.</summary>
    public float ScrollDelta { get; private set; }

    public Vector2 MoveAxis { get; private set; }
    public bool BackPressed { get; private set; }

    public void Update(GameTime gameTime)
    {
        _previousMouse = _mouse;
        _mouse = Mouse.GetState();
        _previousKeyboard = _keyboard;
        _keyboard = Keyboard.GetState();

        var previousPointer = Pointer;
        var wasDown = PointerDown;

        _pointers.Clear();

        if (_platformTouchSource)
        {
            var released = new List<(int Id, Vector2 Position)>();
            foreach (var (id, position, down) in _pendingTouches)
            {
                if (down) _heldTouches[id] = position;
                else if (_heldTouches.Remove(id)) released.Add((id, position));
            }
            _pendingTouches.Clear();

            foreach (var (id, position) in released)
                _pointers.Add(new Pointer(id, _viewport.ToVirtual(position), false, _previousDown.Contains(id)));

            foreach (var (id, position) in _heldTouches)
                _pointers.Add(new Pointer(id, _viewport.ToVirtual(position), true, _previousDown.Contains(id)));

            // Keep the last position so the pointer does not jump when every finger lifts.
            if (_pointers.Count == 0)
                _pointers.Add(new Pointer(-2, Pointer, false, false));
        }
        else
        {
            var touches = TouchPanel.GetState();
            foreach (var touch in touches)
            {
                var id = touch.Id;
                var position = _viewport.ToVirtual(new Vector2(touch.Position.X, touch.Position.Y));
                var down = touch.State is TouchLocationState.Pressed or TouchLocationState.Moved;

                _pointers.Add(new Pointer(id, position, down, _previousDown.Contains(id)));
            }

            if (touches.Count == 0)
            {
                var position = _viewport.ToVirtual(new Vector2(_mouse.X, _mouse.Y));
                var down = _mouse.LeftButton == ButtonState.Pressed;
                _pointers.Add(new Pointer(-1, position, down, _previousDown.Contains(-1)));
            }
        }

        var primary = _pointers[0];
        Pointer = primary.Position;
        PointerDown = primary.IsDown;
        PointerPressed = primary.Pressed;
        PointerReleased = primary.Released;

        PointerDelta = _pointers.Count > 0 && _previousPositions.TryGetValue(primary.Id, out var previous)
            ? Pointer - previous
            : Vector2.Zero;
        PointerMoved = PointerDelta.LengthSquared() > 1f;

        ScrollDelta = _mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;

        _previousDown.Clear();
        _previousPositions.Clear();
        foreach (var pointer in _pointers)
        {
            if (pointer.IsDown) _previousDown.Add(pointer.Id);
            _previousPositions[pointer.Id] = pointer.Position;
        }

        BackPressed = IsNewKeyPress(Keys.Escape);

        var axis = Vector2.Zero;
        if (_keyboard.IsKeyDown(Keys.A) || _keyboard.IsKeyDown(Keys.Left)) axis.X -= 1;
        if (_keyboard.IsKeyDown(Keys.D) || _keyboard.IsKeyDown(Keys.Right)) axis.X += 1;
        if (_keyboard.IsKeyDown(Keys.W) || _keyboard.IsKeyDown(Keys.Up)) axis.Y -= 1;
        if (_keyboard.IsKeyDown(Keys.S) || _keyboard.IsKeyDown(Keys.Down)) axis.Y += 1;

        var pad = GamePad.GetState(PlayerIndex.One);
        if (pad.IsConnected)
        {
            axis += pad.ThumbSticks.Left * new Vector2(1, -1);
            if (pad.Buttons.Back == ButtonState.Pressed) BackPressed = true;
        }

        if (axis.LengthSquared() > 1) axis.Normalize();
        MoveAxis = axis;

        _ = previousPointer;
        _ = wasDown;
        _ = gameTime;
    }

    public bool IsKeyDown(Keys key) => _keyboard.IsKeyDown(key);
    public bool IsNewKeyPress(Keys key) => _keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    public bool PressedIn(Rectangle area) => PointerPressed && area.Contains(Pointer.ToPoint());
    public bool Holding(Rectangle area) => PointerDown && area.Contains(Pointer.ToPoint());

    /// <summary>True while any finger is held inside the area (used by the 3D control pad).</summary>
    public bool AnyPointerIn(Rectangle area)
    {
        foreach (var pointer in _pointers)
        {
            if (pointer.IsDown && area.Contains(pointer.Position.ToPoint())) return true;
        }
        return false;
    }

    public bool AnyPointerPressedIn(Rectangle area)
    {
        foreach (var pointer in _pointers)
        {
            if (pointer.Pressed && area.Contains(pointer.Position.ToPoint())) return true;
        }
        return false;
    }
}
