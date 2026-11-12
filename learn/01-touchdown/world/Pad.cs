using Godot;

namespace Touchdown;

/// <summary>A landing pad. Main tells it whether it is the next target, already done, or still ahead.</summary>
public partial class Pad : StaticBody2D
{
	public const float Width = 440f;

	public enum State { Ahead, Target, Done }

	public int Index { get; set; }
	public State Status { get; set; } = State.Ahead;

	double _time;

	public override void _Process(double delta)
	{
		_time += delta;
		QueueRedraw();
	}

	// Placeholder art: a deck with lights. The target pad's lights blink.
	public override void _Draw()
	{
		DrawRect(new Rect2(-Width / 2, 0, Width, 14), new Color(0.3f, 0.31f, 0.34f));
		DrawRect(new Rect2(-Width / 2, 0, Width, 3), new Color(0.6f, 0.62f, 0.66f));
		Color lamp = Status switch
		{
			State.Target => (int)(_time * 2.5) % 2 == 0 ? new Color(1f, 0.8f, 0.2f) : new Color(0.4f, 0.3f, 0.1f),
			State.Done => new Color(0.3f, 0.9f, 0.4f),
			_ => new Color(0.35f, 0.35f, 0.4f),
		};
		for (int i = 0; i < 6; i++) DrawCircle(new Vector2(-Width / 2 + 20 + i * (Width - 40) / 5, 8), 3.5f, lamp);
		DrawString(ThemeDB.FallbackFont, new Vector2(-8, 42), Index.ToString(), HorizontalAlignment.Left, -1, 26, lamp);
	}
}
