using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace Touchdown;

/// <summary>
/// Test tools for checking the game without playing it by hand. They do nothing unless the game is started with
/// arguments after <c>--</c>:
///
///   godot --path learn/01-touchdown -- --drive=demo --shots=C:/tmp/td --quit=10
///
/// --drive=NAME  presses actions on a timeline (see <see cref="Scripts"/>), as if someone were playing.
/// --shots=DIR   saves a screenshot every second into DIR.
/// --quit=SEC    quits after SEC seconds.
/// Every half second it prints the ship's state, so a run can be read afterwards.
/// </summary>
public partial class DevTools : Node
{
	/// <summary>Timelines: (time in seconds, action, press or release). Besides input actions, "L=0.5" and "R=0.5"
	/// set a lever directly, and "tp=N" puts the ship 300 pixels above pad N, at rest.</summary>
	static readonly Dictionary<string, (float t, string action, bool down)[]> Scripts = new()
	{
		["demo"] =
		[
			(0.5f, "lever_up", true), (1.5f, "lever_up", false),
			(1.6f, "move_right", true), (2.1f, "jump", true), (2.2f, "jump", false), (3.8f, "move_right", false),
			(3.9f, "lever_up", true), (4.9f, "lever_up", false),
		],
		// Hover, then sink back onto pad 0.
		["hover"] = [(0.5f, "L=0.62", true), (0.5f, "R=0.62", true), (3f, "L=0.54", true), (3f, "R=0.54", true)],
		// Drop onto pad 1 gently: should count as a landing.
		["land"] = [(0.2f, "tp=1", true), (0.2f, "L=0.59", true), (0.2f, "R=0.525", true)],
		// Spin the ship into the ground: should crash.
		["crash"] = [(0.5f, "L=1", true), (0.5f, "R=0.45", true), (2.5f, "L=0", true), (2.5f, "R=0", true)],
	};

	readonly Dictionary<string, string> _args = new();
	(float t, string action, bool down)[] _script = [];
	int _next;
	float _time, _nextShot = 1f, _nextLog;

	public static bool Wanted()
	{
		foreach (string a in OS.GetCmdlineUserArgs())
			if (a.StartsWith("--drive") || a.StartsWith("--shots") || a.StartsWith("--quit")) return true;
		return false;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		foreach (string a in OS.GetCmdlineUserArgs())
		{
			string[] kv = a.TrimStart('-').Split('=', 2);
			_args[kv[0]] = kv.Length > 1 ? kv[1] : "";
		}
		if (_args.TryGetValue("drive", out string? name) && Scripts.TryGetValue(name, out var s)) _script = s;
		if (_args.TryGetValue("shots", out string? dir)) DirAccess.MakeDirRecursiveAbsolute(dir);
	}

	public override void _PhysicsProcess(double delta)
	{
		_time += (float)delta;
		while (_next < _script.Length && _script[_next].t <= _time)
		{
			var (_, action, down) = _script[_next++];
			var ship = GetParent().GetNode<Ship>("Ship");
			if (action.StartsWith("L=")) ship.Interior.LeftLever.Throttle = float.Parse(action[2..], CultureInfo.InvariantCulture);
			else if (action.StartsWith("R=")) ship.Interior.RightLever.Throttle = float.Parse(action[2..], CultureInfo.InvariantCulture);
			else if (action.StartsWith("tp="))
			{
				var pad = GetParent().GetNode<Terrain>("Terrain").Pads[int.Parse(action[3..])];
				ship.GlobalPosition = pad.GlobalPosition + new Vector2(0, -412);
				ship.LinearVelocity = Vector2.Zero;
				ship.Rotation = 0;
			}
			else if (down) Input.ActionPress(action); else Input.ActionRelease(action);
		}

		if (_time >= _nextLog)
		{
			_nextLog += 0.5f;
			var ship = GetParent().GetNode<Ship>("Ship");
			var crew = ship.Interior.Crew;
			GD.Print(string.Format(CultureInfo.InvariantCulture,
				"t={0:0.0} ship=({1:0},{2:0}) v=({3:0},{4:0}) tilt={5:0} fuel={6:0.0} L={7:0.00} R={8:0.00} crew=({9:0},{10:0}) g=({11:0},{12:0}) wrecked={13}",
				_time, ship.GlobalPosition.X, ship.GlobalPosition.Y, ship.LinearVelocity.X, ship.LinearVelocity.Y,
				ship.TiltDegrees, ship.Fuel, ship.ThrottleLeft, ship.ThrottleRight, crew.Position.X, crew.Position.Y,
				ship.Interior.Gravity.X, ship.Interior.Gravity.Y, ship.Wrecked));
		}
	}

	public override void _Process(double delta)
	{
		if (_args.TryGetValue("shots", out string? dir) && _time >= _nextShot)
		{
			GetViewport().GetTexture().GetImage().SavePng($"{dir}/t{_nextShot:00}.png");
			_nextShot += 1f;
		}
		if (_args.TryGetValue("quit", out string? q) && float.TryParse(q, CultureInfo.InvariantCulture, out float sec) && _time >= sec)
			GetTree().Quit();
	}
}
