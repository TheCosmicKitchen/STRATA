using System.Linq;
using System;
using System.IO;
using Godot;
using Strata.Rules;
using Strata.Sim;

namespace Strata.Game;

public partial class Game
{
	/// <summary>
	/// Plays the demo end to end through the same controls a player uses: walk to the truck, drive to the dig site,
	/// park the winch over the shaft, clip on, go down, take the sample, reel up, drive home and deliver it.
	/// Logs every step with its time and saves a screenshot at each one, so a run can be reviewed afterwards.
	/// Start with: godot --path game -- --play=&lt;folder&gt;
	/// </summary>
	sealed class Autopilot
	{
		enum Step { WalkToTruck, DriveToSite, Park, ClipOn, Descend, ToSample, ReelUp, StepOff, WalkBack, DriveHome, Deliver, Done }

		readonly string _dir;
		readonly StreamWriter _log;
		Step _step = Step.WalkToTruck;
		float _t, _stepT, _sincePress = 99;
		int _shot;
		Vector2 _lastPos; float _stillFor;
		public const float StepTimeout = 120;

		public Autopilot(string dir)
		{
			_dir = dir;
			Directory.CreateDirectory(dir);
			_log = new StreamWriter(Path.Combine(dir, "play.log")) { AutoFlush = true };
		}

		void Log(Game g, string s)
		{
			string line = $"[{_t,7:F1}s] {_step,-11} {s}  (player {g._p.X:F0},{g._p.Y:F0} · truck {g._truck.X:F0} · rope {g._p.RopeLen:F0} · air {g._p.Air:F0} · legs {g._p.Body[Part.LeftLeg]}/{g._p.Body[Part.RightLeg]})";
			GD.Print(line); _log.WriteLine(line);
		}

		void Shot(Game g, string name) => g.SaveShot(Path.Combine(_dir, $"{++_shot:00}-{name}.png"));

		void Go(Game g, Step next, string why)
		{
			Log(g, $"→ {next}: {why}");
			_step = next; _stepT = 0;
			g.CallDeferred(nameof(SaveShotDeferred), Path.Combine(_dir, $"{++_shot:00}-{next}.png"));
		}

		bool Press()
		{
			if (_sincePress < .35f) return false;
			_sincePress = 0; return true;
		}

		public Controls Think(Game g, float dt)
		{
			_t += dt; _stepT += dt; _sincePress += dt;
			var c = new Controls();
			var p = g._p; var tr = g._truck; var site = g._site;
			float head = p.Y - Player.Height + 2;
			c.Aim = new Vector2(p.X + (p.Facing ? 8 : -8), head + 6);

			// Notice getting stuck: not moving for a long time, or a step taking too long.
			var pos = new Vector2(g._inTruck ? tr.X : p.X, p.Y);
			_stillFor = pos.DistanceTo(_lastPos) < .05f ? _stillFor + dt : 0; _lastPos = pos;
			if (_step != Step.Done && (_stepT > StepTimeout || _stillFor > 20))
			{
				Log(g, $"STUCK: {(_stepT > StepTimeout ? "step took too long" : "not moving for 20 s")}. Message: \"{p.Message}\"");
				Shot(g, "stuck");
				_step = Step.Done; g.GetTree().CreateTimer(.5f, true, false, true).Timeout += () => g.GetTree().Quit();
				return c;
			}

			switch (_step)
			{
				case Step.WalkToTruck:
				case Step.WalkBack:
				{
					float dx = tr.X - p.X;
					if (MathF.Abs(dx) > Truck.HalfLength) c.MoveX = MathF.Sign(dx);
					else if (Press()) c.Use = true;
					if (g._inTruck) Go(g, _step == Step.WalkToTruck ? Step.DriveToSite : Step.DriveHome, "in the truck");
					break;
				}
				case Step.DriveToSite:
				{
					// Drive east; stop with the boom (at the back) just inside the east side of the shaft.
					float target = site.WinchX + 2 + Truck.HalfLength + Truck.BoomReach;
					float d = target - tr.X, brake = tr.Speed * tr.Speed / (2 * tr.Brake) + 4;
					if (d > brake) c.MoveX = 1;
					if (d < 30 && MathF.Abs(tr.Speed) < 3) Go(g, Step.Park, $"arrived, {d:F1} cells short of the parking spot");
					break;
				}
				case Step.Park:
				{
					float off = p.AnchorX - (site.WinchX + 2);
					if (off > .6f) c.MoveY = 1;         // boom too far east: creep backward (west)
					else if (off < -.6f) c.MoveY = -1;  // too far west: creep forward
					if (MathF.Abs(off) <= .6f && MathF.Abs(tr.Speed) < .5f && Press()) c.Use = true;
					if (!g._inTruck) Go(g, Step.ClipOn, $"parked, boom {off:F1} cells from the planned spot");
					break;
				}
				case Step.ClipOn:
				{
					if (p.DistanceToLine() < p.ClipReach - 2) { if (Press()) c.Clip = true; }
					else c.MoveX = MathF.Sign(p.AnchorX - p.X);
					if (p.Clipped) Go(g, Step.Descend, "clipped on");
					break;
				}
				case Step.Descend:
				{
					c.MoveY = 1;                                     // pay out
					if (p.Grounded && p.Y < site.SampleY - 25) c.MoveX = MathF.Sign(p.AnchorX - p.X);   // step off the edge into the shaft
					c.Aim = new Vector2(p.X, head + 12);
					if (p.Grounded && p.Y > site.SampleY - 25) Go(g, Step.ToSample, "reached the chamber floor");
					break;
				}
				case Step.ToSample:
				{
					float dx = site.SampleX - p.X;
					c.MoveX = MathF.Abs(dx) > 3 ? MathF.Sign(dx) : 0;
					c.MoveY = 1;                                     // keep paying out so the line doesn't hold us back
					if (p.Grounded && _stepT > 1.5f && MathF.Abs(p.VX) < 1 && MathF.Abs(dx) > 3) { c.Jump = true; c.JumpHeld = true; }
					c.Aim = new Vector2(site.SampleX, site.SampleY - 2);
					if (MathF.Abs(dx) <= 4 && Press()) c.Use = true;
					if (p.CarryingSample) Go(g, Step.ReelUp, "sample taken");
					break;
				}
				case Step.ReelUp:
				{
					c.MoveY = -1;                                    // reel in
					c.MoveX = MathF.Abs(p.AnchorX - p.X) > 2 ? MathF.Sign(p.AnchorX - p.X) : 0;
					c.Aim = new Vector2(p.X, head - 12);
					if (p.Y < CaveGen.Surface + 1) Go(g, Step.StepOff, "back at the top");
					break;
				}
				case Step.StepOff:
				{
					c.MoveX = 1;                                     // swing towards the truck side
					bool overGround = Player.BlocksBody(g._world.Get((int)p.X, (int)p.Y + 1)) || Player.BlocksBody(g._world.Get((int)p.X, (int)p.Y + 3));
					if ((p.Grounded || overGround) && p.X > site.WinchX + CaveGen.ShaftHalfWidth + 1 && Press()) c.Clip = true;
					if (!p.Clipped) Go(g, Step.WalkBack, "unclipped on the edge");
					break;
				}
				case Step.DriveHome:
				{
					float target = site.BaseX + 75;
					float d = tr.X - target, brake = tr.Speed * tr.Speed / (2 * tr.Brake) + 4;
					if (d > brake) c.MoveX = -1;
					if (d < 25 && MathF.Abs(tr.Speed) < 1 && Press()) c.Use = true;
					if (!g._inTruck) Go(g, Step.Deliver, "home");
					break;
				}
				case Step.Deliver:
				{
					c.MoveX = MathF.Sign(site.BaseX - p.X);
					if (g._won)
					{
						Go(g, Step.Done, "sample delivered");
						Log(g, $"DONE in {_t:F0} s of game time");
						g.GetTree().CreateTimer(.5f, true, false, true).Timeout += () => g.GetTree().Quit();
					}
					break;
				}
			}
			return c;
		}
	}

	void SaveShotDeferred(string path) => SaveShot(path);
}

public partial class Game
{
	/// <summary>
	/// A scripted climb for checking the feel: builds a rock wall near the base, jumps at it, climbs and mantles,
	/// saving screenshots on the way. Start with: godot --path game -- --demo-climb=&lt;folder&gt;
	/// </summary>
	sealed class ClimbDemo
	{
		readonly string _dir; float _t; int _shot; bool _built;
		public ClimbDemo(string dir) { _dir = dir; System.IO.Directory.CreateDirectory(dir); }

		public Controls Think(Game g, float dt)
		{
			var c = new Controls();
			var p = g._p;
			int wx = g._site.BaseX + 46;
			if (!_built)
			{
				_built = true;
				for (int x = wx; x < wx + 14; x++) for (int y = g.BaseY - 12; y < g.BaseY; y++) g._world.Set(x, y, Mat.Rock);
				p.X = wx - 8; p.Y = g.BaseY;
			}
			_t += dt;
			c.Aim = new Godot.Vector2(wx, g.BaseY - 10);
			if (_t < .05f) { c.Jump = true; c.JumpHeld = true; c.MoveX = 1; }
			else if (_t < 6) { c.MoveX = 1; c.MoveY = -1; c.JumpHeld = true; }
			foreach (var at in new[] { .6f, 1.4f, 2.6f, 4f })
				if (_t - dt < at && _t >= at) g.CallDeferred(nameof(SaveShotDeferred), System.IO.Path.Combine(_dir, $"{++_shot:00}-climb-{at:F1}s.png"));
			if (_t >= at4 && _t - dt < at4) GD.Print($"climb demo: clinging={p.Clinging} stamina={p.Stamina:F0} pos={p.X:F0},{p.Y:F0}");
			if (_t > 4.3f) g.GetTree().Quit();
			return c;
		}
		const float at4 = 4f;
	}
}

public partial class Game
{
	/// <summary>
	/// A scripted spray test in the real cave: lava, then water onto it, then cold fire, with screenshots.
	/// Start with: godot --path game -- --demo-spray=&lt;folder&gt;
	/// </summary>
	sealed class SprayDemo
	{
		readonly string _dir; float _t; int _shot;
		public SprayDemo(string dir) { _dir = dir; System.IO.Directory.CreateDirectory(dir); }

		public Controls Think(Game g, float dt)
		{
			var c = new Controls();
			var p = g._p;
			_t += dt;
			// Step back first, then spray in an arc well away from your own feet.
			if (_t < .8f) { c.MoveX = -1; return c; }
			c.Aim = new Godot.Vector2(p.X + 60, g.BaseY - 16);
			byte want = _t < 3f ? Mat.Lava : _t < 5.5f ? Mat.Water : Mat.Coldflame;
			g._sprayIndex = Array.IndexOf(SprayList, want);
			g._slot = Tool.Sprayer;
			c.Spray = _t < 7;
			foreach (var at in new[] { 2.9f, 5.4f, 7.5f })
				if (_t - dt < at && _t >= at) g.CallDeferred(nameof(SaveShotDeferred), System.IO.Path.Combine(_dir, $"{++_shot:00}-spray-{at:F1}s.png"));
			if (_t > 8) g.GetTree().Quit();
			return c;
		}
	}
}

public partial class Game
{
	/// <summary>Walk right, jump, walk left, jump, logging position. Start with: godot --path game -- --demo-walk</summary>
	sealed class WalkDemo
	{
		float _t; public float Out;
		public Controls Think(Game g, float dt)
		{
			var c = new Controls();
			var p = g._p;
			_t += dt;
			if (Out > 0) { c.MoveX = 1; if ((int)(_t * 4) % 3 == 0) { c.Jump = true; c.JumpHeld = true; } if ((int)_t != (int)(_t - dt)) GD.Print($"out t={_t:F0} x={p.X:F0} y={p.Y:F0} base={g._site.BaseX}"); if (_t > Out) g.GetTree().Quit(); return c; }
			if (_t < 2) c.MoveX = 1; else if (_t < 4) c.MoveX = -1;
			if (_t is > .5f and < .55f or > 1.5f and < 1.55f or > 3f and < 3.05f) { c.Jump = true; c.JumpHeld = true; }
			else if (_t is > .5f and < .9f or > 1.5f and < 1.9f or > 3f and < 3.4f) c.JumpHeld = true;
			if ((int)(_t * 10) != (int)((_t - dt) * 10))
				GD.Print($"walk t={_t:F1} x={p.X:F1} y={p.Y:F1} vx={p.VX:F1} vy={p.VY:F1} grounded={p.Grounded} cling={p.Clinging} downed={p.Body.Downed} feet={MaterialTable.Name[g._world.Get((int)p.X, (int)p.Y)]} head={MaterialTable.Name[g._world.Get((int)p.X, (int)p.Y - Player.Height)]}");
			if (_t > 4.2f) g.GetTree().Quit();
			return c;
		}
	}
}

public partial class Game
{
	/// <summary>--demo-grapple: fire the hook at the base roof, climb the line, swing, then let go.</summary>
	sealed class GrappleDemo
	{
		float _t;
		public Controls Think(Game g, float dt)
		{
			var c = new Controls();
			var p = g._p;
			_t += dt;
			c.Aim = new Godot.Vector2(p.X + 10, g.BaseY - 40);
			if (_t is > .3f and < .32f) c.Grapple = true;
			if (_t is > .6f and < 2.2f) c.MoveY = -1;                  // climb the line
			if (_t is > 2.2f and < 3.2f) c.MoveX = (int)(_t * 2) % 2 == 0 ? 1 : -1;   // swing
			if (_t is > 3.3f and < 3.32f) { c.Jump = true; c.JumpHeld = true; }
			if ((int)(_t * 5) != (int)((_t - dt) * 5))
				GD.Print($"grapple t={_t:F1} x={p.X:F1} y={p.Y:F1} clipped={p.Clipped} grappled={g._grappled} flying={g._hookFlying} rope={p.RopeLen:F1} msg={p.Message}");
			if (_t > 4.5f) g.GetTree().Quit();
			return c;
		}
	}
}

public partial class Game
{
	/// <summary>--demo-bubble: bubble up, set off a blast right beside you, then run off the east edge of the base.</summary>
	sealed class BubbleDemo
	{
		float _t; bool _blasted, _pressed;
		public Controls Think(Game g, float dt)
		{
			var c = new Controls();
			var p = g._p;
			_t += dt;
			if (_t > .3f && !_pressed) { _pressed = true; c.Bubble = true; }
			if (_t > .6f && !_blasted) { _blasted = true; for (int k = 0; k < 3; k++) g._world.Set((int)p.X - 4, (int)p.Y - 2 - k, Mat.Blast); }
			if (_t > 2.5f) c.MoveX = 1;
			if ((int)(_t * 5) != (int)((_t - dt) * 5))
				GD.Print($"bubble t={_t:F1} x={p.X:F1} y={p.Y:F1} vx={p.VX:F0} vy={p.VY:F0} on={g._bubbleOn} batt={g._bubbleBattery:F1} hurt={string.Join("", p.Body.Parts.Select(h => (int)h))} downed={p.Body.Downed} msg={p.Message}");
			if (_t > 9) g.GetTree().Quit();
			return c;
		}
	}
}

public partial class Game
{
	/// <summary>--demo-tools: hoover the specimen tank, blow it out, shovel sand, then hoover fuel and flame.</summary>
	sealed class ToolsDemo
	{
		float _t; int _stage = -1;
		public Controls Think(Game g, float dt)
		{
			var c = new Controls();
			var p = g._p;
			_t += dt;
			int bx = g._site.BaseX, gy = g.BaseY;
			if (_stage < 0) { _stage = 0; p.X = bx - 80; p.Y = gy; p.Facing = false; }
			if (_t < 2) { g._slot = Tool.Vacuum; c.Aim = new Godot.Vector2(bx - 86, gy - 3); c.Dig = true; }               // hoover the slime tank
			else if (_t < 3.2f) { c.Aim = new Godot.Vector2(bx - 60, gy - 14); c.Spray = true; }                           // blow it out
			else if (_t < 3.3f) { for (int x = 0; x < 6; x++) for (int y = 0; y < 3; y++) g._world.Set(bx - 74 + x, gy - 1 - y, Mat.Sand); }
			else if (_t < 4.3f) { g._slot = Tool.Shovel; c.Aim = new Godot.Vector2(bx - 72, gy - 2); c.Dig = true; }    // scoop
			else if (_t < 4.6f) { c.Aim = new Godot.Vector2(bx - 50, gy - 12); c.Spray = true; }                           // throw
			else if (_t < 4.7f) { for (int x = 0; x < 5; x++) for (int y = 0; y < 4; y++) g._world.Set(bx - 76 + x, gy - 3 - y, Mat.Methane); }
			else if (_t is > 5.6f and < 5.7f) g._world.Set(bx - 74, gy - 4, Mat.Fire, 60);
			else if (_t < 6.5f) { g._slot = Tool.Vacuum; c.Aim = new Godot.Vector2(bx - 74, gy - 4); c.Dig = true; }
			if ((int)(_t * 4) != (int)((_t - dt) * 4))
				GD.Print($"tools t={_t:F2} slot={g._slot} tank={g._hasJar} n={g._jarTotal} [{g.JarContents()}] temp={g._jarTemp:F0} cracks={g._jarCracks} shovel={g._shovel.Count} hurt={string.Join("", p.Body.Parts.Select(h => (int)h))} msg={p.Message}");
			if (_t > 7.5f) g.GetTree().Quit();
			return c;
		}
	}
}

public partial class Game
{
	/// <summary>--demo-tips: plain pick vs crystal tip on granite, lava tip quenched in water, cryo tip freezing water, void tip.</summary>
	sealed class TipsDemo
	{
		float _t; int _step = -1;
		int Count(Game g, byte m, int x0, int x1, int y0, int y1) { int n = 0; for (int x = x0; x < x1; x++) for (int y = y0; y < y1; y++) if (g._world.Get(x, y) == m) n++; return n; }
		void Fill(Game g, byte m, int x0, int x1, int y0, int y1) { for (int x = x0; x < x1; x++) for (int y = y0; y < y1; y++) g._world.Set(x, y, m); }
		public Controls Think(Game g, float dt)
		{
			var c = new Controls();
			var p = g._p;
			_t += dt;
			int bx = g._site.BaseX, gy = g.BaseY, wx = bx - 60;
			g._slot = Tool.Pick;
			int step = (int)(_t / 1.5f);
			bool first = step != _step; _step = step;
			var aim = new Godot.Vector2(wx + 1, gy - 4);
			switch (step)
			{
				case 0:
					if (first) { p.X = wx - 6; p.Y = gy; p.Facing = true; Fill(g, Mat.Granite, wx, wx + 8, gy - 8, gy); g.ResetTip(); }
					c.Aim = aim; c.Dig = _t < .35f; break;
				case 1:
					if (first) { GD.Print($"tips plain pick: granite left {Count(g, Mat.Granite, wx, wx + 8, gy - 8, gy)}/64"); Fill(g, Mat.Granite, wx, wx + 8, gy - 8, gy); g._world.Set(wx + 1, gy - 4, Mat.Crystal); g.ResetTip(); }
					if (_t < step * 1.5f + .2f) { c.Aim = aim; c.Spray = true; } else { c.Aim = aim; c.Dig = _t < step * 1.5f + .55f || step == 4; }
					break;
				case 2:
					if (first) { GD.Print($"tips crystal tip: granite left {Count(g, Mat.Granite, wx, wx + 8, gy - 8, gy)}/64 tip={g.TipName} msg={p.Message}"); Fill(g, Mat.Empty, wx, wx + 8, gy - 8, gy); g._world.Set(wx + 1, gy - 4, Mat.Lava); g.ResetTip(); }
					if (_t < step * 1.5f + .2f) { c.Aim = aim; c.Spray = true; }
					else if (_t < step * 1.5f + .3f) { GD.Print($"tips lava tipped: tip={g.TipName} msg={p.Message}"); Fill(g, Mat.Water, wx, wx + 6, gy - 6, gy); }
					else { c.Aim = new Godot.Vector2(wx + 2, gy - 2); c.Dig = true; }
					break;
				case 3:
					if (first) { GD.Print($"tips after dunking: tip={g.TipName} msg={p.Message}"); g._world.Set(wx + 1, gy - 4, Mat.Cryo); g.ResetTip(); Fill(g, Mat.Water, wx + 2, wx + 7, gy - 5, gy); }
					if (_t < step * 1.5f + .2f) { c.Aim = aim; c.Spray = true; }
					else { c.Aim = new Godot.Vector2(wx + 3, gy - 1); c.Dig = true; }
					break;
				case 4:
					if (first) { GD.Print($"tips cryo tip: tip={g.TipName} ice={Count(g, Mat.Ice, wx - 2, wx + 8, gy - 8, gy)} water={Count(g, Mat.Water, wx - 2, wx + 8, gy - 8, gy)} msg={p.Message}"); Fill(g, Mat.Granite, wx, wx + 8, gy - 8, gy); g._world.Set(wx + 1, gy - 4, Mat.Void); g.ResetTip(); }
					if (_t < step * 1.5f + .2f) { c.Aim = aim; c.Spray = true; } else { c.Aim = aim; c.Dig = _t < step * 1.5f + .55f || step == 4; }
					break;
				default:
					GD.Print($"tips void tip: tip={g.TipName} granite left {Count(g, Mat.Granite, wx, wx + 8, gy - 8, gy)}/64 pick={g._pack[Item.Pick]} msg={p.Message}");
					g.GetTree().Quit(); break;
			}
			return c;
		}
	}
}
