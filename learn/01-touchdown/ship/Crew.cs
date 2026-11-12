using Godot;

namespace Touchdown;

/// <summary>
/// You: a small person in the cabin. Walks, jumps over the engine block, and works the levers.
/// </summary>
public partial class Crew : CharacterBody2D
{
	[Export] public float Speed = 140f;
	[Export] public float Acceleration = 900f;
	[Export] public float JumpSpeed = 330f;
	/// <summary>Your weight, compared with the ship's 10. It pulls down on whichever end of the ship you stand at.</summary>
	[Export] public float Mass = 1.5f;

	Interior _interior = null!;

	public override void _Ready()
	{
		_interior = GetParent<Interior>();
	}

	// YOU 1 ─────────────────────────────────────────────────────────────────────────────────────────────────────────
	/// <summary>
	/// Walk and jump. The twist: "down" in the cabin is <c>_interior.Gravity</c>, and it changes direction and strength
	/// as the ship tilts, thrusts and falls. In free fall it is nearly zero, and you float.
	///
	/// A good order:
	/// 1. Start with gravity straight down: add <c>_interior.Gravity * delta</c> to <c>Velocity</c>, then call <c>MoveAndSlide()</c>.
	///    Run the game and watch yourself land on the floor.
	/// 2. Walk: <c>Input.GetAxis("move_left", "move_right")</c> gives -1 to 1. Move the sideways speed toward
	///    <c>input * Speed</c> with <c>Mathf.MoveToward</c> and <c>Acceleration</c>.
	/// 3. Jump: if <c>IsOnFloor()</c> and "jump" was just pressed, set the upward speed to <c>JumpSpeed</c>.
	/// 4. Now make "down" follow gravity. Set <c>UpDirection = -_interior.Gravity.Normalized()</c> so <c>IsOnFloor()</c>
	///    still works when tilted. Split <c>Velocity</c> into a sideways part and an up part using the dot product
	///    with <c>UpDirection</c> and with "right" (<c>new Vector2(-up.Y, up.X)</c>), change each part, and put them
	///    back together.
	///
	/// Read: Unit 2 ("Listening to player input") and Unit 3 ("Using CharacterBody2D", "Vector math").
	/// </summary>
	public override void _PhysicsProcess(double delta)
	{
		// YOU: write this.
	}

	// Placeholder art: head, body, legs. Leans the way "up" is.
	public override void _Draw()
	{
		Vector2 up = _interior.Gravity.LengthSquared() > 1 ? -_interior.Gravity.Normalized() : Vector2.Up;
		float lean = Mathf.Clamp(up.Angle() + Mathf.Pi / 2, -0.6f, 0.6f);
		DrawSetTransform(Vector2.Zero, lean);
		var suit = new Color(0.95f, 0.6f, 0.2f);
		DrawRect(new Rect2(-5, -6, 10, 12), suit);
		DrawRect(new Rect2(-5, 6, 4, 7), suit.Darkened(0.3f));
		DrawRect(new Rect2(1, 6, 4, 7), suit.Darkened(0.3f));
		DrawCircle(new Vector2(0, -10), 5, new Color(0.85f, 0.88f, 0.92f));
		DrawRect(new Rect2(0, -12, 4, 3), new Color(0.2f, 0.5f, 0.7f));
		DrawSetTransform(Vector2.Zero);
	}

	public override void _Process(double delta) => QueueRedraw();
}
