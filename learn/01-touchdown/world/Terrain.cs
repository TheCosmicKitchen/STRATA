using System.Collections.Generic;
using Godot;

namespace Touchdown;

/// <summary>
/// Builds the moon's surface when the scene starts: a jagged ridge line with flat shelves where the pads sit, each
/// pad further away and each ridge between them taller. The ground is one StaticBody2D whose collision is the outline
/// of the surface; the pads are instances of Pad.tscn placed on the shelves.
/// </summary>
public partial class Terrain : Node2D
{
	[Export] public PackedScene PadScene = null!;
	[Export] public int PadCount = 5;
	[Export] public float PadSpacing = 2600f;
	[Export] public ulong Seed = 7;

	public List<Pad> Pads { get; } = new();

	/// <summary>Surface points from left to right. Lower y is higher up: Godot's y axis points down.</summary>
	readonly List<Vector2> _surface = new();

	public override void _Ready()
	{
		var rng = new RandomNumberGenerator { Seed = Seed };
		float step = 70f, half = Pad.Width / 2 + 30f;

		// Pad heights: the first sits at 0, later ones wander up and down.
		var padY = new float[PadCount];
		for (int i = 1; i < PadCount; i++) padY[i] = padY[i - 1] + rng.RandfRange(-500f, 350f);

		float last = (PadCount - 1) * PadSpacing;
		float left = -1400f, right = last + 1400f;
		_surface.Add(new Vector2(left, -2600f)); // cliff at the west edge
		for (float x = left; x <= right; x += step)
		{
			int near = Mathf.Clamp(Mathf.RoundToInt(x / PadSpacing), 0, PadCount - 1);
			float padX = near * PadSpacing;
			float y;
			if (Mathf.Abs(x - padX) < half) y = padY[near]; // flat shelf for a pad
			else
			{
				// Between pads: blend the two pad heights, then raise a ridge in the middle that grows with each leg.
				int a = Mathf.Clamp(Mathf.FloorToInt(x / PadSpacing), 0, PadCount - 1);
				int b = Mathf.Min(a + 1, PadCount - 1);
				float t = Mathf.Clamp((x - a * PadSpacing) / PadSpacing, 0f, 1f);
				float baseY = Mathf.Lerp(padY[a], padY[b], t);
				float ridge = x < 0 || x > last ? 300f : Mathf.Sin(t * Mathf.Pi) * (250f + 180f * a);
				y = baseY - ridge + rng.RandfRange(-60f, 60f);
				// Ease into the shelves so their edges aren't sheer walls.
				float edge = Mathf.Abs(x - padX) - half;
				if (edge < 160f) y = Mathf.Lerp(padY[near], y, edge / 160f);
			}
			_surface.Add(new Vector2(x, y + 14f)); // +14: the pad deck sits on top of the ground
		}
		_surface.Add(new Vector2(right, -2600f)); // cliff at the east edge

		BuildGround();

		for (int i = 0; i < PadCount; i++)
		{
			var pad = PadScene.Instantiate<Pad>();
			pad.Index = i;
			pad.Position = new Vector2(i * PadSpacing, padY[i]);
			AddChild(pad);
			Pads.Add(pad);
		}
	}

	void BuildGround()
	{
		var outline = new List<Vector2>(_surface);
		outline.Add(new Vector2(_surface[^1].X, 3000f));
		outline.Add(new Vector2(_surface[0].X, 3000f));
		var points = outline.ToArray();

		var body = new StaticBody2D { Name = "Ground", PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.9f } };
		// Segments: collide with the outline only. Fine for ground that never moves, and much cheaper than solids.
		body.AddChild(new CollisionPolygon2D { Polygon = points, BuildMode = CollisionPolygon2D.BuildModeEnum.Segments });
		AddChild(body);

		AddChild(new Polygon2D { Polygon = points, Color = new Color(0.22f, 0.2f, 0.21f) });
		AddChild(new Line2D { Points = _surface.ToArray(), Width = 4, DefaultColor = new Color(0.45f, 0.42f, 0.4f) });
	}

	/// <summary>Height of the ground under x, for the camera.</summary>
	public float GroundY(float x)
	{
		for (int i = 1; i < _surface.Count; i++)
			if (_surface[i].X >= x && _surface[i - 1].X <= x)
			{
				var (a, b) = (_surface[i - 1], _surface[i]);
				return b.X == a.X ? a.Y : Mathf.Lerp(a.Y, b.Y, (x - a.X) / (b.X - a.X));
			}
		return 0;
	}
}
