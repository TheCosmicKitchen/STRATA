using System.Collections.Generic;
using Godot;

namespace Touchdown;

/// <summary>
/// The cabin, seen in cross-section. It sits inside a SubViewport, which gives it its own little physics world that
/// never moves or turns. Instead, the ship turns this world's gravity, so tilting the ship tilts "down" in here.
///
/// Room coordinates run from (0, 0) at the top left to <see cref="Size"/> at the bottom right.
/// </summary>
public partial class Interior : Node2D
{
	public static readonly Vector2 Size = new(320, 120);

	public Lever LeftLever { get; private set; } = null!;
	public Lever RightLever { get; private set; } = null!;
	public Crew Crew { get; private set; } = null!;

	Vector2 _gravity = new(0, 900);
	float _shake;

	/// <summary>Which way and how hard "down" pulls in the cabin right now. The ship sets this every physics frame.
	/// Loose objects (the crate) follow it automatically; the crew reads it in its own code.</summary>
	public Vector2 Gravity
	{
		get => _gravity;
		set
		{
			_gravity = value;
			if (!IsInsideTree()) return;
			Rid space = GetWorld2D().Space;
			PhysicsServer2D.AreaSetParam(space, PhysicsServer2D.AreaParameter.Gravity, value.Length());
			if (value.LengthSquared() > 0.0001f)
				PhysicsServer2D.AreaSetParam(space, PhysicsServer2D.AreaParameter.GravityVector, value.Normalized());
		}
	}

	public override void _Ready()
	{
		LeftLever = GetNode<Lever>("LeftLever");
		RightLever = GetNode<Lever>("RightLever");
		Crew = GetNode<Crew>("Crew");
		Gravity = _gravity;
	}

	/// <summary>Converts a point in room coordinates to the ship's local coordinates (the ship's centre is the room's centre).</summary>
	public static Vector2 ToShip(Vector2 roomPos) => roomPos - Size / 2;

	/// <summary>Everything in the cabin that has weight: where it is, and its mass.</summary>
	public IEnumerable<(Vector2 pos, float mass)> Loads()
	{
		foreach (Node child in GetChildren())
		{
			if (child is Crew crew) yield return (crew.Position, crew.Mass);
			else if (child is RigidBody2D body) yield return (body.Position, body.Mass);
		}
	}

	/// <summary>A hard knock: the room picture shakes and everything loose gets thrown.</summary>
	public void Shake(float strength)
	{
		_shake = Mathf.Clamp(strength / 40f, 2f, 10f);
		foreach (Node child in GetChildren())
			if (child is RigidBody2D body) body.ApplyImpulse(new Vector2(GD.Randf() - 0.5f, -1f) * strength * 2f);
	}

	public override void _Process(double delta)
	{
		_shake = Mathf.MoveToward(_shake, 0, (float)delta * 20f);
		Position = _shake > 0 ? new Vector2(GD.Randf() - 0.5f, GD.Randf() - 0.5f) * _shake : Vector2.Zero;
		QueueRedraw();
	}

	// Placeholder art: the room. Walls match the collision shapes on the Walls node.
	public override void _Draw()
	{
		var bg = new Color(0.11f, 0.12f, 0.15f);
		var wall = new Color(0.42f, 0.44f, 0.48f);
		var stripe = new Color(0.2f, 0.21f, 0.25f);
		DrawRect(new Rect2(Vector2.Zero, Size), bg);
		for (int x = 12; x < Size.X; x += 24) DrawLine(new Vector2(x, 8), new Vector2(x, 112), stripe, 1);
		DrawRect(new Rect2(0, 112, 320, 8), wall);       // floor
		DrawRect(new Rect2(0, 0, 320, 8), wall);         // ceiling
		DrawRect(new Rect2(0, 0, 8, 120), wall);         // left wall
		DrawRect(new Rect2(312, 0, 8, 120), wall);       // right wall
		DrawRect(new Rect2(140, 82, 40, 30), new Color(0.3f, 0.32f, 0.36f)); // engine block
		DrawRect(new Rect2(144, 86, 32, 4), new Color(0.9f, 0.5f, 0.2f, 0.6f));
		// A small arrow showing which way "down" feels right now, by the ceiling.
		Vector2 c = new(160, 24);
		Vector2 d = _gravity.LengthSquared() > 1 ? _gravity.Normalized() : Vector2.Zero;
		float g = _gravity.Length() / 900f;
		DrawCircle(c, 9, new Color(0, 0, 0, 0.35f));
		DrawLine(c, c + d * 8 * Mathf.Min(g, 1.5f), new Color(0.6f, 0.9f, 1f), 2);
	}
}
