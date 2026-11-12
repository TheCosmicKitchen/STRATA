using Godot;

namespace Touchdown;

/// <summary>A loose supply crate in the cabin. Nobody tied it down. It slides when the ship tilts, and its weight
/// goes with it.</summary>
public partial class Crate : RigidBody2D
{
	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		var wood = new Color(0.55f, 0.4f, 0.22f);
		DrawRect(new Rect2(-10, -10, 20, 20), wood);
		DrawRect(new Rect2(-10, -10, 20, 20), wood.Darkened(0.4f), false, 2);
		DrawLine(new Vector2(-10, -10), new Vector2(10, 10), wood.Darkened(0.4f), 2);
	}
}
