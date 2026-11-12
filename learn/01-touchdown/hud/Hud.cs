using Godot;

namespace Touchdown;

/// <summary>
/// The instruments. Lives on a CanvasLayer, so it stays fixed on screen while the camera moves around.
/// Main calls these methods; the HUD never reaches into the game itself ("call down, signal up").
/// </summary>
public partial class Hud : CanvasLayer
{
	Label _pad = null!, _speed = null!, _tilt = null!, _message = null!;
	ProgressBar _fuel = null!, _left = null!, _right = null!;
	TargetArrow _arrow = null!;

	public override void _Ready()
	{
		_pad = GetNode<Label>("%PadLabel");
		_speed = GetNode<Label>("%SpeedLabel");
		_tilt = GetNode<Label>("%TiltLabel");
		_message = GetNode<Label>("%Message");
		_fuel = GetNode<ProgressBar>("%FuelBar");
		_left = GetNode<ProgressBar>("%LeftBar");
		_right = GetNode<ProgressBar>("%RightBar");
		_arrow = GetNode<TargetArrow>("%Arrow");
	}

	public void ShowShip(Ship ship, float safeSpeed, float safeTilt)
	{
		_fuel.MaxValue = ship.MaxFuel;
		_fuel.Value = ship.Fuel;
		_left.Value = ship.ThrottleLeft * 100;
		_right.Value = ship.ThrottleRight * 100;
		Vector2 v = ship.LinearVelocity;
		float speed = v.Length(), tilt = ship.TiltDegrees;
		_speed.Text = $"SPEED  {speed,4:0}   (down {v.Y,4:0})";
		_speed.Modulate = speed <= safeSpeed ? Colors.White : new Color(1f, 0.45f, 0.35f);
		_tilt.Text = $"TILT   {tilt,4:0}°";
		_tilt.Modulate = tilt <= safeTilt ? Colors.White : new Color(1f, 0.45f, 0.35f);
	}

	public void ShowPad(int target, int count) =>
		_pad.Text = target < count ? $"PAD {target} OF {count - 1}" : "ALL PADS DONE";

	public void ShowMessage(string text) => _message.Text = text;

	/// <summary>Points at the target when it is off screen. Pass null to hide.</summary>
	public void PointAt(Vector2? screenPos) => _arrow.Target = screenPos;
}
