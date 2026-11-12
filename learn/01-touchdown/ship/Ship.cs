using Godot;

namespace Touchdown;

/// <summary>
/// The ship flying in the world: a RigidBody2D pushed around by gravity, its two thrusters and the weight of whatever
/// is moving around inside it.
///
/// The cabin is not in this world. It lives in its own physics world inside the InteriorViewport (a SubViewport),
/// and the InteriorView sprite shows that viewport's picture on the hull. Every physics frame the ship tells the cabin
/// which way "down" feels like from inside, and the cabin tells the ship where its weight is.
/// </summary>
public partial class Ship : RigidBody2D
{
	/// <summary>Emitted when the ship touches something gently enough to survive.</summary>
	[Signal] public delegate void TouchedDownEventHandler(Node2D body);

	/// <summary>Emitted when the ship hits something too hard or at too steep an angle.</summary>
	[Signal] public delegate void CrashedEventHandler(float speed, float tiltDegrees);

	[ExportGroup("Engines")]
	/// <summary>Push of one thruster at full throttle, in force units. Both together lift about 1.7 times the loaded ship.</summary>
	[Export] public float MaxThrust = 2300f;
	[Export] public float MaxFuel = 100f;
	/// <summary>Fuel used per second by one thruster at full throttle.</summary>
	[Export] public float BurnPerSecond = 1.4f;

	[ExportGroup("Landing")]
	/// <summary>Fastest touch that is still a landing, in pixels per second.</summary>
	[Export] public float SafeSpeed = 70f;
	/// <summary>Steepest tilt that is still a landing, in degrees.</summary>
	[Export] public float SafeTiltDegrees = 14f;

	[ExportGroup("Cabin")]
	/// <summary>How much stronger gravity feels inside the cabin than outside, so walking is snappy while the ship floats.</summary>
	[Export] public float CabinGravityScale = 4.5f;
	/// <summary>The most the cabin can feel, as a multiple of normal gravity, so a crash throws you but doesn't fling you through walls.</summary>
	[Export] public float CabinMaxG = 5f;

	public float Fuel { get; set; }
	public bool Wrecked { get; private set; }
	public Interior Interior { get; private set; } = null!;

	/// <summary>The ship's velocity on the previous physics frame. By the time BodyEntered fires, the collision has
	/// already slowed the ship down, so use this to judge how hard it hit.</summary>
	public Vector2 PreviousVelocity { get; private set; }

	/// <summary>Tilt away from upright, in degrees, always positive.</summary>
	public float TiltDegrees => Mathf.Abs(Mathf.RadToDeg(Mathf.Wrap(Rotation, -Mathf.Pi, Mathf.Pi)));

	Marker2D _leftThruster = null!, _rightThruster = null!;
	Vector2 _cabinGravity;
	float _gravity;

	public override void _Ready()
	{
		Fuel = MaxFuel;
		_gravity = (float)ProjectSettings.GetSetting("physics/2d/default_gravity");
		_leftThruster = GetNode<Marker2D>("LeftThruster");
		_rightThruster = GetNode<Marker2D>("RightThruster");

		var viewport = GetNode<SubViewport>("InteriorViewport");
		Interior = viewport.GetNode<Interior>("Interior");
		GetNode<Sprite2D>("InteriorView").Texture = viewport.GetTexture();
		_cabinGravity = new Vector2(0, _gravity * CabinGravityScale);
		Interior.Gravity = _cabinGravity;

		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (!Wrecked)
		{
			FireThrusters(dt);
			ApplyCabinWeight();
		}
		UpdateCabinGravity(dt);
		PreviousVelocity = LinearVelocity;
		QueueRedraw();
	}

	// YOU 3 ─────────────────────────────────────────────────────────────────────────────────────────────────────────
	/// <summary>
	/// Push the ship with both thrusters and burn fuel.
	///
	/// - Each lever's throttle is <c>Interior.LeftLever.Throttle</c> and <c>Interior.RightLever.Throttle</c>, from 0 to 1.
	/// - A thruster pushes along the ship's "up", which turns as the ship turns. <c>-GlobalTransform.Y</c> is that direction.
	/// - <c>ApplyForce(force, offset)</c> pushes at a point. The offset is from the ship's centre, in world directions:
	///   <c>_leftThruster.GlobalPosition - GlobalPosition</c>. Pushing off-centre is what makes the ship turn.
	/// - Burn <c>BurnPerSecond * throttle * delta</c> from <c>Fuel</c> per thruster. With no fuel left, nothing fires.
	///
	/// Read: Unit 3 in docs/godot-syllabus.md, especially "Using RigidBody" and "Vector math".
	/// </summary>
	void FireThrusters(float delta)
	{
		// YOU: write this.
	}

	// YOU 4 ─────────────────────────────────────────────────────────────────────────────────────────────────────────
	/// <summary>
	/// Runs when the hull or legs hit something (the ground, a pad). Decide: landing or crash?
	///
	/// - How hard it hit: <c>PreviousVelocity.Length()</c> (see why on <see cref="PreviousVelocity"/>).
	/// - How tilted it was: <see cref="TiltDegrees"/>.
	/// - Too fast or too tilted: call <c>Wreck()</c>, then <c>EmitSignal(SignalName.Crashed, speed, tilt)</c>.
	/// - Otherwise: <c>EmitSignal(SignalName.TouchedDown, body)</c>. Main decides whether it was the right pad.
	/// - Once wrecked, ignore any further hits.
	///
	/// Read: Unit 4 in docs/godot-syllabus.md ("Using signals" and "C# signals").
	/// </summary>
	void OnBodyEntered(Node body)
	{
		// YOU: write this.
	}

	/// <summary>Breaks the ship: the engines die, and the cabin feels the hit.</summary>
	public void Wreck()
	{
		if (Wrecked) return;
		Wrecked = true;
		Interior.Shake(PreviousVelocity.Length());
	}

	public float ThrottleLeft => Interior.LeftLever.Throttle;
	public float ThrottleRight => Interior.RightLever.Throttle;
	public bool Firing => !Wrecked && Fuel > 0f;

	/// <summary>Everything loose in the cabin pulls down on the ship where it stands, so a crew member at one end
	/// makes that end heavier.</summary>
	void ApplyCabinWeight()
	{
		foreach (var (cabinPos, mass) in Interior.Loads())
		{
			Vector2 offset = ToGlobal(Interior.ToShip(cabinPos)) - GlobalPosition;
			ApplyForce(new Vector2(0, _gravity * mass), offset);
		}
	}

	/// <summary>
	/// What the crew feels as "down": real gravity minus the ship's own acceleration, turned into the ship's frame.
	/// Sitting on the ground it is plain gravity. Thrusting up presses you into the floor. Falling freely, you float.
	/// </summary>
	void UpdateCabinGravity(float dt)
	{
		Vector2 accel = (LinearVelocity - PreviousVelocity) / dt;
		Vector2 felt = (new Vector2(0, _gravity) - accel).Rotated(-Rotation) * CabinGravityScale;
		float max = _gravity * CabinGravityScale * CabinMaxG;
		if (felt.Length() > max) felt = felt.Normalized() * max;
		// A little smoothing, so one bumpy physics frame doesn't jolt the crew.
		_cabinGravity = _cabinGravity.Lerp(felt, 0.35f);
		Interior.Gravity = _cabinGravity;
	}

	// Placeholder art: the hull, legs, nozzles and flames. The cabin picture is the InteriorView sprite on top.
	public override void _Draw()
	{
		var hull = new Color(0.35f, 0.38f, 0.42f);
		var dark = new Color(0.16f, 0.17f, 0.2f);
		var legs = new Color(0.55f, 0.57f, 0.6f);
		if (Wrecked) { hull = hull.Darkened(0.5f); legs = legs.Darkened(0.5f); }

		// Legs and feet.
		foreach (float s in new[] { -1f, 1f })
		{
			DrawLine(new Vector2(s * 140, 60), new Vector2(s * 164, 108), legs, 6);
			DrawRect(new Rect2(s * 164 - 18, 106, 36, 6), legs);
		}
		// Hull: a frame around the cabin window.
		DrawRect(new Rect2(-168, -68, 336, 136), hull);
		DrawRect(new Rect2(-162, -62, 324, 124), dark);
		// Nozzles and flames.
		DrawThruster(_leftThruster.Position, ThrottleLeft);
		DrawThruster(_rightThruster.Position, ThrottleRight);
	}

	void DrawThruster(Vector2 at, float throttle)
	{
		DrawRect(new Rect2(at.X - 12, at.Y - 6, 24, 10), new Color(0.25f, 0.25f, 0.28f));
		if (!Firing || throttle <= 0.01f) return;
		float len = 18 + 70 * throttle * (0.85f + 0.15f * GD.Randf());
		DrawColoredPolygon([at + new Vector2(-10, 4), at + new Vector2(10, 4), at + new Vector2(0, 4 + len)],
			new Color(1f, 0.6f, 0.15f, 0.9f));
		DrawColoredPolygon([at + new Vector2(-5, 4), at + new Vector2(5, 4), at + new Vector2(0, 4 + len * 0.55f)],
			new Color(1f, 0.95f, 0.7f));
	}
}
