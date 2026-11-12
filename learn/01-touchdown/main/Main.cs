using Godot;

namespace Touchdown;

/// <summary>
/// Runs one game: puts the ship on the first pad, listens for touchdowns and crashes, moves the camera and feeds the
/// HUD. Land on each pad in order; the last one wins.
/// </summary>
public partial class Main : Node2D
{
	/// <summary>Fuel given back for each pad you land on.</summary>
	[Export] public float RefuelPerPad = 45f;
	/// <summary>How long the ship must sit still on a pad before the landing counts, in seconds.</summary>
	[Export] public float SettleTime = 0.8f;

	enum Phase { Flying, Won, Lost }

	Terrain _terrain = null!;
	Ship _ship = null!;
	Camera2D _camera = null!;
	Hud _hud = null!;

	Phase _phase = Phase.Flying;
	int _target = 1;
	Pad? _touching;
	float _settle, _stuck, _time, _shake;

	public override void _Ready()
	{
		_terrain = GetNode<Terrain>("Terrain");
		_ship = GetNode<Ship>("Ship");
		_camera = GetNode<Camera2D>("Camera");
		_hud = GetNode<Hud>("Hud");

		// The ship starts parked on pad 0. Its feet are 112 pixels below its centre.
		Pad start = _terrain.Pads[0];
		_ship.GlobalPosition = start.GlobalPosition + new Vector2(0, -112);
		start.Status = Pad.State.Done;
		_terrain.Pads[_target].Status = Pad.State.Target;
		_camera.GlobalPosition = _ship.GlobalPosition;

		// Signals from the ship. These only fire once YOU 4 in Ship.cs emits them.
		_ship.TouchedDown += OnTouchedDown;
		_ship.Crashed += OnCrashed;

		_hud.ShowPad(_target, _terrain.PadCount);
		_hud.ShowMessage("Walk to a lever (A/D), push it up (W).\nThe other lever is at the far end.");

		if (DevTools.Wanted()) AddChild(new DevTools());
	}

	void OnTouchedDown(Node2D body)
	{
		if (_phase != Phase.Flying) return;
		if (body is Pad pad && pad.Index == _target) _touching = pad;
	}

	void OnCrashed(float speed, float tiltDegrees)
	{
		if (_phase != Phase.Flying) return;
		Explode(_ship.GlobalPosition);
		string why = speed > _ship.SafeSpeed
			? $"Hit at {speed:0} (safe is {_ship.SafeSpeed:0})"
			: $"Tilted {tiltDegrees:0}° (safe is {_ship.SafeTiltDegrees:0}°)";
		Lose($"CRASHED\n{why}\n\nR to try again");
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (_phase != Phase.Flying) return;
		_time += dt;

		// A landing counts once the ship has sat still on the target pad for a moment, so bouncing off doesn't count.
		bool still = _ship.LinearVelocity.Length() < 6f && Mathf.Abs(_ship.AngularVelocity) < 0.08f;
		if (_touching != null && still && _ship.GetCollidingBodies().Contains(_touching))
		{
			_settle += dt;
			if (_settle >= SettleTime) Landed(_touching);
		}
		else _settle = 0;

		// Out of fuel and not going anywhere, or fallen off the world: the run is over.
		_stuck = _ship.Fuel <= 0 && still ? _stuck + dt : 0;
		if (_stuck > 2f || _ship.GlobalPosition.Y > 2800f) Lose("STRANDED\nNo fuel, no way home.\n\nR to try again");
	}

	void Landed(Pad pad)
	{
		pad.Status = Pad.State.Done;
		_touching = null;
		_settle = 0;
		_target++;
		_ship.Fuel = Mathf.Min(_ship.MaxFuel, _ship.Fuel + RefuelPerPad);
		_hud.ShowPad(_target, _terrain.PadCount);
		if (_target >= _terrain.PadCount)
		{
			_phase = Phase.Won;
			_hud.ShowMessage($"TOUCHDOWN\nEvery pad in {_time:0} s with {_ship.Fuel:0} fuel left.\n\nR to fly again");
			return;
		}
		_terrain.Pads[_target].Status = Pad.State.Target;
		_hud.ShowMessage($"Pad {pad.Index} done. +{RefuelPerPad:0} fuel.\nNext: pad {_target}.");
		GetTree().CreateTimer(2.5).Timeout += () => { if (_phase == Phase.Flying) _hud.ShowMessage(""); };
	}

	void Lose(string message)
	{
		_phase = Phase.Lost;
		_hud.ShowMessage(message);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		// Camera: lead a little in the direction of travel, and zoom out the higher the ship is above the ground.
		Vector2 lead = _ship.LinearVelocity * 0.35f;
		_camera.GlobalPosition = _camera.GlobalPosition.Lerp(_ship.GlobalPosition + lead, 1f - Mathf.Exp(-4f * dt));
		float altitude = _terrain.GroundY(_ship.GlobalPosition.X) - _ship.GlobalPosition.Y;
		float zoom = Mathf.Clamp(Mathf.Remap(altitude, 250f, 1600f, 1.4f, 0.45f), 0.45f, 1.4f);
		_camera.Zoom = _camera.Zoom.Lerp(new Vector2(zoom, zoom), 1f - Mathf.Exp(-2f * dt));

		// Crash shake.
		_shake = Mathf.Max(0, _shake - dt * 1.2f);
		_camera.Offset = new Vector2(GD.Randf() - 0.5f, GD.Randf() - 0.5f) * 40f * _shake;

		_hud.ShowShip(_ship, _ship.SafeSpeed, _ship.SafeTiltDegrees);
		_hud.PointAt(_target < _terrain.PadCount
			? GetViewport().GetCanvasTransform() * _terrain.Pads[_target].GlobalPosition
			: null);
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e.IsActionPressed("restart")) GetTree().ReloadCurrentScene();
		else if (e.IsActionPressed("back")) GetTree().ChangeSceneToFile("res://title/Title.tscn");
	}

	void Explode(Vector2 at)
	{
		var burst = new CpuParticles2D
		{
			Position = at, OneShot = true, Amount = 120, Lifetime = 1.6, Explosiveness = 0.95f,
			Direction = Vector2.Up, Spread = 180f, InitialVelocityMin = 120f, InitialVelocityMax = 420f,
			Gravity = new Vector2(0, 200), ScaleAmountMin = 3f, ScaleAmountMax = 7f,
			Color = new Color(1f, 0.6f, 0.2f),
		};
		AddChild(burst);
		burst.Emitting = true;
		_shake = 1f;
	}
}
