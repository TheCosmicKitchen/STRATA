using Godot;

namespace Touchdown;

/// <summary>
/// A throttle lever on the cabin wall. Stand at it and push it up or down. It stays where you leave it, so you can set
/// it and run to the other one.
/// </summary>
public partial class Lever : Area2D
{
	/// <summary>Emitted whenever the throttle moves. The HUD could listen to this (it polls for now).</summary>
	[Signal] public delegate void ThrottleChangedEventHandler(float throttle);

	/// <summary>Shown on the lever: "L" or "R".</summary>
	[Export] public string Label = "L";
	/// <summary>How fast the lever moves while held, in full throws per second.</summary>
	[Export] public float Rate = 0.6f;

	/// <summary>0 is off, 1 is full power.</summary>
	public float Throttle { get; set; }

	bool _crewInReach;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	// YOU 2 ─────────────────────────────────────────────────────────────────────────────────────────────────────────
	/// <summary>
	/// Keep track of whether the crew is standing at this lever.
	///
	/// - This Area2D's collision mask is set to the "crew" layer, so only the crew can trigger it.
	/// - Still check <c>body is Crew</c>: it's a good habit, and it's how you'd tell crew members apart later.
	/// - Set <c>_crewInReach</c> here, and clear it in <see cref="OnBodyExited"/>.
	///
	/// Read: Unit 4 ("Using signals") and "Using Area2D" from Unit 3.
	/// </summary>
	void OnBodyEntered(Node2D body)
	{
		// YOU: write this.
	}

	void OnBodyExited(Node2D body)
	{
		// YOU: write this.
	}

	/// <summary>
	/// While the crew is in reach, the "lever_up" and "lever_down" actions move the throttle at <see cref="Rate"/>.
	/// Keep it between 0 and 1 (<c>Mathf.Clamp</c>), and emit ThrottleChanged when it moves.
	/// </summary>
	public override void _PhysicsProcess(double delta)
	{
		// YOU: write this.

		QueueRedraw();
	}

	// Placeholder art: a slot with a handle. The handle rises with the throttle, and glows when you can reach it.
	public override void _Draw()
	{
		var slot = new Rect2(-3, -30, 6, 34);
		DrawRect(slot, new Color(0.05f, 0.05f, 0.06f));
		DrawRect(new Rect2(-3, 4 - 34 * Throttle, 6, 34 * Throttle), new Color(1f, 0.55f, 0.15f, 0.7f));
		float y = 4 - 34 * Throttle;
		var knob = _crewInReach ? new Color(1f, 0.85f, 0.3f) : new Color(0.75f, 0.75f, 0.78f);
		DrawRect(new Rect2(-7, y - 3, 14, 6), knob);
		DrawString(ThemeDB.FallbackFont, new Vector2(-4, -36), Label, HorizontalAlignment.Left, -1, 10,
			new Color(0.8f, 0.8f, 0.85f));
	}
}
