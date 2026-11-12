using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Strata.Rules;
using Strata.Sim;

namespace Strata.Game;

/// <summary>
/// MVP scene. A long strip of surface: the base (generator, bench, forge) at the west end, a grey-lace bloom and ore
/// outcrops along the way, and the dig site with its winch and cave near the east end. Drive the truck between them,
/// dig materials, craft at the base by hand, go down the line for the sample and bring it home.
///
/// The visible part of the world is drawn into one low-resolution image each frame (material colour × light), with
/// the player, truck, rope and base structures drawn as pixels on top.
/// </summary>
public partial class Game : Node2D
{
	const int CellPx = 4;                       // screen pixels per cell
	int _vw, _vh;                              // view size in cells
	World _world = null!;
	Player _p = null!;
	Truck _truck = null!;
	Underground.Site _site = null!;
	ulong _seed = 1;

	readonly Pack _pack = new();
	Bench _bench = new();
	Forge _forge = new();
	Item _held = Item.Ore;
	bool _inTruck, _won, _sampleOnBench;
	int BenchX => _site.BaseX - 16;
	int ForgeX => _site.BaseX + 44;
	int GenX => _site.BaseX - 112;
	int LockerX => _site.BaseX - 70;
	int CotX => _site.BaseX - 48;
	int MapX => _site.BaseX + 18;
	int RackX => _site.BaseX + 66;
	const int BaseHalf = Underground.BaseHalf;

	/// <summary>Everything drawn fully lit while we work on caving; L toggles the real lighting back on.</summary>
	bool _fullBright = true;
	bool _mapOpen;
	TextureRect _map = null!;
	ImageTexture? _mapTex;
	byte[] _mapPx = [];
	int BaseY => _site.BaseY;

	Image _img = null!;
	ImageTexture _tex = null!;
	Sprite2D _sprite = null!;
	byte[] _px = [];
	float[] _light = [];
	bool[] _seen = [];
	int _camX, _camY;
	float _camFX, _camFY, _look, _shake, _shX, _shY;
	readonly ActorLayer _actors = new() { Size = CellPx };
	// Interpolated draw positions, and the positions at the start of the current physics tick.
	float _playerX, _playerY, _truckX, _truckRearY, _truckFrontY;
	float _prevPX, _prevPY, _prevTX, _prevTRY, _prevTFY;

	void SnapshotPositions()
	{
		_prevPX = _p.X; _prevPY = _p.Y; _prevTX = _truck.X; _prevTRY = _truck.RearY; _prevTFY = _truck.FrontY;
	}
	bool _paused;
	Rng _rng = new(99);

	readonly List<Flare> _flares = [];
	float _digCooldown, _hammerCooldown, _hammerFlash;

	Label _hud = null!, _msg = null!, _hint = null!;
	BodyPanel _bodyPanel = null!;

	// ---------- Input: keyboard + mouse and the first connected controller, usable side by side ----------
	//
	// Controller: left stick / d-pad move (and reel up/down on the line), right stick aims the lamp, digging and
	// flares, A jump, B clip, X use, Y lamp, RT dig or hammer, RB throw flare, LB cycle held item, Start pause,
	// Back restart.
	const int Pad = 0;
	const float Dead = .25f;
	readonly HashSet<Key> _down = [];
	bool _stickAim;
	float _aimAngle = MathF.PI / 2, _aimDist = 8;

	static bool PadButton(JoyButton b) => b != JoyButton.Invalid && Input.IsJoyButtonPressed(Pad, b);
	static float Axis(JoyAxis a) { float v = Input.GetJoyAxis(Pad, a); return MathF.Abs(v) < Dead ? 0 : v; }

	/// <summary>True on the frame the key or controller button goes down.</summary>
	bool Pressed(Key k, JoyButton b = JoyButton.Invalid)
	{
		bool now = Input.IsPhysicalKeyPressed(k) || PadButton(b);
		bool was = _down.Contains(k);
		if (now) _down.Add(k); else _down.Remove(k);
		return now && !was;
	}

	static bool Held(Key k, JoyButton b = JoyButton.Invalid) => Input.IsPhysicalKeyPressed(k) || PadButton(b);

	static float MoveX => Math.Clamp(Axis(JoyAxis.LeftX) + (Held(Key.D, JoyButton.DpadRight) ? 1 : 0) - (Held(Key.A, JoyButton.DpadLeft) ? 1 : 0), -1, 1);
	static float MoveY => Math.Clamp(Axis(JoyAxis.LeftY) + (Held(Key.S, JoyButton.DpadDown) ? 1 : 0) - (Held(Key.W, JoyButton.DpadUp) ? 1 : 0), -1, 1);
	static bool DigHeld => Input.IsMouseButtonPressed(MouseButton.Left) || Input.GetJoyAxis(Pad, JoyAxis.TriggerRight) > .5f;

	/// <summary>One tick of player intent, from the devices or from the autopilot.</summary>
	struct Controls
	{
		public float MoveX, MoveY;
		public bool Jump, JumpHeld, Clip, Use, Dig, Flare, Lamp, NextItem, Spray, SprayNext, SprayPrev, Grapple, Beacon;
		public Vector2? Aim;     // world cells; null means use mouse or stick
	}
	Controls _ctl;
	Autopilot? _bot;
	ClimbDemo? _climbDemo;
	SprayDemo? _sprayDemo;
	WalkDemo? _walkDemo;
	GrappleDemo? _grappleDemo;

	Controls ReadControls() => new()
	{
		MoveX = MoveX, MoveY = MoveY,
		Jump = Pressed(Key.Space, JoyButton.A), JumpHeld = Held(Key.Space, JoyButton.A),
		Clip = Pressed(Key.C, JoyButton.B), Use = Pressed(Key.E, JoyButton.X),
		Dig = DigHeld, Flare = Pressed(Key.Q, JoyButton.RightStick), Lamp = Pressed(Key.F, JoyButton.Y),
		NextItem = Pressed(Key.Tab),
		Spray = Input.IsMouseButtonPressed(MouseButton.Right) || Input.GetJoyAxis(Pad, JoyAxis.TriggerLeft) > .5f,
		SprayNext = Pressed(Key.X, JoyButton.DpadRight) || TakeWheel(+1),
		SprayPrev = Pressed(Key.Z, JoyButton.DpadLeft) || TakeWheel(-1),
		Grapple = Pressed(Key.G, JoyButton.LeftShoulder) || Input.IsMouseButtonPressed(MouseButton.Middle) && !_midWasDown,
		Beacon = Held(Key.H, JoyButton.DpadDown),
	};
	bool _midWasDown;

	int _wheel;   // mouse wheel steps since the last tick
	bool TakeWheel(int dir) { if (Math.Sign(_wheel) != dir) return false; _wheel -= dir; return true; }

	public override void _Input(InputEvent e)
	{
		if (e is InputEventMouseButton mb && mb.Pressed)
		{
			if (mb.ButtonIndex == MouseButton.WheelUp) _wheel++;
			if (mb.ButtonIndex == MouseButton.WheelDown) _wheel--;
		}
		if (e is InputEventMouseMotion) _stickAim = false;   // the mouse takes over aiming as soon as it moves
	}

	/// <summary>Where the player is aiming, in cells: the mouse, or the right stick relative to the head.</summary>
	(float x, float y) AimPoint(float hx, float hy)
	{
		if (_ctl.Aim is Vector2 a) return (a.X, a.Y);
		float rx = Axis(JoyAxis.RightX), ry = Axis(JoyAxis.RightY), mag = MathF.Sqrt(rx * rx + ry * ry);
		if (mag > 0) { _stickAim = true; _aimAngle = MathF.Atan2(ry, rx); _aimDist = 3 + 8 * MathF.Min(1, mag); }
		if (_stickAim || !GetViewportRect().HasPoint(GetViewport().GetMousePosition()))
			return (hx + MathF.Cos(_aimAngle) * _aimDist, hy + MathF.Sin(_aimAngle) * _aimDist);
		var mp = GetViewport().GetMousePosition() / CellPx;
		return (_camFX + mp.X, _camFY + mp.Y);
	}

	// Automation: --shot=<path> --shot-frames=<n> [--seed=<n>] [--demo-craft]; --play=<dir> runs the autopilot
	// through the whole demo and saves screenshots and a log there.
	string? _shotPath; int _shotFrames = 120, _frame; bool _demoCraft;
	string? _mapPath; int _mapSteps; bool _probe;

	sealed class Flare { public float X, Y, VX, VY, Life = 20; }

	// ---------- The sprayer: throw any material from your hands ----------
	//
	// Liquids, powders and solids fly as droplets on an arc and settle where they land; gases and flames come out as
	// a short cone in front of you. Unlimited for now, to see how the materials play.

	static readonly byte[] SprayList =
	[
		Mat.Water, Mat.Lava, Mat.Acid, Mat.Cryo, Mat.Oil, Mat.Fire, Mat.Coldflame, Mat.Methane, Mat.Hydrogen, Mat.Steam, Mat.CO2,
		Mat.Sand, Mat.CoalDust, Mat.Skydust, Mat.Snow, Mat.Ember, Mat.Salt, Mat.Spore, Mat.Glimmer, Mat.Mycelium, Mat.Moss,
		Mat.Ghostwater, Mat.Mite, Mat.Slime, Mat.Quicksilver, Mat.Brine, Mat.Mud, Mat.Tar, Mat.FoulGas,
		Mat.Rock, Mat.Ice, Mat.Crystal, Mat.Sunstone, Mat.Resonite, Mat.Void,
	];
	int _sprayIndex;
	byte SprayMat => SprayList[_sprayIndex];

	sealed class Drop { public float X, Y, VX, VY, Life; public byte M; }
	readonly List<Drop> _spray = [];
	float _sprayAcc;

	void Spray(float hx, float hy, float mx, float my)
	{
		byte m = SprayMat;
		float ax = mx - hx, ay = my - hy, d = MathF.Max(.01f, MathF.Sqrt(ax * ax + ay * ay));
		float dx = ax / d, dy = ay / d;
		float nx = hx + dx * 2.5f, ny = hy + dy * 2.5f;     // the nozzle, just in front of the hands
		_sprayAcc += 1 / 60f * 55;                           // about 55 cells a second
		while (_sprayAcc >= 1)
		{
			_sprayAcc -= 1;
			float a = MathF.Atan2(dy, dx) + (_rng.Float() - .5f) * .35f;
			if (MaterialTable.Gas[m])
			{
				// Gas and flame: a short cone, placed straight into open cells.
				float dist = 2 + _rng.Float() * 9;
				int cx = (int)MathF.Floor(nx + MathF.Cos(a) * dist), cy = (int)MathF.Floor(ny + MathF.Sin(a) * dist);
				if (ClearLine(nx, ny, cx, cy) && _world.Get(cx, cy) == Mat.Empty) _world.Set(cx, cy, m, World.DefaultLife(m));
			}
			else
			{
				float v = 55 + _rng.Float() * 15;
				_spray.Add(new Drop { X = nx, Y = ny, VX = MathF.Cos(a) * v + _p.VX * .5f, VY = MathF.Sin(a) * v + _p.VY * .3f, Life = 2.5f, M = m });
			}
		}
		if (_rng.Chance(.25f)) Dust(nx, ny, MaterialTable.Color[m], 1, 4, 2);
	}

	bool ClearLine(float x0, float y0, int x1, int y1)
	{
		float d = MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0));
		for (int i = 1; i <= d; i++)
		{
			float t = i / d;
			byte c = _world.Get((int)MathF.Floor(x0 + (x1 - x0) * t), (int)MathF.Floor(y0 + (y1 - y0) * t));
			if (c != Mat.Empty && !MaterialTable.Gas[c]) return false;
		}
		return true;
	}

	void UpdateSpray(float dt)
	{
		for (int i = _spray.Count - 1; i >= 0; i--)
		{
			var p = _spray[i];
			p.Life -= dt;
			p.VY += (p.M == Mat.Skydust ? -70 : 70) * dt;   // skydust falls up, even in flight
			float x1 = p.X + p.VX * dt, y1 = p.Y + p.VY * dt;
			// Walk the path in small steps; settle in the last open cell before anything in the way.
			float steps = MathF.Max(1, MathF.Ceiling(MathF.Max(MathF.Abs(x1 - p.X), MathF.Abs(y1 - p.Y))));
			bool hit = false;
			float px = p.X, py = p.Y;
			for (int s = 1; s <= steps; s++)
			{
				float qx = p.X + (x1 - p.X) * s / steps, qy = p.Y + (y1 - p.Y) * s / steps;
				byte c = _world.Get((int)MathF.Floor(qx), (int)MathF.Floor(qy));
				if (c != Mat.Empty && !MaterialTable.Gas[c]) { hit = true; break; }
				px = qx; py = qy;
			}
			p.X = px; p.Y = py;
			if (hit || p.Life <= 0)
			{
				Settle(p);
				_spray.RemoveAt(i);
			}
		}
	}

	/// <summary>Puts a landed droplet into the world: in its own cell if open, else the nearest open cell next to it.</summary>
	void Settle(Drop p)
	{
		int x = (int)MathF.Floor(p.X), y = (int)MathF.Floor(p.Y);
		for (int r = 0; r <= 2; r++)
			for (int dy = -r; dy <= r; dy++)
				for (int dx = -r; dx <= r; dx++)
				{
					byte c = _world.Get(x + dx, y + dy);
					if (InBody(x + dx, y + dy)) continue;
					if (c == Mat.Empty || MaterialTable.Gas[c] && c != Mat.Fire)
					{
						_world.Set(x + dx, y + dy, p.M, World.DefaultLife(p.M));
						return;
					}
				}
	}

	bool InBody(int x, int y)
	{
		int px = (int)MathF.Floor(_p.X), py = (int)MathF.Floor(_p.Y);
		return !_inTruck && x >= px - Player.HalfW && x <= px + Player.HalfW && y >= py - Player.Height && y < py;
	}

	// ---------- Grappling hook ----------
	const float HookSpeed = 220, HookRange = 90, BeaconTime = 2.5f;
	bool _hookFlying, _grappled;
	float _hookX, _hookY, _hookDX, _hookDY, _hookOut, _beacon;

	void UpdateHook(float dt)
	{
		if (!_hookFlying) return;
		float step = HookSpeed * dt;
		for (float t = 0; t < step; t += .5f)
		{
			float nx = _hookX + _hookDX * .5f, ny = _hookY + _hookDY * .5f;
			_hookOut += .5f;
			byte c = _world.Get((int)MathF.Floor(nx), (int)MathF.Floor(ny));
			if (Player.BlocksBody(c))
			{
				if (!MaterialTable.Solid[c]) { _hookFlying = false; Say($"The hook tears out of the {MaterialTable.Defs[c].Name}."); return; }
				_hookFlying = false; _grappled = true;
				_p.AnchorX = _hookX; _p.AnchorY = _hookY; _p.Clipped = true;
				float dx = _p.X - _hookX, dy = _p.Y - Player.Height - _hookY;
				_p.RopeLen = MathF.Max(2, MathF.Sqrt(dx * dx + dy * dy));
				Sparks(_hookX, _hookY, 3);
				Say($"Hooked into {MaterialTable.Defs[c].Name}. W/S to climb the line · jump or G to let go.");
				return;
			}
			_hookX = nx; _hookY = ny;
			if (_hookOut >= HookRange) { _hookFlying = false; Say("The hook falls short."); return; }
		}
	}

	/// <summary>Short-lived bits for feel: dust and grit from digging and landing, sparks off rock and ore.</summary>
	sealed class Bit { public float X, Y, VX, VY, Life, Max, Size; public uint Color; public bool Spark; }
	readonly List<Bit> _bits = [];

	void Dust(float x, float y, uint color, int n, float speed = 14, float up = 16)
	{
		for (int i = 0; i < n; i++)
			_bits.Add(new Bit { X = x, Y = y, VX = (_rng.Float() - .5f) * 2 * speed, VY = -up * (.3f + _rng.Float()), Life = .4f + _rng.Float() * .5f, Max = .9f, Size = 1 + _rng.Float() * 1.5f, Color = color });
	}

	void Sparks(float x, float y, int n)
	{
		for (int i = 0; i < n; i++)
		{
			float a = _rng.Float() * MathF.Tau, v = 25 + _rng.Float() * 35;
			_bits.Add(new Bit { X = x, Y = y, VX = MathF.Cos(a) * v, VY = MathF.Sin(a) * v - 15, Life = .15f + _rng.Float() * .25f, Max = .4f, Size = 1.2f, Color = 0xFFD070, Spark = true });
		}
	}

	void UpdateBits(float dt)
	{
		for (int i = _bits.Count - 1; i >= 0; i--)
		{
			var b = _bits[i];
			b.Life -= dt;
			if (b.Life <= 0) { _bits.RemoveAt(i); continue; }
			b.VY += (b.Spark ? 80 : 50) * dt; b.VX *= .97f;
			float nx = b.X + b.VX * dt, ny = b.Y + b.VY * dt;
			if (Player.BlocksBody(_world.Get((int)nx, (int)ny))) { b.VX *= -.3f; b.VY *= -.25f; nx = b.X; ny = b.Y; if (!b.Spark) b.Life *= .6f; }
			b.X = nx; b.Y = ny;
			// A spark landing in methane sets it off.
			if (b.Spark && _world.Get((int)b.X, (int)b.Y) == Mat.Methane) { _world.Set((int)b.X, (int)b.Y, Mat.Fire, 30); b.Life = 0; }
		}
	}

	static readonly Item[] Selectable = [Item.Stone, Item.Sand, Item.Ore, Item.Fibre, Item.Metal, Item.Glass, Item.Rope];

	public override void _Ready()
	{
		foreach (var a in OS.GetCmdlineUserArgs())
		{
			if (a.StartsWith("--shot=")) _shotPath = a[7..];
			else if (a.StartsWith("--shot-frames=")) _shotFrames = int.Parse(a[14..]);
			else if (a.StartsWith("--seed=")) _seed = ulong.Parse(a[7..]);
			else if (a.StartsWith("--play=")) _bot = new Autopilot(a[7..]);
			else if (a.StartsWith("--demo-climb=")) _climbDemo = new ClimbDemo(a[13..]);
			else if (a.StartsWith("--demo-spray=")) _sprayDemo = new SprayDemo(a[13..]);
			else if (a == "--demo-walk") _walkDemo = new WalkDemo();
			else if (a == "--demo-grapple") _grappleDemo = new GrappleDemo();
			else if (a.StartsWith("--demo-out=")) _walkDemo = new WalkDemo { Out = float.Parse(a[11..]) };
			else if (a == "--demo-craft") _demoCraft = true;
			else if (a.StartsWith("--map=")) _mapPath = a[6..];
			else if (a.StartsWith("--map-steps=")) _mapSteps = int.Parse(a[12..]);
			else if (a == "--probe") _probe = true;
		}

		var vp = GetViewportRect().Size;
		_vw = (int)vp.X / CellPx + 1; _vh = (int)vp.Y / CellPx + 1;    // one spare cell for sub-cell scrolling
		_px = new byte[_vw * _vh * 4];
		_light = new float[_vw * _vh];
		_img = Image.CreateEmpty(_vw, _vh, false, Image.Format.Rgba8);
		_tex = ImageTexture.CreateFromImage(_img);
		_sprite = new Sprite2D { Texture = _tex, Centered = false, Scale = new Vector2(CellPx, CellPx), TextureFilter = TextureFilterEnum.Nearest };
		AddChild(_sprite);
		AddChild(_actors);

		var ui = new CanvasLayer();
		AddChild(ui);
		_hud = MakeLabel(ui, new Vector2(12, 8), 15, "e4ddcf");
		_hint = MakeLabel(ui, new Vector2(0, vp.Y - 74), 17, "6fdcc0");
		_hint.HorizontalAlignment = HorizontalAlignment.Center; _hint.Size = new Vector2(vp.X, 26);
		_msg = MakeLabel(ui, new Vector2(0, vp.Y - 44), 18, "e3a33f");
		_msg.HorizontalAlignment = HorizontalAlignment.Center; _msg.Size = new Vector2(vp.X, 30);
		_map = new TextureRect
		{
			Visible = false, Position = vp * .05f, Size = vp * .9f, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest,
		};
		ui.AddChild(_map);
		_bodyPanel = new BodyPanel { Position = new Vector2(vp.X - 90, 12) };
		ui.AddChild(_bodyPanel);

		NewRun();
		if (_mapPath != null) { ExportMap(_mapPath, _mapSteps); GetTree().Quit(); return; }
		if (_bot != null) { Engine.PhysicsTicksPerSecond = 180; Engine.TimeScale = 3; Engine.MaxPhysicsStepsPerFrame = 12; }
	}

	static Label MakeLabel(Node parent, Vector2 pos, int size, string color)
	{
		var l = new Label { Position = pos };
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_color", new Color(color));
		l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .85f));
		l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, .9f));
		l.AddThemeConstantOverride("outline_size", 5);
		parent.AddChild(l);
		return l;
	}

	void NewRun()
	{
		_world = new World(32, 10, _seed);               // 2048 × 640 cells
		_site = Underground.Generate(_world, _seed);
		_p = new Player(_world, _site.ShaftX, CaveGen.Surface - 14) { X = _site.BaseX + 30, Y = BaseY };
		_p.Message = "You're in the base, deep underground. Explore. Right mouse sprays materials; the wheel picks which.";
		_truck = new Truck(_world, _site.ShaftX + Truck.HalfLength + Truck.BoomReach);   // parked up top, winch over the shaft
		Array.Clear(_pack.Count); _pack.Add(Item.Flare, 2);
		_bench = new Bench(); _forge = new Forge();
		_inTruck = _won = _sampleOnBench = false;
		_seen = new bool[_world.Width * _world.Height];
		_flares.Clear(); _bits.Clear(); _spray.Clear();
		_hookFlying = _grappled = false; _beacon = 0;
		_mapOpen = false; if (_map != null) _map.Visible = false;
		_bodyPanel.Body = _p.Body;
		SnapshotPositions();
		if (_demoCraft) { _pack.Add(Item.Ore, 3); _pack.Add(Item.Fibre, 4); _pack.Add(Item.Sand, 2); _forge.Put(Item.Ore); _bench.Place(Item.Fibre); _bench.Place(Item.Fibre); _held = Item.Fibre; _p.X = BenchX + 2; }
	}

	void Say(string s) => _p.Message = s;

	float _collapseNoticeCooldown, _healTimer;

	/// <summary>Explosions knock you about and hurt you by distance; collapses nearby shake the ground.</summary>
	void HandleSimEvents()
	{
		_collapseNoticeCooldown = MathF.Max(0, _collapseNoticeCooldown - 1 / 60f);
		float px = _p.X, py = _p.Y - Player.Height / 2f;
		SimEvent? worst = null; float worstD = float.MaxValue;
		foreach (var e in _world.Events)
		{
			float d = MathF.Sqrt((e.X - px) * (e.X - px) + (e.Y - py) * (e.Y - py));
			if (e.Kind == SimEventKind.Explosion)
			{
				_shake = MathF.Max(_shake, MathF.Min(1, 1.4f - d / 60));
				if (d < worstD) { worstD = d; worst = e; }
			}
			else if (e.Kind == SimEventKind.Collapse && d < 45)
			{
				_shake = MathF.Max(_shake, .3f * (1 - d / 45));
				if (_collapseNoticeCooldown <= 0 && d < 30) { Say("Rock gives way somewhere close."); _collapseNoticeCooldown = 4; }
			}
		}
		if (worst is SimEvent w && !_inTruck)
		{
			float reach = w.Power + 5;
			if (worstD < reach)
			{
				float k = 1 - worstD / reach;
				float dx = px - w.X, dy = py - w.Y, len = MathF.Max(.5f, MathF.Sqrt(dx * dx + dy * dy));
				_p.VX += dx / len * 70 * k; _p.VY += dy / len * 70 * k - 20 * k;
				_p.Clinging = false;
				var b = _p.Body;
				b.Injure(Part.Torso, k > .6f ? Hurt.Useless : Hurt.Hurt);
				if (k > .4f) b.Injure(_rng.Chance(.5f) ? Part.LeftArm : Part.RightArm, Hurt.Hurt);
				if (k > .7f) { b.Injure(Part.Head, Hurt.Hurt); b.Downed = true; }
				Say(k > .7f ? "The blast throws you down." : "The blast knocks you sideways.");
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Pressed(Key.R, JoyButton.Back)) NewRun();
		if (Pressed(Key.N)) { _seed = (ulong)Random.Shared.NextInt64(1, int.MaxValue); NewRun(); }
		if (Pressed(Key.P, JoyButton.Start)) _paused = !_paused;
		if (Pressed(Key.L)) { _fullBright = !_fullBright; Say(_fullBright ? "Lights: everything lit." : "Lights: real lighting (lamp, flares, glowing things)."); }
		bool shot = Pressed(Key.F12);
		SnapshotPositions();
		if (_paused) return;
		float dt = (float)delta;

		_ctl = ReadControls();
		if (_bot != null) _ctl = _bot.Think(this, dt);
		if (_climbDemo != null) _ctl = _climbDemo.Think(this, dt);
		if (_sprayDemo != null) _ctl = _sprayDemo.Think(this, dt);
		if (_walkDemo != null) _ctl = _walkDemo.Think(this, dt);
		if (_grappleDemo != null) _ctl = _grappleDemo.Think(this, dt);
		for (int k = 0; k < Selectable.Length; k++) if (Pressed(Key.Key1 + k)) _held = Selectable[k];
		if (_ctl.NextItem) _held = Selectable[(Array.IndexOf(Selectable, _held) + 1) % Selectable.Length];
		if (_ctl.Lamp && _p.Battery > 0) _p.LampOn = !_p.LampOn;

		_midWasDown = Input.IsMouseButtonPressed(MouseButton.Middle);
		if (_grappled && !_p.Clipped) _grappled = false;                  // unclipped (C) lets go of the hook
		if (_grappled) (_p.AnchorX, _p.AnchorY) = (_hookX, _hookY);
		else (_p.AnchorX, _p.AnchorY) = _truck.WinchAnchor;              // the winch rides on the truck
		if (_inTruck) DriveTick(dt, _ctl.Use);
		else WalkTick(dt, _ctl.Use);

		_world.Step();
		HandleSimEvents();
		_forge.Update(dt);
		if (_forge.Burning && _rng.Chance(.3f)) _world.Set(ForgeX + _rng.Int(5) - 2, BaseY - 10 - _rng.Int(3), Mat.Fire, 20);
		UpdateFlares(dt);
		UpdateBits(dt);
		UpdateSpray(dt);
		_hammerFlash = MathF.Max(0, _hammerFlash - dt);

		// Home comforts: the generator charges the lamp, and the pump refuels a parked truck.
		if (MathF.Abs(_p.X - GenX) < 24) _p.Battery = MathF.Min(_p.BatteryMax, _p.Battery + 25 * dt);
		if (MathF.Abs(_truck.X - _site.BaseX - 60) < 50 && MathF.Abs(_truck.Speed) < 1) _truck.Fuel = MathF.Min(_truck.FuelMax, _truck.Fuel + 15 * dt);

		// The base keeps you alive: its scrubbers refill your air, and resting there mends injuries.
		if (MathF.Abs(_p.X - _site.BaseX) < BaseHalf && MathF.Abs(_p.Y - BaseY) < 34)
		{
			_p.Air = MathF.Min(_p.AirMax, _p.Air + 30 * dt);
			_healTimer += dt;
			if (_healTimer > .6f)
			{
				_healTimer = 0;
				var parts = _p.Body.Parts;
				int worst = Array.IndexOf(parts, parts.Max());
				if (parts[worst] != Hurt.Fine) { parts[worst]--; if (parts.All(p => p == Hurt.Fine)) Say("Patched up."); }
				if (_p.Body.Downed && parts.All(p => p != Hurt.Useless)) _p.Body.Downed = false;
			}
		}

		if (!_won && _p.CarryingSample && MathF.Abs(_p.X - _site.BaseX) < BaseHalf - 10 && MathF.Abs(_p.Y - BaseY) < 4)
		{ _won = true; Say("Sample delivered to the base. R to go again, N for a new world."); }

		if (_mapOpen)
		{
			if (!NearSurface(MapX, 14)) { _mapOpen = false; _map.Visible = false; }
			else if (_frame % 20 == 0) BuildMap();
		}

		if (shot) SaveShot($"user://shot-{DateTime.Now:HHmmss}.png");
	}

	void DriveTick(float dt, bool use)
	{
		var m = _truck.Update(_ctl.MoveX, _ctl.MoveY, dt);
		if (m != null && m != _p.Message) Say(m);
		_p.X = _truck.X + (_truck.Facing ? 5 : -5); _p.Y = _truck.Y - 3; _p.VX = _p.VY = 0;
		_p.Air = MathF.Min(_p.AirMax, _p.Air + 25 * dt);
		_p.Battery = MathF.Min(_p.BatteryMax, _p.Battery + 5 * dt);
		if (use && MathF.Abs(_truck.Speed) < 3)
		{
			_inTruck = false;
			// Step out on whichever side has ground at the truck's level (the back may be over the shaft).
			int dir = _truck.Facing ? 1 : -1;
			float back = _truck.X - dir * (Truck.HalfLength + 3), front = _truck.X + dir * (Truck.HalfLength + 3);
			_p.X = MathF.Abs(_truck.GroundAt(back) - _truck.Y) < 4 ? back : front;
			_p.Y = _truck.GroundAt(_p.X);
			Say("Out of the truck.");
		}
		else if (use) Say("Stop first.");
	}

	void WalkTick(float dt, bool use)
	{
		var inp = new PlayerInput
		{
			MoveX = _ctl.MoveX, Left = _ctl.MoveX < -.4f, Right = _ctl.MoveX > .4f, Up = _ctl.MoveY < -.5f, Down = _ctl.MoveY > .5f,
			JumpPressed = _ctl.Jump, JumpHeld = _ctl.JumpHeld, ClipPressed = _ctl.Clip,
		};

		float hx = _p.X, hy = _p.Y - Player.Height + 2;
		var (mx, my) = AimPoint(hx, hy);
		float reach = MathF.Sqrt((mx - hx) * (mx - hx) + (my - hy) * (my - hy));
		bool atBench = NearSurface(BenchX, 8);

		_digCooldown -= dt; _hammerCooldown -= dt;
		bool lmb = _ctl.Dig && !_p.Body.Downed;
		if (lmb && atBench && _bench.On.Count > 0) Hammer();
		else if (lmb && _digCooldown <= 0 && reach < 12) Dig(mx, my);

		if (_ctl.SprayNext) _sprayIndex = (_sprayIndex + 1) % SprayList.Length;
		if (_ctl.SprayPrev) _sprayIndex = (_sprayIndex - 1 + SprayList.Length) % SprayList.Length;
		if (_ctl.Spray && !_p.Body.Downed) Spray(hx, hy, mx, my);

		if (_ctl.Flare && !_p.Body.Downed && _pack.Take(Item.Flare))
		{
			float ax = mx - hx, ay = my - hy, d = MathF.Max(1, MathF.Sqrt(ax * ax + ay * ay));
			_flares.Add(new Flare { X = hx, Y = hy, VX = ax / d * 40, VY = ay / d * 40 - 10 });
		}
		if (use && !_p.Body.Downed) Interact();

		// Grappling hook: fire at the aim; it bites the first solid thing it hits and you're on a line to it.
		if (_ctl.Grapple && !_p.Body.Downed)
		{
			if (_grappled) { _grappled = false; _p.Clipped = false; Say("Hook released."); }
			else if (!_hookFlying)
			{
				float ax = mx - hx, ay = my - hy, d = MathF.Max(.01f, MathF.Sqrt(ax * ax + ay * ay));
				_hookFlying = true; _hookX = hx; _hookY = hy; _hookDX = ax / d; _hookDY = ay / d; _hookOut = 0;
			}
		}
		UpdateHook(dt);
		// Jump while hanging from the hook: let go with a kick.
		if (_grappled && _ctl.Jump && !_p.Grounded) { _grappled = false; _p.Clipped = false; _p.VY = MathF.Min(_p.VY, -35); _p.VX *= 1.2f; }

		// Rescue beacon: hold it and the base hauls you home.
		if (_ctl.Beacon && !_inTruck)
		{
			_beacon += dt;
			if (_beacon >= BeaconTime)
			{
				_beacon = 0; _grappled = false; _p.Clipped = false; _hookFlying = false;
				_p.X = _site.BaseX + 30; _p.Y = BaseY; _p.VX = _p.VY = 0; _p.Body.Downed = false;
				_playerX = _p.X; _playerY = _p.Y;
				Say("The rescue beacon did its job. You're back at the base.");
			}
		}
		else _beacon = 0;

		_p.Unstick();
		_p.Update(inp, dt);
		if (_p.LandingImpact > 30) _shake = MathF.Max(_shake, MathF.Min(1, (_p.LandingImpact - 30) / 60));
		if (_p.LandingImpact > 25) Dust(_p.X, _p.Y - .5f, 0x8A8076, (int)MathF.Min(10, _p.LandingImpact / 8), 22, 8);
		if (_p.Mantled) Dust(_p.X, _p.Y - .5f, 0x8A8076, 4, 10, 6);
		if (_p.GripFailed) Dust(_p.X + _p.ClingDir * 2, _p.Y - 4, 0x8A8076, 5, 6, 2);
		if (_p.Clinging && (Held(Key.W, JoyButton.DpadUp) || _ctl.MoveY != 0) && _rng.Chance(.08f)) Dust(_p.X + _p.ClingDir * 2, _p.Y - 5, 0x7A746E, 1, 4, 2);
	}

	bool NearSurface(float x, float r) => MathF.Abs(_p.X - x) < r && _p.Y <= BaseY + 3 && _p.Y >= BaseY - 6;

	void Dig(float mx, float my)
	{
		bool pick = _pack[Item.Pick] > 0;
		int r = pick ? 2 : 1;
		_digCooldown = (pick ? .06f : .12f) * (_p.Body[Part.RightArm] >= Hurt.Hurt ? 2.5f : 1);
		bool full = false;
		float power = pick ? 2 : 1;
		for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
		{
			if (dx * dx + dy * dy > r * r + 1) continue;
			int x = (int)mx + dx, y = (int)my + dy;
			byte m = _world.Get(x, y);
			if (!(MaterialTable.Solid[m] || MaterialTable.Powder[m])) continue;
			float hard = MaterialTable.Hardness[m];
			if (hard <= 0 || !_rng.Chance(MathF.Min(1, power / hard))) continue;   // hard rock takes more strikes
			(Item item, float amt)? yield = m switch
			{
				Mat.Rock or Mat.Granite or Mat.Shale or Mat.Limestone or Mat.Basalt or Mat.Obsidian => (Item.Stone, 1 / 10f),
				Mat.Sand or Mat.Gravel or Mat.Dirt => (Item.Sand, 1 / 8f),
				Mat.Ore or Mat.Copper => (Item.Ore, 1 / 5f),
				Mat.Mycelium or Mat.Fungus or Mat.Root => (Item.Fibre, 1 / 4f), Mat.Moss => (Item.Fibre, 1 / 8f),
				Mat.Coal => (Item.Coal, 1 / 5f), Mat.Salt => (Item.Salt, 1 / 5f), Mat.Sulfur => (Item.Sulfur, 1 / 5f),
				Mat.Crystal or Mat.Shards => (Item.Crystal, 1 / 4f), Mat.Clay => (Item.Clay, 1 / 6f),
				_ => null,
			};
			if (yield != null && !_pack.AddPartial(yield.Value.item, yield.Value.amt)) full = true;
			// What stays behind: rubble from rock, a cloud of coal dust, crystal shards, meltwater from ice.
			byte left = m switch
			{
				Mat.Rock or Mat.Limestone or Mat.Basalt when _rng.Chance(.18f) => Mat.Sand,
				Mat.Shale or Mat.Granite when _rng.Chance(.25f) => Mat.Gravel,
				Mat.Coal when _rng.Chance(.35f) => Mat.CoalDust,
				Mat.Crystal when _rng.Chance(.5f) => Mat.Shards,
				Mat.Ice when _rng.Chance(.3f) => Mat.Water,
				_ => Mat.Empty,
			};
			_world.Set(x, y, left, World.DefaultLife(left));
			uint dc = MaterialTable.Color[m];
			Dust(x + .5f, y + .5f, dc, _rng.Chance(.6f) ? 2 : 1);
			// Striking hard rock or ore throws sparks. Mind the methane and the coal dust.
			if ((m is Mat.Rock or Mat.Ore or Mat.Copper or Mat.Granite or Mat.Basalt) && _rng.Chance(m is Mat.Ore or Mat.Copper ? .35f : m == Mat.Granite ? .25f : .12f))
				Sparks(x + .5f, y + .5f, 3 + _rng.Int(3));
		}
		if (full) Say("Your pack is full.");
		_shake = MathF.Max(_shake, .14f);
	}

	void Hammer()
	{
		if (_hammerCooldown > 0) return;
		_hammerCooldown = _p.Body[Part.RightArm] >= Hurt.Hurt ? .7f : .35f;
		_hammerFlash = .08f;
		if (_rng.Chance(.04f)) { _p.Body.Injure(Part.LeftArm, Hurt.Hurt); Say("You caught your hand with the hammer."); }
		var r = _bench.Strike();
		if (r == null) return;
		if (r.Value.item == Item.Junk) { Say("Whatever that was going to be, it isn't anything now."); return; }
		_pack.Add(r.Value.item, r.Value.count);
		Say($"Made {(r.Value.count > 1 ? r.Value.count + " " : "a ")}{ItemName(r.Value.item, r.Value.count)}.");
	}

	void Interact()
	{
		// The sample.
		if (!_p.CarryingSample && MathF.Abs(_site.SampleX - _p.X) < 5 && MathF.Abs(_site.SampleY - _p.Y) < 9)
		{ _p.CarryingSample = true; Say("Sample taken. Get it back to the base."); return; }

		// The forge: take out what's in it, or put the held thing in.
		if (NearSurface(ForgeX, 7))
		{
			if (_forge.Content != null)
			{
				bool hot = _forge.Hot;
				var got = _forge.TakeOut();
				if (got != null) _pack.Add(got.Value);
				if (hot) { _p.Body.Injure(Part.RightArm, Hurt.Hurt); Say($"You pull out the {ItemName(got ?? Item.Junk)} while it's still hot. Your hand burns."); }
				else Say($"Took the {ItemName(got ?? Item.Junk)} out of the forge.");
				return;
			}
			if (_pack.Take(_held)) { _forge.Put(_held); Say($"Put {ItemName(_held)} in the forge."); }
			else Say($"You have no {ItemName(_held)}.");
			return;
		}

		// The supply locker: flares and a rope.
		if (NearSurface(LockerX, 6))
		{
			int got = Math.Max(0, 5 - _pack[Item.Flare]);
			_pack.Add(Item.Flare, got);
			bool rope = _pack[Item.Rope] == 0; if (rope) _pack.Add(Item.Rope, 1);
			Say(got > 0 || rope ? $"From the locker: {(got > 0 ? $"{got} flare{(got > 1 ? "s" : "")}" : "")}{(got > 0 && rope ? " and " : "")}{(rope ? "a rope" : "")}." : "You're already stocked up.");
			return;
		}

		// The cot: lie down and get patched up properly.
		if (NearSurface(CotX, 8))
		{
			Array.Clear(_p.Body.Parts); _p.Body.Downed = false; _p.Battery = _p.BatteryMax;
			Say("You lie down for a while. Patched up, lamp charged.");
			return;
		}

		// The map table: everything you've seen so far.
		if (NearSurface(MapX, 8))
		{
			_mapOpen = !_mapOpen; _map.Visible = _mapOpen;
			if (_mapOpen) { BuildMap(); Say("The survey map: everywhere you've seen. E or walk away to put it down."); }
			return;
		}

		// The canister rack: swap the sprayer's canister.
		if (NearSurface(RackX, 10))
		{
			_sprayIndex = (_sprayIndex + 1) % SprayList.Length;
			Say($"Swapped in a canister of {MaterialTable.Defs[SprayMat].Name}.");
			return;
		}

		// The bench: lay the held thing on it.
		if (NearSurface(BenchX, 8))
		{
			if (_bench.On.Count >= Bench.MaxItems) { Say("The bench is full."); return; }
			if (_pack.Take(_held)) { _bench.Place(_held); Say($"Laid {ItemName(_held)} on the bench."); }
			else Say($"You have no {ItemName(_held)}.");
			return;
		}

		// The winch on the back of the truck: fit a rope to lengthen the line.
		if (_held == Item.Rope && MathF.Abs(_p.X - _p.AnchorX) < 12 && MathF.Abs(_p.Y - _truck.Y) < 10)
		{
			if (_pack.Take(Item.Rope)) { _p.RopeMax += 120; Say($"Spliced the rope onto the winch line. It now reaches {_p.RopeMax * .3f:F0} m."); }
			else Say("You have no rope.");
			return;
		}

		// The truck.
		if (MathF.Abs(_truck.X - _p.X) < Truck.HalfLength + 4 && MathF.Abs(_truck.Y - _p.Y) < 8)
		{
			if (_p.Clipped) { Say("Unclip from the line first (C)."); return; }
			_inTruck = true; Say("In the truck. A/D to drive, E to get out.");
			return;
		}
		Say("Nothing to use here.");
	}

	static string ItemName(Item i, int n = 1) => i switch
	{
		Item.Fibre => "fibre", Item.Glass => "glass", Item.Metal => "metal", Item.Sand => "sand", Item.Stone => "stone",
		Item.Ore => "ore", Item.Rope => n > 1 ? "ropes" : "rope", Item.Pick => "pick", Item.Flare => n > 1 ? "flares" : "flare",
		Item.Coal => "coal", Item.Salt => "salt", Item.Sulfur => "sulfur", Item.Crystal => "crystal", Item.Clay => "clay", _ => "junk",
	};

	void UpdateFlares(float dt)
	{
		for (int i = _flares.Count - 1; i >= 0; i--)
		{
			var f = _flares[i];
			f.Life -= dt;
			if (f.Life <= 0) { _flares.RemoveAt(i); continue; }
			f.VY += 60 * dt;
			float nx = f.X + f.VX * dt, ny = f.Y + f.VY * dt;
			if (Player.BlocksBody(_world.Get((int)nx, (int)f.Y))) { f.VX *= -.3f; nx = f.X; }
			if (Player.BlocksBody(_world.Get((int)nx, (int)ny))) { f.VY *= -.3f; f.VX *= .6f; ny = f.Y; }
			f.X = nx; f.Y = ny;
			for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
				if (_world.Get((int)f.X + dx, (int)f.Y + dy) is Mat.Methane or Mat.Spore) _world.Set((int)f.X + dx, (int)f.Y + dy, Mat.Fire, 30);
		}
	}

	public override void _Process(double delta)
	{
		// Positions between physics ticks, so movement is smooth at any frame rate.
		float f = (float)Engine.GetPhysicsInterpolationFraction();
		_playerX = _prevPX + (_p.X - _prevPX) * f; _playerY = _prevPY + (_p.Y - _prevPY) * f;
		_truckX = _prevTX + (_truck.X - _prevTX) * f;
		_truckRearY = _prevTRY + (_truck.RearY - _prevTRY) * f; _truckFrontY = _prevTFY + (_truck.FrontY - _prevTFY) * f;

		// Camera keeps the player (or truck) centred, follows smoothly, and may look above the world into the sky.
		// It moves in fractions of a cell: the world image is shifted by the leftover so scrolling is pixel-smooth.
		// Look ahead in the direction of travel: more when driving fast.
		float dtf = (float)delta;
		float lookTarget = _inTruck ? Math.Clamp(_truck.Speed * .7f, -50, 70) : Math.Clamp(_p.VX * .45f, -18, 18);
		_look += (lookTarget - _look) * (1 - MathF.Exp(-dtf * 2.5f));
		float fx = (_inTruck ? _truckX : _playerX) + _look, fy = (_inTruck ? MathF.Min(_truckRearY, _truckFrontY) : _playerY) - Player.Height / 2f;
		float tx = Math.Clamp(fx - (_vw - 1) / 2f, 0, _world.Width - _vw), ty = Math.Min(fy - (_vh - 1) / 2f, _world.Height - _vh);
		float k = 1 - MathF.Exp(-dtf * 10), ky = 1 - MathF.Exp(-dtf * 6);
		if (_frame == 0 || MathF.Abs(tx - _camFX) > _vw || MathF.Abs(ty - _camFY) > _vh) { _camFX = tx; _camFY = ty; }
		else { _camFX += (tx - _camFX) * k; _camFY += (ty - _camFY) * ky; }
		// A short shake on hard landings.
		_shake = MathF.Max(0, _shake - dtf * 2.5f);
		float sx = _shake * _shake * 2.2f * MathF.Sin(_frame * 1.7f), sy = _shake * _shake * 2.2f * MathF.Cos(_frame * 2.3f);
		_shX = sx; _shY = sy; _camFX += sx; _camFY += sy;
		_camX = (int)MathF.Floor(_camFX); _camY = (int)MathF.Floor(_camFY);
		_sprite.Position = new Vector2(-(_camFX - _camX) * CellPx, -(_camFY - _camY) * CellPx);
		_actors.Cells.Clear(); _actors.Marks.Clear(); _actors.Rot.Clear(); _actors.Texts.Clear();
		ComputeLight();
		DrawWorld();
		_img.SetData(_vw, _vh, false, Image.Format.Rgba8, _px);
		_tex.Update(_img);
		_actors.QueueRedraw();
		_camFX -= _shX; _camFY -= _shY;    // shake only affects what is drawn
		UpdateHud();
		_bodyPanel.QueueRedraw();

		_frame++;
		if (_probe && _frame % 60 == 0) GD.Print($"probe frame {_frame} fps {Engine.GetFramesPerSecond()} physics-ms {Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:F1} player {_p.X:F0},{_p.Y:F0} vx {_p.VX:F1} grounded {_p.Grounded} awake {_world.AwakeChunksLastStep} visited {_world.CellsVisitedLastStep}");
		if (_shotPath != null && _frame == _shotFrames) { SaveShot(_shotPath); GetTree().Quit(); }
	}

	/// <summary>Saves the whole world as an image, one pixel per cell, fully lit, for reviewing generated caves.</summary>
	void ExportMap(string path, int steps)
	{
		for (int i = 0; i < steps; i++) _world.Step();
		int W = _world.Width, H = _world.Height;
		var px = new byte[W * H * 4];
		var mat = _world.Materials; var shade = _world.Shades;
		for (int i = 0; i < W * H; i++)
		{
			byte m = mat[i]; uint c = MaterialTable.Color[m];
			float k = m == Mat.Empty ? 1 : .85f + shade[i] / 255f * .3f;
			if (m == Mat.Empty) c = i / W < CaveGen.Surface ? 0x2A2F38u : 0x0E0C0Bu;
			px[i * 4] = (byte)Math.Min(255, ((c >> 16) & 255) * k * 1.3f);
			px[i * 4 + 1] = (byte)Math.Min(255, ((c >> 8) & 255) * k * 1.3f);
			px[i * 4 + 2] = (byte)Math.Min(255, (c & 255) * k * 1.3f);
			px[i * 4 + 3] = 255;
		}
		void Mark(int cx, int cy, byte r, byte g, byte b)
		{
			for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++)
				if ((dx == 0 || dy == 0) && _world.InBounds(cx + dx, cy + dy)) { int i = ((cy + dy) * W + cx + dx) * 4; px[i] = r; px[i + 1] = g; px[i + 2] = b; }
		}
		Mark(_site.BaseX, BaseY - 4, 255, 255, 255);
		Mark(_site.SampleX, _site.SampleY, 111, 220, 192);
		var img = Image.CreateFromData(W, H, false, Image.Format.Rgba8, px);
		img.SavePng(path);
		var counts = new int[Mat.Count]; foreach (var m in mat) counts[m]++;
		GD.Print($"Saved map {W}x{H} after {steps} extra steps: {ProjectSettings.GlobalizePath(path)}");
		GD.Print("Chambers: " + string.Join(", ", _site.Chambers.GroupBy(c => c.Theme).Select(g => $"{g.Key} {g.Count()}")));
		GD.Print("Cells: " + string.Join(", ", Enumerable.Range(1, Mat.Count - 1).Where(m => counts[m] > 0).OrderByDescending(m => counts[m]).Select(m => $"{MaterialTable.Name[m]} {counts[m]}")));
	}

	void SaveShot(string path)
	{
		var img = GetViewport().GetTexture().GetImage();
		img.SavePng(path);
		GD.Print($"Saved screenshot: {ProjectSettings.GlobalizePath(path)}");
	}

	// ---------- Light ----------

	void ComputeLight()
	{
		if (_fullBright) { Array.Fill(_light, .62f); return; }    // about the brightness of rock in your lamp
		Array.Clear(_light);
		var mat = _world.Materials;
		int W = _world.Width;

		// Sky light down to the first solid cell in each column, fading into openings below.
		for (int vx = 0; vx < _vw; vx++)
		{
			int x = _camX + vx;
			float sky = .55f;
			for (int y = Math.Min(0, _camY); y < _camY + _vh; y++)
			{
				if (y < 0) { if (y >= _camY) _light[(y - _camY) * _vw + vx] += sky; continue; }   // open sky above the world
				byte m = mat[y * W + x];
				if (y >= _camY) _light[(y - _camY) * _vw + vx] += sky;
				if (Player.BlocksBody(m) || m == Mat.Water) sky *= .3f;
				else if (y > CaveGen.Surface + 4) sky *= .97f;
				if (sky < .01f) break;
			}
		}

		const int margin = 10;
		for (int y = Math.Max(0, _camY - margin); y < Math.Min(_world.Height, _camY + _vh + margin); y++)
		for (int x = Math.Max(0, _camX - margin); x < Math.Min(W, _camX + _vw + margin); x++)
		{
			float e = MaterialTable.Emission[mat[y * W + x]];
			if (e > 0) Glow(x, y, e * 7, e * .5f);
		}

		// Base lights.
		Glow(GenX, BaseY - 10, 28, .45f);
		foreach (int lx in new[] { -100, -50, 0, 50, 100 }) Glow(_site.BaseX + lx, BaseY - 22, 44, .45f);
		if (_forge.Content != null) Glow(ForgeX, BaseY - 5, 18, _forge.Hot ? .8f : .3f);
		Glow(_p.AnchorX, _p.AnchorY + 2, 16, .3f);    // work light on the winch boom

		if (!_p.CarryingSample) Glow(_site.SampleX, _site.SampleY - 1, 6, .7f);
		foreach (var f in _flares) Cone(f.X, f.Y, 0, MathF.Tau, 34, 1.1f * MathF.Min(1, f.Life / 3), 220);
		foreach (var b in _bits) if (b.Spark) Glow(b.X, b.Y, 3, .5f * b.Life / b.Max);

		if (_inTruck)
		{
			float tx = _truck.X + (_truck.Facing ? Truck.HalfLength : -Truck.HalfLength), ty = _truck.Y - 5;
			Cone(tx, ty, _truck.Facing ? .15f : MathF.PI - .15f, .7f, 60, 1f, 120);
			return;
		}
		float hx = _playerX, hy = _playerY - Player.Height + 1;
		if (_p.LampOn)
		{
			float strength = _p.Battery < 20 ? .6f + .4f * MathF.Sin(_frame * .7f) * (_p.Battery / 20) : 1f;
			// A pool of light all around you (it stops at walls), plus the beam where you aim.
			Cone(hx, hy + 1, 0, MathF.Tau, 18, .7f * strength, 150);
			var (ax, ay) = AimPoint(hx, hy);
			float dir = MathF.Atan2(ay - hy, ax - hx);
			Cone(hx, hy, dir, 1.1f, 52, 1.15f * strength, 170);
		}
		else Cone(hx, hy + 1, 0, MathF.Tau, 7, .3f, 80);    // just enough to see your own feet
	}

	void Glow(float cx, float cy, float r, float s)
	{
		int x0 = (int)(cx - r) - _camX, x1 = (int)(cx + r) - _camX, y0 = (int)(cy - r) - _camY, y1 = (int)(cy + r) - _camY;
		for (int y = Math.Max(0, y0); y <= Math.Min(_vh - 1, y1); y++)
		for (int x = Math.Max(0, x0); x <= Math.Min(_vw - 1, x1); x++)
		{
			float dx = x + _camX - cx, dy = y + _camY - cy, d = MathF.Sqrt(dx * dx + dy * dy);
			if (d < r) _light[y * _vw + x] += s * (1 - d / r);
		}
	}

	/// <summary>
	/// Ray-marched light: stops at the first solid cell, which is lit (you see walls, not through them).
	/// Brightness falls off smoothly with distance and, for beams, towards the edges of the beam.
	/// </summary>
	void Cone(float ox, float oy, float dir, float width, float range, float s, int rays)
	{
		var mat = _world.Materials;
		int W = _world.Width;
		bool full = width >= MathF.Tau - .01f;
		for (int r = 0; r < rays; r++)
		{
			float u = full ? (float)r / rays : (float)r / (rays - 1);
			float a = full ? u * MathF.Tau : dir - width / 2 + width * u;
			float edge = full ? 1 : 1 - MathF.Pow(MathF.Abs(u * 2 - 1), 3) * .85f;
			float dx = MathF.Cos(a), dy = MathF.Sin(a);
			int lastCell = -1;
			for (float t = 0; t < range; t += .5f)
			{
				int x = (int)MathF.Floor(ox + dx * t), y = (int)MathF.Floor(oy + dy * t);
				if (!_world.InBounds(x, y)) break;
				int vx = x - _camX, vy = y - _camY, i = y * W + x;
				if (i != lastCell && (uint)vx < (uint)_vw && (uint)vy < (uint)_vh)
				{
					float fall = 1 - t / range;
					float v = s * edge * fall * fall * (1.3f - .3f * fall);
					ref float l = ref _light[vy * _vw + vx];
					if (l < v) l = v;
				}
				lastCell = i;
				byte m = mat[i];
				if (Player.BlocksBody(m)) break;
				if (m == Mat.Water) t += .5f;
			}
		}
	}

	// ---------- Drawing ----------

	void DrawWorld()
	{
		var mat = _world.Materials; var shade = _world.Shades;
		int W = _world.Width;
		for (int vy = 0; vy < _vh; vy++)
		for (int vx = 0; vx < _vw; vx++)
		{
			int x = _camX + vx, y = _camY + vy, i = y * W + x, o = (vy * _vw + vx) * 4;
			// Open air above ground (inside or above the simulated world) is one smooth dusk gradient, plus any extra
			// light from lamps and fires on top of the sky's own.
			if (y < 0 || (mat[i] == Mat.Empty && y < CaveGen.Surface + 2))
			{
				float t = Math.Clamp((CaveGen.Surface - y) / 160f, 0, 1);
				float extra = _fullBright ? 0 : MathF.Max(0, _light[vy * _vw + vx] - .55f) * 90;
				_px[o] = (byte)Math.Min(255, 46 - 28 * t + extra); _px[o + 1] = (byte)Math.Min(255, 50 - 30 * t + extra * .85f);
				_px[o + 2] = (byte)Math.Min(255, 60 - 30 * t + extra * .6f); _px[o + 3] = 255;
				continue;
			}
			byte m = mat[i];
			uint c = MaterialTable.Color[m];
			float l = MathF.Min(1.2f, _light[vy * _vw + vx] + MaterialTable.Emission[m]);
			float var = .88f + shade[i] / 255f * .24f;
			if (l > .04f) _seen[i] = true;
			float k = l * var * (m == Mat.Empty ? 1f : 1.6f);
			if (l <= .04f && _seen[i] && m != Mat.Empty) k = .07f;    // faint memory of what you have seen
			if (m == Mat.Empty) { c = y < CaveGen.Surface + 2 ? 0x2A2F38u : _fullBright ? 0x1E1914u : 0x2A2219u; }
			if (m == Mat.Ore && (shade[i] & 7) == 0) c = 0xD08850;   // glints in ore
			_px[o] = (byte)Math.Min(255, ((c >> 16) & 255) * k);
			_px[o + 1] = (byte)Math.Min(255, ((c >> 8) & 255) * k);
			_px[o + 2] = (byte)Math.Min(255, (c & 255) * k);
			_px[o + 3] = 255;
		}

		DrawBase();

		// The winch line from the boom on the truck. Unclipped, the hook hangs down until it reaches the ground.
		var (ax0, ay0) = _grappled ? (_hookX, _hookY) : BoomTip();
		if (_hookFlying) { LineA(_playerX, _playerY - Player.Height + 2, _hookX, _hookY, 0xCFC6B3); PutA(_hookX - .5f, _hookY - .5f, 0xE0E4E8, 1.3f); }
		if (_grappled) PutA(_hookX - .5f, _hookY - .5f, 0xE0E4E8, 1.3f);
		var (ex, ey) = _p.Clipped ? (_playerX, _playerY - Player.Height) : (ax0, MathF.Min(ay0 + _p.RopeLen, _truck.GroundAt(ax0) - 1));
		LineA(ax0, ay0, ex, ey, 0xCFC6B3);
		if (!_p.Clipped) PutA(ex - .5f, ey, 0xC96A3B, 1);

		if (!_p.CarryingSample) { Put(_site.SampleX, _site.SampleY - 1, 0x6FDCC0, 1.2f); Put(_site.SampleX + 1, _site.SampleY - 1, 0x6FDCC0, 1.2f); }
		foreach (var f in _flares) { Put((int)f.X, (int)f.Y, 0xFFE0A0, 1.5f); Put((int)f.X + 1, (int)f.Y, 0xFF6040, 1.5f); }

		DrawTruck();
		if (!_inTruck) { DrawPlayer(); DrawReticle(); DrawStamina(); }
		foreach (var dr in _spray)
		{
			uint dc = MaterialTable.Color[dr.M];
			int vx0 = (int)dr.X - _camX, vy0 = (int)dr.Y - _camY;
			float li = MathF.Max(.5f, (uint)vx0 < (uint)_vw && (uint)vy0 < (uint)_vh ? MathF.Min(1.2f, _light[vy0 * _vw + vx0] + .3f + MaterialTable.Emission[dr.M]) : .5f);
			var dcol = new Color(((dc >> 16) & 255) / 255f * li * 1.2f, ((dc >> 8) & 255) / 255f * li * 1.2f, (dc & 255) / 255f * li * 1.2f);
			_actors.Marks.Add((new Rect2((dr.X - _camFX) * CellPx - 1, (dr.Y - _camFY) * CellPx - 1, 3, 3), dcol));
		}
		foreach (var b in _bits)
		{
			float a = MathF.Min(1, b.Life / b.Max * 1.6f);
			int vx = (int)b.X - _camX, vy = (int)b.Y - _camY;
			float light = b.Spark ? 1.4f : ((uint)vx < (uint)_vw && (uint)vy < (uint)_vh ? MathF.Min(1, _light[vy * _vw + vx] + .15f) : 0);
			var col = new Color(((b.Color >> 16) & 255) / 255f * light, ((b.Color >> 8) & 255) / 255f * light, (b.Color & 255) / 255f * light, a);
			_actors.Marks.Add((new Rect2((b.X - _camFX) * CellPx, (b.Y - _camFY) * CellPx, b.Size, b.Size), col));
		}
	}

	void DrawBase()
	{
		int g = BaseY, bx = _site.BaseX;
		// Timber posts, in the background: you walk in front of them.
		foreach (int px in new[] { bx - 104, bx - 30, bx + 30, bx + 100 })
			for (int y = g - 26; y < g; y++) if (_world.Get(px, y) == Mat.Empty) Put(px, y, 0x5A4430, .55f);
		// Work lamps strung along the chamber.
		foreach (int lx in new[] { -100, -50, 0, 50, 100 })
		{
			for (int x = bx + lx - 2; x <= bx + lx + 2; x++) Put(x, g - 23, 0xF0C87A, 1.3f);
			for (int y = g - 27; y < g - 23; y++) Put(bx + lx, y, 0x4A4038, .9f);
		}
		// Supply locker.
		for (int y = g - 11; y < g; y++) for (int x = LockerX - 3; x <= LockerX + 3; x++) Put(x, y, x == LockerX ? 0x3A4248u : 0x56626Au, .95f);
		Put(LockerX - 1, g - 6, 0xC0C8CC, 1.1f); Put(LockerX + 1, g - 6, 0xC0C8CC, 1.1f);
		// Cot.
		for (int x = CotX - 6; x <= CotX + 6; x++) { Put(x, g - 3, 0x6A7A5A, .95f); }
		for (int x = CotX - 6; x <= CotX - 3; x++) Put(x, g - 4, 0xD8D0C0, .95f);
		Put(CotX - 6, g - 2, 0x4A4038, .9f); Put(CotX - 6, g - 1, 0x4A4038, .9f); Put(CotX + 6, g - 2, 0x4A4038, .9f); Put(CotX + 6, g - 1, 0x4A4038, .9f);
		// Map table: a lit survey sheet on legs.
		for (int x = MapX - 6; x <= MapX + 6; x++) { Put(x, g - 6, 0x7A5A3C, .95f); Put(x, g - 7, (x + _frame / 30) % 5 == 0 ? 0x6FDCC0u : 0xC8BC98u, 1f); }
		for (int y = g - 5; y < g; y++) { Put(MapX - 5, y, 0x5A4430, .85f); Put(MapX + 5, y, 0x5A4430, .85f); }
		// Canister rack: one canister per sprayable material; the loaded one is lifted out.
		int perShelf = (SprayList.Length + 1) / 2;
		for (int x = RackX - perShelf / 2 - 1; x <= RackX + perShelf / 2 + 1; x++) { Put(x, g - 5, 0x4A4442, .95f); Put(x, g - 11, 0x4A4442, .95f); }
		for (int y = g - 12; y < g; y++) { Put(RackX - perShelf / 2 - 1, y, 0x4A4442, .95f); Put(RackX + perShelf / 2 + 1, y, 0x4A4442, .95f); }
		for (int k = 0; k < SprayList.Length; k++)
		{
			int shelf = k / perShelf, cx = RackX - perShelf / 2 + k % perShelf, top = shelf == 0 ? g - 14 : g - 8;
			bool on = k == _sprayIndex;
			uint c = MaterialTable.Color[SprayList[k]];
			Put(cx, top + 1 - (on ? 1 : 0), c, on ? 1.4f : 1f); Put(cx, top + 2 - (on ? 1 : 0), c, on ? 1.4f : .85f);
		}

		// Labels over the stations.
		void Label(int x, int y, string t) => _actors.Texts.Add((new Vector2((x - _camFX) * CellPx, (y - _camFY) * CellPx), t, new Color("e4ddcf")));
		Label(GenX, g - 10, "GENERATOR"); Label(bx - 91, g - 12, "SPECIMEN TANKS"); Label(LockerX, g - 13, "LOCKER");
		Label(CotX, g - 7, "COT"); Label(BenchX, g - 9, "WORKBENCH"); Label(MapX, g - 10, "MAP TABLE");
		Label(ForgeX, g - 18, "FORGE"); Label(RackX, g - 15, "CANISTERS"); Label(bx, g - 30, "SHAFT ↑");

		// Generator.
		for (int y = g - 7; y < g; y++) for (int x = GenX - 5; x <= GenX + 5; x++) Put(x, y, 0x8C6A3C, .9f);
		Put(GenX - 2, g - 5, 0x6FDCC0, 1.3f); Put(GenX + 2, g - 5, (_frame / 20) % 2 == 0 ? 0xE3A33Fu : 0x3A3F44u, 1.3f);
		// Bench, with whatever is on it.
		for (int x = BenchX - 7; x <= BenchX + 7; x++) Put(x, g - 5, 0x7A5A3C, .9f);
		Put(BenchX - 6, g - 4, 0x5A4430, .8f); Put(BenchX - 6, g - 3, 0x5A4430, .8f); Put(BenchX - 6, g - 2, 0x5A4430, .8f); Put(BenchX - 6, g - 1, 0x5A4430, .8f);
		Put(BenchX + 6, g - 4, 0x5A4430, .8f); Put(BenchX + 6, g - 3, 0x5A4430, .8f); Put(BenchX + 6, g - 2, 0x5A4430, .8f); Put(BenchX + 6, g - 1, 0x5A4430, .8f);
		for (int k = 0; k < _bench.On.Count; k++)
		{
			uint c = ItemColor(_bench.On[k]);
			Put(BenchX - 4 + k * 3, g - 6, c, 1); Put(BenchX - 3 + k * 3, g - 6, c, 1);
		}
		if (_hammerFlash > 0) for (int k = -1; k <= 1; k++) Put(BenchX + k, g - 7 - _rng.Int(2), 0xFFE0A0, 1.5f);
		// Forge with chimney.
		for (int y = g - 8; y < g; y++) for (int x = ForgeX - 4; x <= ForgeX + 4; x++) Put(x, y, 0x4A4442, .9f);
		for (int y = g - 16; y < g - 8; y++) { Put(ForgeX + 2, y, 0x3A3432, .9f); Put(ForgeX + 3, y, 0x3A3432, .9f); }
		if (_forge.Content != null)
		{
			float heat = _forge.Hot ? 1.4f : .7f;
			uint c = _forge.Done ? ItemColor(_forge.Result ?? Item.Junk) : ItemColor(_forge.Content.Value);
			for (int x = ForgeX - 2; x <= ForgeX + 2; x++) Put(x, g - 4, _forge.Hot ? 0xFF8A3Au : 0x6A4A3Au, heat);
			Put(ForgeX, g - 5, c, heat); Put(ForgeX + 1, g - 5, c, heat);
		}
		else for (int x = ForgeX - 2; x <= ForgeX + 2; x++) Put(x, g - 4, 0x8A3A1A, .9f);
	}

	/// <summary>The survey map: every cell you have seen, at one pixel per cell, with you on it.</summary>
	void BuildMap()
	{
		int W = _world.Width, H = _world.Height;
		if (_mapPx.Length != W * H * 3) _mapPx = new byte[W * H * 3];
		var mat = _world.Materials;
		for (int i = 0; i < W * H; i++)
		{
			uint c = !_seen[i] ? 0x0A0A0Cu : mat[i] == Mat.Empty ? 0x2A241Cu : MaterialTable.Color[mat[i]];
			_mapPx[i * 3] = (byte)(c >> 16); _mapPx[i * 3 + 1] = (byte)(c >> 8); _mapPx[i * 3 + 2] = (byte)c;
		}
		for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++)
		{
			int x = (int)_p.X + dx, y = (int)_p.Y - 3 + dy;
			if ((uint)x < (uint)W && (uint)y < (uint)H) { int o = (y * W + x) * 3; _mapPx[o] = 255; _mapPx[o + 1] = 255; _mapPx[o + 2] = 255; }
		}
		var img = Image.CreateFromData(W, H, false, Image.Format.Rgb8, _mapPx);
		if (_mapTex == null) _mapTex = ImageTexture.CreateFromImage(img); else _mapTex.Update(img);
		_map.Texture = _mapTex;
	}

	// The truck and the player are drawn on a separate layer at sub-cell positions (interpolated between physics
	// ticks), so they glide instead of snapping from cell to cell.

	/// <summary>The winch boom tip, at interpolated draw positions (matches Truck.WinchAnchor).</summary>
	(float x, float y) BoomTip()
	{
		int dir = _truck.Facing ? 1 : -1;
		return (_truckX - dir * (Truck.HalfLength + Truck.BoomReach), (dir > 0 ? _truckRearY : _truckFrontY) - 14);
	}

	void DrawTruck()
	{
		var t = _truck;
		int dir = t.Facing ? 1 : -1;
		float tx = _truckX;
		const int axle = Truck.HalfLength - Truck.WheelInset;
		void Wheel(float cx, float gy) { for (int dy = 1; dy <= 3; dy++) for (int dx = -1; dx <= 1; dx++) PutA(cx + dx - .5f, gy - dy, dx == 0 && dy == 2 ? 0x8A9095u : 0x34393Du, 1); }
		Wheel(tx - axle, _truckRearY); Wheel(tx + axle, _truckFrontY);

		// The body is one rigid piece that tilts with the wheels, instead of every column following the ground.
		float ang = MathF.Atan2(_truckFrontY - _truckRearY, 2 * axle), cy = (_truckRearY + _truckFrontY) / 2;
		for (int s = -Truck.HalfLength; s <= Truck.HalfLength; s++)
		{
			bool cab = s > Truck.HalfLength - 8;
			int top = cab ? 9 : 5;
			for (int h = 0; h < top; h++)
			{
				uint c = cab && h >= 5 && h <= 7 && s < Truck.HalfLength - 1 && s > Truck.HalfLength - 6 ? 0x2F4448u : 0x5F6B58u;
				PutR(tx, cy, s * dir - .5f, -5 - h, ang, c, .95f);
			}
		}
		if (_p.CarryingSample && _inTruck) PutR(tx, cy, -dir * 4 - .5f, -10, ang, 0x6FDCC0, 1.3f);

		// Winch: a mast at the back and a boom reaching out over the edge, with the drum and a work light.
		var (bx, by) = BoomTip();
		float mx = tx - dir * Truck.HalfLength, baseY = (dir > 0 ? _truckRearY : _truckFrontY) - 5;
		for (float y = by; y <= baseY; y += 1) PutA(mx - .5f, y, 0x9AA3A8, .95f);
		for (float x = 0; x <= Truck.BoomReach; x += 1) PutA(mx - dir * x - .5f, by, 0x9AA3A8, .95f);
		PutA(bx - .5f, by, 0xC96A3B, 1.1f);
		PutA(mx + dir * 1 - .5f, baseY - 1, 0x5A4A3A, .95f); PutA(mx + dir * 2 - .5f, baseY - 1, 0x5A4A3A, .95f);
	}

	/// <summary>Queues one cell of a rotated body: (lx, ly) are local cell coordinates around a world origin.</summary>
	void PutR(float ox, float oy, float lx, float ly, float ang, uint c, float k)
	{
		float cos = MathF.Cos(ang), sin = MathF.Sin(ang);
		float wx = ox + (lx + .5f) * cos - (ly + .5f) * sin, wy = oy + (lx + .5f) * sin + (ly + .5f) * cos;
		int vx = (int)MathF.Floor(wx) - _camX, vy = (int)MathF.Floor(wy) - _camY;
		float light = (uint)vx < (uint)_vw && (uint)vy < (uint)_vh ? _light[vy * _vw + vx] : 0;
		float l = MathF.Max(k * .35f, MathF.Min(k, light + .25f));
		var col = new Color(MathF.Min(1, ((c >> 16) & 255) / 255f * l), MathF.Min(1, ((c >> 8) & 255) / 255f * l), MathF.Min(1, (c & 255) / 255f * l));
		_actors.Rot.Add((new Vector2((ox - _camFX) * CellPx, (oy - _camFY) * CellPx), ang, new Rect2(lx * CellPx - .3f, ly * CellPx - .3f, CellPx + .6f, CellPx + .6f), col));
	}

	void LineA(float x0, float y0, float x1, float y1, uint c)
	{
		float d = MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0));
		for (int i = 0; i <= d; i++) { float t = d == 0 ? 0 : i / d; PutA(x0 + (x1 - x0) * t - .5f, y0 + (y1 - y0) * t, c, 1); }
	}

	void DrawPlayer()
	{
		float px = _playerX - .5f, py = _playerY - 1;
		var b = _p.Body;
		int lx = (int)px - _camX, ly = (int)py - _camY;
		float pl = MathF.Max(.5f, MathF.Min(1, _light[Math.Clamp(ly * _vw + lx, 0, _light.Length - 1)] + .3f));
		if (b.Downed)
		{
			for (int t = -3; t <= 3; t++) PutA(px + t, py, 0xC96A3B, pl);
			PutA(px + (_p.Facing ? 4 : -4), py, 0xD9D2C2, pl);
			return;
		}
		PutA(px - 1, py, PartCol(b[Part.LeftLeg]), pl); PutA(px + 1, py, PartCol(b[Part.RightLeg]), pl);
		PutA(px - 1, py - 1, PartCol(b[Part.LeftLeg]), pl); PutA(px + 1, py - 1, PartCol(b[Part.RightLeg]), pl);
		for (int t = 2; t <= 4; t++) for (int s = -1; s <= 1; s++) PutA(px + s, py - t, b[Part.Torso] >= Hurt.Hurt ? 0x9A4A2Au : 0xC96A3Bu, pl);
		if (_p.Clinging)
		{
			int d = _p.ClingDir;
			PutA(px + d * 2, py - 6, PartCol(b[Part.RightArm]), pl); PutA(px + d * 2, py - 5, PartCol(b[Part.LeftArm]), pl);
		}
		else PutA(px + (_p.Facing ? 2 : -2), py - 3, PartCol(_p.Facing ? b[Part.RightArm] : b[Part.LeftArm]), pl);
		if (_pack[_held] > 0) PutA(px + (_p.Facing ? 3 : -3), py - 3, ItemColor(_held), pl + .2f);
		for (int s = -1; s <= 1; s++) { PutA(px + s, py - 5, 0xD9D2C2, pl); PutA(px + s, py - 6, 0xD9D2C2, pl); }
		PutA(px + (_p.Facing ? 1 : -1), py - 5, 0x6FDCC0, 1.2f);
		if (_p.CarryingSample) PutA(px + (_p.Facing ? -2 : 2), py - 3, 0x6FDCC0, 1.2f);
	}

	/// <summary>A thin bar above the head while stamina is being used or recovering.</summary>
	void DrawStamina()
	{
		if (_p.Stamina >= _p.StaminaMax - .5f && !_p.Clinging) return;
		float r = _p.Stamina / _p.StaminaMax;
		var col = r > .5f ? new Color(.44f, .86f, .75f, .9f) : r > .2f ? new Color(.89f, .64f, .25f, .95f) : new Color(.89f, .33f, .25f, 1);
		float x = (_playerX - _camFX) * CellPx - 8, y = (_playerY - Player.Height - 3 - _camFY) * CellPx;
		_actors.Marks.Add((new Rect2(x - 1, y - 1, 18, 4), new Color(0, 0, 0, .5f)));
		_actors.Marks.Add((new Rect2(x, y, 16 * r, 2), col));
	}

	/// <summary>A small corner-bracket cursor where you are aiming: bright when you can dig there, faint when out of reach.</summary>
	void DrawReticle()
	{
		if (_p.Body.Downed) return;
		float hx = _playerX, hy = _playerY - Player.Height + 2;
		var (ax, ay) = AimPoint(hx, hy);
		float reach = MathF.Sqrt((ax - hx) * (ax - hx) + (ay - hy) * (ay - hy));
		byte m = _world.Get((int)ax, (int)ay);
		bool diggable = m is Mat.Rock or Mat.Sand or Mat.Ore or Mat.Mycelium or Mat.Moss;
		var col = reach < 12 ? (diggable ? new Color(.44f, .86f, .75f, .95f) : new Color(.9f, .87f, .81f, .55f)) : new Color(.9f, .87f, .81f, .2f);
		float cx = ((int)ax - _camFX) * CellPx, cy = ((int)ay - _camFY) * CellPx;
		const float r = 6, len = 3, w = 1.5f;
		foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
		{
			float ox = cx + CellPx / 2f + sx * r, oy = cy + CellPx / 2f + sy * r;
			_actors.Marks.Add((new Rect2(sx < 0 ? ox : ox - len, oy - w / 2, len, w), col));
			_actors.Marks.Add((new Rect2(ox - w / 2, sy < 0 ? oy : oy - len, w, len), col));
		}
	}

	/// <summary>Queues one cell-sized square on the actor layer at a fractional world position.</summary>
	void PutA(float x, float y, uint c, float k)
	{
		int vx = (int)MathF.Floor(x) - _camX, vy = (int)MathF.Floor(y) - _camY;
		float light = (uint)vx < (uint)_vw && (uint)vy < (uint)_vh ? _light[vy * _vw + vx] : 0;
		float l = MathF.Max(k * .35f, MathF.Min(k, light + .25f));
		var col = new Color(MathF.Min(1, ((c >> 16) & 255) / 255f * l), MathF.Min(1, ((c >> 8) & 255) / 255f * l), MathF.Min(1, (c & 255) / 255f * l));
		_actors.Cells.Add((new Vector2((x - _camFX) * CellPx, (y - _camFY) * CellPx), col));
	}

	static uint ItemColor(Item i) => i switch
	{
		Item.Stone => 0x7A746E, Item.Sand => 0xC4A46A, Item.Ore => 0xB06A40, Item.Fibre => 0xCCC2AC, Item.Metal => 0xB8C0C8,
		Item.Glass => 0x9FD8E8, Item.Rope => 0xD8C890, Item.Pick => 0x9AA3A8, Item.Flare => 0xFF6040,
		Item.Coal => 0x2A2828, Item.Salt => 0xE0DAD2, Item.Sulfur => 0xD0BC40, Item.Crystal => 0x9FE0D8, Item.Clay => 0x8A6A52, _ => 0x4A4038,
	};

	static uint PartCol(Hurt h) => h switch { Hurt.Fine => 0x3A3F44, Hurt.Hurt => 0x8A5A30, _ => 0xA03028 };

	void Put(int x, int y, uint c, float k)
	{
		int vx = x - _camX, vy = y - _camY;
		if ((uint)vx >= (uint)_vw || (uint)vy >= (uint)_vh) return;
		float l = MathF.Max(k * .35f, MathF.Min(k, _light[vy * _vw + vx] + .25f));
		int o = (vy * _vw + vx) * 4;
		_px[o] = (byte)Math.Min(255, ((c >> 16) & 255) * l);
		_px[o + 1] = (byte)Math.Min(255, ((c >> 8) & 255) * l);
		_px[o + 2] = (byte)Math.Min(255, (c & 255) * l);
	}

	void Line(float x0, float y0, float x1, float y1, uint c)
	{
		float d = MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0));
		for (int s = 0; s <= d; s++)
		{
			float t = d == 0 ? 0 : s / d;
			Put((int)(x0 + (x1 - x0) * t), (int)(y0 + (y1 - y0) * t), c, 1);
		}
	}

	// ---------- HUD ----------

	void UpdateHud()
	{
		int depth = Math.Max(0, (int)((_p.Y - CaveGen.Surface) * .3f));
		int bdx = (int)((_site.BaseX - _p.X) * .3f), bdy = (int)((BaseY - _p.Y) * .3f);
		string where = Math.Abs(bdx) < 25 && Math.Abs(bdy) < 8 ? "at the base"
			: $"base {Math.Abs(bdx)} m {(bdx > 0 ? "east" : "west")}, {Math.Abs(bdy)} m {(bdy < 0 ? "up" : "down")}";
		string gas = _p.InGas ? (_frame / 6 % 2 == 0 ? "  SENSOR ●" : "  SENSOR ○") : "";
		string line1 = _inTruck
			? $"driving · {where} · fuel {_truck.Fuel:F0} · speed {MathF.Abs(_truck.Speed) * .3f * 3.6f:F0} km/h"
			: $"{where} · depth {depth} m · line {(_p.Clipped ? $"{_p.RopeLen * .3f:F0}/{_p.RopeMax * .3f:F0} m" : "unclipped")}{(_p.AirEnabled ? $" · air {_p.Air:F0}s" : "")} · lamp {(_p.LampOn ? $"{_p.Battery:F0}s" : "off")}{gas}";
		string pack = string.Join("  ", Selectable.Select((it, k) => $"{(it == _held ? "[" : "")}{k + 1} {ItemName(it)} {_pack[it]}{(it == _held ? "]" : "")}"));
		var minerals = new[] { Item.Coal, Item.Salt, Item.Sulfur, Item.Crystal, Item.Clay }.Where(i => _pack[i] > 0).Select(i => $"{ItemName(i)} {_pack[i]}");
		var (aimX, aimY) = AimPoint(_playerX, _playerY - Player.Height + 1);
		byte aimM = _world.Get((int)MathF.Floor(aimX), (int)MathF.Floor(aimY));
		string looking = aimM == Mat.Empty ? "" : $"  ·  aiming at {MaterialTable.Defs[aimM].Name}";
		string tools = $"flares {_pack[Item.Flare]}{(_pack[Item.Pick] > 0 ? " · pick" : "")} · pack {_pack.BulkUsed}/{_pack.Capacity}{(minerals.Any() ? " · " + string.Join("  ", minerals) : "")}";
		var sd = MaterialTable.Defs[SprayMat];
		string spray = $"SPRAY ▸ {sd.Name}  ({sd.Phase.ToString().ToLowerInvariant()}{(sd.Strange ? ", strange" : "")})  ·  {_sprayIndex + 1}/{SprayList.Length}";
		_hud.Text = $"{line1}\n{spray}{looking}\n{tools}\nKeys: A/D move · Space jump · W/S climb or reel · LMB dig · RMB spray · wheel or Z/X spray material · G or MMB grapple · hold H rescue beacon" +
			"\n      E use · C clip · Q flare · F lamp · L lights · N new cave · R restart · P pause" +
			"\nPad: stick move · right stick aim · A jump · RT dig · LT spray · LB grapple · d-pad ←/→ spray material · hold d-pad ↓ beacon" +
			"\n      X use · B clip · Y lamp · R3 flare · Start pause · Back restart" +(_paused ? "\nPAUSED" : "");
		_hint.Text = Hint();
		_msg.Text = _p.Body.Downed && !_p.Clipped && !_won ? "Downed and off the line. R to restart." : _p.Message;
	}

	string Hint()
	{
		if (_inTruck)
		{
			float off = MathF.Abs(_p.AnchorX - _site.WinchX);
			if (off < 40) return off < CaveGen.ShaftHalfWidth - 1 ? "the winch is over the shaft · E: get out" : "park with the winch boom over the shaft · W/S: creep";
			return MathF.Abs(_truck.Speed) < 3 ? "E: get out" : "";
		}
		if (!_p.CarryingSample && MathF.Abs(_site.SampleX - _p.X) < 5 && MathF.Abs(_site.SampleY - _p.Y) < 9) return "E: take the sample";
		if (NearSurface(ForgeX, 7))
			return _forge.Content == null ? $"E: put {ItemName(_held)} in the forge" : _forge.Done ? (_forge.Hot ? "E: take it out (it's glowing)" : "E: take it out") : "heating…";
		if (NearSurface(BenchX, 8))
			return _bench.On.Count > 0 ? $"LMB: strike ({_bench.Strikes}/{Bench.StrikesNeeded}) · E: lay {ItemName(_held)} on the bench" : $"E: lay {ItemName(_held)} on the bench";
		if (_p.Clinging) return "W/S: climb · jump: kick off · push away: let go";
		if (_beacon > 0) return $"rescue beacon… {(int)(100 * _beacon / BeaconTime)}%";
		if (_grappled) return "W/S: climb / lower on the hook line · A/D: swing · jump or G: let go";
		if (_p.Clipped) return "W/S: reel in / out · C: unclip";
		if (_held == Item.Rope && _pack[Item.Rope] > 0 && MathF.Abs(_p.X - _p.AnchorX) < 12 && MathF.Abs(_p.Y - _truck.Y) < 10) return "E: splice rope onto the winch line";
		if (_p.DistanceToLine() < _p.ClipReach) return "C: clip onto the winch line";
		if (MathF.Abs(_truck.X - _p.X) < Truck.HalfLength + 4 && MathF.Abs(_truck.Y - _p.Y) < 8) return "E: drive";
		if (MathF.Abs(GenX - _p.X) < 24) return "the generator charges your lamp";
		if (NearSurface(LockerX, 6)) return "E: take flares and rope";
		if (NearSurface(CotX, 8)) return "E: rest (heals you, charges the lamp)";
		if (NearSurface(MapX, 8)) return _mapOpen ? "E: put the map down" : "E: look at the survey map";
		if (NearSurface(RackX, 10)) return $"E: swap canister (now {MaterialTable.Defs[SprayMat].Name})";
		if (MathF.Abs(_site.BaseX - 91 - _p.X) < 10 && MathF.Abs(_p.Y - BaseY) < 12) return "specimen tanks: spray things in and see what happens";
		return "";
	}
}

/// <summary>Body silhouette: grey is fine, amber is hurt, red is useless.</summary>
public partial class BodyPanel : Control
{
	public Body? Body;

	public override void _Draw()
	{
		if (Body == null) return;
		Color C(Part p) => Body[p] switch { Hurt.Fine => new Color("5b6368"), Hurt.Hurt => new Color("e3a33f"), _ => new Color("e2553f") };
		DrawRect(new Rect2(0, 0, 76, 120), new Color(0, 0, 0, .45f));
		DrawRect(new Rect2(28, 8, 20, 18), C(Part.Head));
		DrawRect(new Rect2(24, 30, 28, 36), C(Part.Torso));
		DrawRect(new Rect2(12, 30, 9, 34), C(Part.LeftArm));
		DrawRect(new Rect2(55, 30, 9, 34), C(Part.RightArm));
		DrawRect(new Rect2(25, 70, 11, 40), C(Part.LeftLeg));
		DrawRect(new Rect2(40, 70, 11, 40), C(Part.RightLeg));
		if (Body.Downed) DrawString(ThemeDB.FallbackFont, new Vector2(10, 118), "DOWNED", HorizontalAlignment.Left, -1, 12, new Color("e2553f"));
	}
}

/// <summary>Draws moving things (player, truck) as cell-sized squares at fractional positions above the world image.</summary>
public partial class ActorLayer : Node2D
{
	public float Size = 4;
	public readonly List<(Vector2 pos, Color col)> Cells = [];
	public readonly List<(Rect2 rect, Color col)> Marks = [];
	public readonly List<(Vector2 origin, float angle, Rect2 rect, Color col)> Rot = [];
	public readonly List<(Vector2 pos, string text, Color col)> Texts = [];

	public override void _Draw()
	{
		var s = new Vector2(Size, Size);
		foreach (var (o, a, r, c) in Rot) { DrawSetTransform(o, a, Vector2.One); DrawRect(r, c); }
		DrawSetTransform(Vector2.Zero, 0, Vector2.One);
		foreach (var (p, c) in Cells) DrawRect(new Rect2(p, s), c);
		foreach (var (r, c) in Marks) DrawRect(r, c);
		var font = ThemeDB.FallbackFont;
		foreach (var (p, t, c) in Texts)
		{
			float w = font.GetStringSize(t, HorizontalAlignment.Left, -1, 13).X;
			DrawString(font, p + new Vector2(-w / 2 + 1, 1), t, HorizontalAlignment.Left, -1, 13, new Color(0, 0, 0, .7f));
			DrawString(font, p + new Vector2(-w / 2, 0), t, HorizontalAlignment.Left, -1, 13, c);
		}
	}
}
