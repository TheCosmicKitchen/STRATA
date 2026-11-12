using Godot;

namespace Touchdown;

/// <summary>An arrow at the edge of the screen pointing toward the next pad when it is out of view.</summary>
public partial class TargetArrow : Control
{
	public Vector2? Target { get; set; }

	public override void _Process(double delta) => QueueRedraw();

	public override void _Draw()
	{
		if (Target is not Vector2 t) return;
		Rect2 view = GetRect().Grow(-40);
		if (view.HasPoint(t)) return;
		Vector2 c = view.GetCenter(), d = (t - c).Normalized();
		// Walk from the centre toward the target until we reach the edge of the inset rectangle.
		float sx = d.X != 0 ? (view.Size.X / 2) / Mathf.Abs(d.X) : float.MaxValue;
		float sy = d.Y != 0 ? (view.Size.Y / 2) / Mathf.Abs(d.Y) : float.MaxValue;
		Vector2 p = c + d * Mathf.Min(sx, sy);
		Vector2 side = new(-d.Y, d.X);
		DrawColoredPolygon([p + d * 16, p - d * 8 + side * 10, p - d * 8 - side * 10], new Color(1f, 0.8f, 0.2f, 0.9f));
	}
}
