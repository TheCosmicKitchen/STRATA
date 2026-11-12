using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Strata.Rules;
using Strata.Sim;

namespace Strata.Game;

/// <summary>
/// The materials sandbox: paint any material into a small world and watch it interact.
/// Run: godot --path game sandbox.tscn
///
/// Mouse: left = use tool, right = erase, wheel = brush size, click a swatch to pick a material.
/// Keys: 1 paint · 2 erase · 3 ignite · 4 box fill · Q/E previous/next material · Space pause · . step one tick ·
///       F1–F5 speed · L cave lighting · C clear · H help.
/// Scenarios (right panel): an empty box, the reaction gallery, any single reaction, or a real chamber from the
/// cave generator.
/// Gallery screenshots: godot --path game sandbox.tscn -- --gallery=&lt;folder&gt; [--page=N] (saves at 0.5, 3, 10, 30 s, then quits).
/// </summary>
public partial class Sandbox : Node2D
{
	public const int CellPx = 3, CW = 320, CH = 192, PanelX = CW * CellPx + 12;
	enum Tool { Paint, Erase, Ignite, Box }

	World _w = null!;
	Image _img = null!; ImageTexture _tex = null!; byte[] _px = [];
	Sprite2D _sprite = null!;
	public byte Selected = Mat.Sand;
	Tool _tool = Tool.Paint;
	int _brush = 3, _speed = 1;
	bool _paused, _caveLight, _stepOnce;
	Vector2I? _boxStart;
	double _stepMs;

	Label _inspector = null!, _status = null!, _help = null!, _title = null!;
	PaletteView _palette = null!;
	OptionButton _scenario = null!, _reaction = null!;
	readonly List<Button> _toolButtons = [];
	Node2D _labels = null!;
	readonly HashSet<Key> _down = [];

	string? _galleryDir, _autoShot; float _t; int _autoScenario = -1, _autoReaction = -1;
	static World? _caveSource; static Underground.Site? _caveSite;

	public override void _Ready()
	{
		foreach (var a in OS.GetCmdlineUserArgs())
		{
			if (a.StartsWith("--gallery=")) _galleryDir = a[10..];
			else if (a.StartsWith("--page=")) _galleryPage = int.Parse(a[7..]);
			else if (a.StartsWith("--scenario=")) _autoScenario = int.Parse(a[11..]);
			else if (a.StartsWith("--reaction=")) _autoReaction = int.Parse(a[11..]);
			else if (a.StartsWith("--shot=")) _autoShot = a[7..];
			else if (a == "--cave-light") _caveLight = true;
		}
		RenderingServer.SetDefaultClearColor(new Color("0c0f11"));
		_px = new byte[CW * CH * 4];
		_img = Image.CreateEmpty(CW, CH, false, Image.Format.Rgba8);
		_tex = ImageTexture.CreateFromImage(_img);
		_sprite = new Sprite2D { Texture = _tex, Centered = false, Scale = new Vector2(CellPx, CellPx), TextureFilter = TextureFilterEnum.Nearest };
		AddChild(_sprite);
		_labels = new Node2D(); AddChild(_labels);
		BuildUi();
		EmptyBox();
		if (_galleryDir != null) { Directory.CreateDirectory(_galleryDir); Gallery(_galleryPage); }
		if (_autoScenario >= 0) LoadScenario(_autoScenario);
		if (_autoReaction >= 0) OneReaction(Reactions.All[_autoReaction]);
	}

	// ---------- UI ----------

	void BuildUi()
	{
		var ui = new CanvasLayer(); AddChild(ui);
		var bg = new ColorRect { Color = new Color("13171a"), Position = new Vector2(PanelX - 6, 0), Size = new Vector2(1280 - PanelX + 6, 720) };
		ui.AddChild(bg);
		var col = new VBoxContainer { Position = new Vector2(PanelX, 8), Size = new Vector2(1280 - PanelX - 10, 700) };
		col.AddThemeConstantOverride("separation", 6);
		ui.AddChild(col);

		_title = Lbl(col, "MATERIALS SANDBOX", 15, "c96a3b");

		_palette = new PaletteView(this) { CustomMinimumSize = new Vector2(1280 - PanelX - 10, 330) };
		col.AddChild(_palette);

		var tools = new HBoxContainer(); col.AddChild(tools);
		foreach (var (t, name) in new[] { (Tool.Paint, "1 Paint"), (Tool.Erase, "2 Erase"), (Tool.Ignite, "3 Ignite"), (Tool.Box, "4 Box") })
		{
			var b = new Button { Text = name, ToggleMode = true, FocusMode = Control.FocusModeEnum.None };
			var tt = t; b.Pressed += () => SetTool(tt);
			b.AddThemeFontSizeOverride("font_size", 12);
			tools.AddChild(b); _toolButtons.Add(b);
		}
		SetTool(Tool.Paint);

		_scenario = new OptionButton { FocusMode = Control.FocusModeEnum.None };
		_scenario.AddThemeFontSizeOverride("font_size", 12);
		_scenario.AddItem("Scenario: empty box");
		for (int p = 0; p < GalleryPages; p++) _scenario.AddItem($"Scenario: reaction gallery, page {p + 1} of {GalleryPages}");
		foreach (var th in Enum.GetValues<Underground.Theme>()) if (th != Underground.Theme.Plain) _scenario.AddItem($"Cave chamber: {ThemeName(th)}");
		_scenario.ItemSelected += i => LoadScenario((int)i);
		col.AddChild(_scenario);

		_reaction = new OptionButton { FocusMode = Control.FocusModeEnum.None };
		_reaction.AddThemeFontSizeOverride("font_size", 12);
		_reaction.AddItem("Try one reaction…");
		foreach (var r in Reactions.All) _reaction.AddItem($"{MaterialTable.Name[r.A]} + {MaterialTable.Name[r.B]}");
		_reaction.ItemSelected += i => { if (i > 0) OneReaction(Reactions.All[i - 1]); _reaction.Select(0); };
		col.AddChild(_reaction);

		_inspector = Lbl(col, "", 12, "e4ddcf");
		_inspector.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_inspector.CustomMinimumSize = new Vector2(1280 - PanelX - 10, 170);

		_status = new Label { Position = new Vector2(8, CH * CellPx + 6) };
		_status.AddThemeFontSizeOverride("font_size", 13);
		_status.AddThemeColorOverride("font_color", new Color("8e958f"));
		ui.AddChild(_status);

		_help = new Label { Position = new Vector2(40, 40), Visible = false, Text =
			"MATERIALS SANDBOX\n\n" +
			"Left mouse       use the tool (paint, erase, ignite, or drag a box)\n" +
			"Right mouse      erase\n" +
			"Mouse wheel      brush size\n" +
			"Click a swatch   choose a material (or Q / E)\n" +
			"1 2 3 4          paint, erase, ignite, box fill\n" +
			"Space / .        pause / step one tick\n" +
			"F1 – F5          simulation speed\n" +
			"L                cave lighting (dark, lit only by glowing things and your cursor lamp)\n" +
			"C                clear to an empty box\n" +
			"H                this help\n\n" +
			"Paint only fills empty cells; erase or box over things to replace them.\n" +
			"The inspector shows what's under the cursor and everything it reacts with." };
		_help.AddThemeFontSizeOverride("font_size", 16);
		_help.AddThemeColorOverride("font_color", new Color("e4ddcf"));
		_help.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 1));
		_help.AddThemeConstantOverride("shadow_outline_size", 8);
		ui.AddChild(_help);
	}

	static Label Lbl(Node parent, string text, int size, string color)
	{
		var l = new Label { Text = text };
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_color", new Color(color));
		parent.AddChild(l);
		return l;
	}

	void SetTool(Tool t)
	{
		_tool = t; _boxStart = null;
		for (int i = 0; i < _toolButtons.Count; i++) _toolButtons[i].ButtonPressed = i == (int)t;
	}

	public void Select(byte m) { Selected = m; if (_tool == Tool.Erase || _tool == Tool.Ignite) SetTool(Tool.Paint); }

	static string ThemeName(Underground.Theme t) => t switch
	{
		Underground.Theme.Grotto => "moss grotto", Underground.Theme.GlowHall => "glow-worm hall", Underground.Theme.SaltCave => "salt cave",
		Underground.Theme.IcePocket => "ice pocket", Underground.Theme.Geode => "crystal geode", Underground.Theme.RootHall => "root hall",
		Underground.Theme.CoalGallery => "coal gallery (gas!)", Underground.Theme.FungusForest => "grey-lace forest", Underground.Theme.ShaleHall => "shale hall",
		Underground.Theme.OreChamber => "ore chamber", Underground.Theme.OilSeep => "oil seep", Underground.Theme.LavaTube => "lava tube",
		Underground.Theme.SulfurVent => "sulfur vent", Underground.Theme.Quicksilver => "quicksilver pool", Underground.Theme.HydrogenPocket => "hydrogen pocket",
		Underground.Theme.FrostHollow => "frost hollow (cryo)", Underground.Theme.SkydustGrotto => "skydust grotto", Underground.Theme.GhostSpring => "ghostwater spring",
		Underground.Theme.SunstoneHearth => "sunstone hearth", Underground.Theme.MiteNest => "mite nest", Underground.Theme.VoidScar => "void scar", Underground.Theme.ResoniteVein => "resonite vein", _ => t.ToString(),
	};

	// ---------- Input and stepping ----------

	bool Pressed(Key k) { bool now = Input.IsPhysicalKeyPressed(k), was = _down.Contains(k); if (now) _down.Add(k); else _down.Remove(k); return now && !was; }

	public override void _UnhandledInput(InputEvent e)
	{
		if (e is InputEventMouseButton mb && mb.Pressed && InCanvas(mb.Position))
		{
			if (mb.ButtonIndex == MouseButton.WheelUp) _brush = Math.Min(16, _brush + 1);
			if (mb.ButtonIndex == MouseButton.WheelDown) _brush = Math.Max(0, _brush - 1);
			if (mb.ButtonIndex == MouseButton.Left && _tool == Tool.Box) _boxStart = Cell(mb.Position);
		}
		if (e is InputEventMouseButton up && !up.Pressed && up.ButtonIndex == MouseButton.Left && _tool == Tool.Box && _boxStart is Vector2I s)
		{
			var c = Cell(up.Position);
			for (int y = Math.Min(s.Y, c.Y); y <= Math.Max(s.Y, c.Y); y++)
				for (int x = Math.Min(s.X, c.X); x <= Math.Max(s.X, c.X); x++)
					if (_w.Get(x, y) != Mat.Bedrock || Selected == Mat.Bedrock) _w.Set(x, y, Selected, World.DefaultLife(Selected));
			_boxStart = null;
		}
	}

	static bool InCanvas(Vector2 p) => p.X >= 0 && p.Y >= 0 && p.X < CW * CellPx && p.Y < CH * CellPx;
	static Vector2I Cell(Vector2 p) => new((int)(p.X / CellPx), (int)(p.Y / CellPx));

	public override void _PhysicsProcess(double delta)
	{
		if (Pressed(Key.Key1)) SetTool(Tool.Paint);
		if (Pressed(Key.Key2)) SetTool(Tool.Erase);
		if (Pressed(Key.Key3)) SetTool(Tool.Ignite);
		if (Pressed(Key.Key4)) SetTool(Tool.Box);
		if (Pressed(Key.Q)) Selected = (byte)(Selected <= 1 ? Mat.Count - 1 : Selected - 1);
		if (Pressed(Key.E)) Selected = (byte)(Selected >= Mat.Count - 1 ? 1 : Selected + 1);
		if (Pressed(Key.Space)) _paused = !_paused;
		if (Pressed(Key.Period)) _stepOnce = true;
		if (Pressed(Key.L)) _caveLight = !_caveLight;
		if (Pressed(Key.C)) EmptyBox();
		if (Pressed(Key.H)) _help.Visible = !_help.Visible;
		for (int k = 0; k < 5; k++) if (Pressed(Key.F1 + k)) _speed = k + 1;

		var mp = GetViewport().GetMousePosition();
		if (InCanvas(mp))
		{
			var c = Cell(mp);
			bool lmb = Input.IsMouseButtonPressed(MouseButton.Left), rmb = Input.IsMouseButtonPressed(MouseButton.Right);
			if (rmb || (lmb && _tool == Tool.Erase)) Brush(c, Mat.Empty, replace: true);
			else if (lmb && _tool == Tool.Paint) Brush(c, Selected, replace: false);
			else if (lmb && _tool == Tool.Ignite) Brush(c, Mat.Fire, replace: false, chance: .25f);
		}

		var sw = System.Diagnostics.Stopwatch.StartNew();
		int steps = _paused ? (_stepOnce ? 1 : 0) : _speed;
		for (int s = 0; s < steps; s++) _w.Step();
		if (steps > 0) _stepMs = sw.Elapsed.TotalMilliseconds / steps;
		_stepOnce = false;

		if (_autoShot != null)
		{
			_t += (float)delta;
			if (_t >= 4 && _t - (float)delta < 4) CallDeferred(nameof(Shot), _autoShot);
			if (_t > 4.5f) GetTree().Quit();
		}
		if (_galleryDir != null)
		{
			_t += (float)delta;
			foreach (var at in new[] { .5f, 3f, 10f, 30f })
				if (_t - (float)delta < at && _t >= at) CallDeferred(nameof(Shot), Path.Combine(_galleryDir, $"gallery-{at:00.0}s.png"));
			if (_t > 30.5f) GetTree().Quit();
		}
	}

	Rng _rng = new(42);
	void Brush(Vector2I c, byte m, bool replace, float chance = 1)
	{
		for (int y = c.Y - _brush; y <= c.Y + _brush; y++)
		for (int x = c.X - _brush; x <= c.X + _brush; x++)
		{
			int dx = x - c.X, dy = y - c.Y;
			if (dx * dx + dy * dy > _brush * _brush || !_w.InBounds(x, y)) continue;
			byte cur = _w.Get(x, y);
			if (cur == Mat.Bedrock && m != Mat.Bedrock && !(replace && x > 1 && y > 1 && x < CW - 2 && y < CH - 2)) continue;
			if (!replace && cur != Mat.Empty) continue;
			if (chance < 1 && !_rng.Chance(chance)) continue;
			_w.Set(x, y, m, World.DefaultLife(m));
		}
	}

	void Shot(string path) { GetViewport().GetTexture().GetImage().SavePng(path); GD.Print($"saved {path}"); }

	// ---------- Drawing ----------

	public override void _Process(double delta)
	{
		var mat = _w.Materials; var shade = _w.Shades;
		var mp = GetViewport().GetMousePosition();
		var cur = Cell(mp);
		bool inCanvas = InCanvas(mp);
		for (int y = 0; y < CH; y++) for (int x = 0; x < CW; x++)
		{
			int i = y * _w.Width + x, o = (y * CW + x) * 4;
			byte m = mat[i]; uint c = m == Mat.Empty ? 0x121214u : MaterialTable.Color[m];
			float k = m == Mat.Empty ? 1 : .85f + shade[i] / 255f * .3f;
			if (MaterialTable.Gas[m] && m != Mat.Empty) k *= .9f + .2f * MathF.Sin(x * .7f + y * .5f + _w.Tick * .05f);
			if (_caveLight)
			{
				float l = MaterialTable.Emission[m] * 1.2f;
				if (inCanvas) { float d = MathF.Sqrt((x - cur.X) * (x - cur.X) + (y - cur.Y) * (y - cur.Y)); l = MathF.Max(l, MathF.Max(0, 1 - d / 40f) * 1.1f); }
				k *= MathF.Min(1.1f, l + .03f);
			}
			else k *= 1.2f + MaterialTable.Emission[m] * .3f;
			_px[o] = (byte)Math.Min(255, ((c >> 16) & 255) * k);
			_px[o + 1] = (byte)Math.Min(255, ((c >> 8) & 255) * k);
			_px[o + 2] = (byte)Math.Min(255, (c & 255) * k);
			_px[o + 3] = 255;
		}
		// Brush outline and box preview.
		if (inCanvas)
		{
			if (_tool == Tool.Box && _boxStart is Vector2I s)
				for (int y = Math.Min(s.Y, cur.Y); y <= Math.Max(s.Y, cur.Y); y++)
				for (int x = Math.Min(s.X, cur.X); x <= Math.Max(s.X, cur.X); x++)
					if (x == s.X || x == cur.X || y == s.Y || y == cur.Y) Tint(x, y);
			if (_tool != Tool.Box)
				for (int a = 0; a < 48; a++) Tint(cur.X + (int)MathF.Round(MathF.Cos(a * MathF.Tau / 48) * _brush), cur.Y + (int)MathF.Round(MathF.Sin(a * MathF.Tau / 48) * _brush));
		}
		_img.SetData(CW, CH, false, Image.Format.Rgba8, _px);
		_tex.Update(_img);
		_labels.Visible = !_caveLight;

		Inspect(inCanvas ? cur : (Vector2I?)null);
		var counts = new int[Mat.Count]; foreach (var m in mat) counts[m]++;
		_status.Text = $"{(_paused ? "PAUSED" : $"speed {_speed}×")} · tick {_w.Tick} · {_stepMs:F2} ms/step · {_w.AwakeChunksLastStep}/{_w.ChunksX * _w.ChunksY} chunks awake · " +
			$"{_w.CellsVisitedLastStep} cells visited · tool {_tool} · brush {_brush} · {MaterialTable.Name[Selected]}: {counts[Selected]} cells · L cave light {(_caveLight ? "on" : "off")} · H help";
		_palette.QueueRedraw();
	}

	void Tint(int x, int y)
	{
		if (x < 0 || y < 0 || x >= CW || y >= CH) return;
		int o = (y * CW + x) * 4;
		_px[o] = (byte)Math.Min(255, _px[o] / 2 + 120); _px[o + 1] = (byte)Math.Min(255, _px[o + 1] / 2 + 110); _px[o + 2] = (byte)Math.Min(255, _px[o + 2] / 2 + 96);
	}

	void Inspect(Vector2I? at)
	{
		byte m = at is Vector2I c ? _w.Get(c.X, c.Y) : Selected;
		var d = MaterialTable.Defs[m];
		var lines = new List<string>
		{
			$"{(at != null ? "UNDER CURSOR" : "SELECTED")}: {d.Name.ToUpperInvariant()}  ({d.Phase.ToString().ToLowerInvariant()}{(at is Vector2I cc && _w.LifeAt(cc.X, cc.Y) > 0 ? $", life {_w.LifeAt(cc.X, cc.Y)}" : "")})",
			d.Notes,
		};
		var props = new List<string>();
		if (d.Phase != Phase.Solid) props.Add($"density {d.Density:0.#}");
		if (d.Flammability > 0) props.Add(d.BurnsInto == Mat.Blast ? "explodes when lit" : $"burns → {MaterialTable.Name[d.BurnsInto]}{(d.BurnByproduct != 0 ? " + " + MaterialTable.Name[d.BurnByproduct] : "")}");
		if (d.Emission > 0) props.Add("glows");
		if (d.Grippable) props.Add("climbable");
		if (d.Suffocation > 0) props.Add($"breathing it: air ×{d.Suffocation:0}");
		if (d.Hot) props.Add("hot");
		if (d.DecayChance > 0) props.Add($"fades → {MaterialTable.Name[d.DecayInto]}");
		if (props.Count > 0) lines.Add(string.Join(" · ", props));
		var rx = Reactions.All.Where(r => r.A == m || r.B == m).Take(9).ToList();
		if (rx.Count > 0)
		{
			lines.Add("");
			lines.Add("REACTS WITH");
			foreach (var r in rx)
			{
				bool a = r.A == m;
				byte other = a ? r.B : r.A, self = a ? r.AInto : r.BInto, them = a ? r.BInto : r.AInto;
				string What(byte into, byte orig) => into == Reaction.Keep ? "stays" : MaterialTable.Name[into];
				lines.Add($"· {MaterialTable.Name[other]}: it → {What(self, m)}, {MaterialTable.Name[other]} → {What(them, other)}");
			}
		}
		_inspector.Text = string.Join("\n", lines);
	}

	// ---------- Scenarios ----------

	void ClearLabels() { foreach (var n in _labels.GetChildren()) n.QueueFree(); }

	void EmptyBox()
	{
		_w = new World(5, 3, 7);
		for (int y = 0; y < _w.Height; y++) for (int x = 0; x < _w.Width; x++)
			_w.Set(x, y, x < 2 || x >= CW - 2 || y < 2 || y >= CH - 2 ? Mat.Bedrock : Mat.Empty);
		ClearLabels();
	}

	void LoadScenario(int index)
	{
		if (index == 0) EmptyBox();
		else if (index <= GalleryPages) Gallery(index - 1);
		else
		{
			var themes = Enum.GetValues<Underground.Theme>().Where(t => t != Underground.Theme.Plain).ToArray();
			CaveChamber(themes[index - 1 - GalleryPages]);
		}
	}

	void OneReaction(Reaction r)
	{
		EmptyBox();
		Fill(60, 70, 100, 100, r.A);
		Fill(160, 70, 100, 100, r.B);
		AddLabel($"{MaterialTable.Name[r.A]} + {MaterialTable.Name[r.B]}: {r.Why}", 60, 58, 13);
	}

	/// <summary>Copies a real chamber of the given theme out of a generated cave into the sandbox.</summary>
	void CaveChamber(Underground.Theme theme)
	{
		if (_caveSource == null)
		{
			_caveSource = new World(32, 10, 5);
			_caveSite = Underground.Generate(_caveSource, 5, settleSteps: 60);
		}
		var ch = _caveSite!.Chambers.Where(c => c.Theme == theme).OrderByDescending(c => c.RX * c.RY).FirstOrDefault();
		EmptyBox();
		if (ch == null) { AddLabel($"No {ThemeName(theme)} in this cave", 20, 20, 14); return; }
		int x0 = ch.X - CW / 2, y0 = ch.Y - CH / 2;
		for (int y = 2; y < CH - 2; y++) for (int x = 2; x < CW - 2; x++)
		{
			byte m = _caveSource.Get(x0 + x, y0 + y);
			_w.Set(x, y, m, Math.Max(_caveSource.LifeAt(x0 + x, y0 + y), World.DefaultLife(m)));
		}
		AddLabel($"{ThemeName(theme)}  (from the cave generator, depth {(ch.Y - Underground.Surface) * .3f:F0} m)", 6, 4, 13);
	}

	const int GalleryPages = 2, PerPage = 70;
	int _galleryPage;

	void Gallery(int page)
	{
		EmptyBox();
		var cases = new List<(string label, Action<int, int, int, int> build)>();
		foreach (var r in Reactions.All) { var rr = r; cases.Add(($"{Short(rr.A)}+{Short(rr.B)}", (x0, y0, w, h) => { Fill(x0, y0, w / 2, h, rr.A); Fill(x0 + w / 2, y0, w - w / 2, h, rr.B); })); }
		for (byte m = 1; m < Mat.Count; m++)
		{
			if (MaterialTable.Flammability[m] <= 0) continue;
			byte mm = m;
			cases.Add(($"{Short(mm)} on lava", (x0, y0, w, h) => { Fill(x0, y0 + h - 4, w, 4, Mat.Lava); Fill(x0 + 2, y0 + h - 10, w - 4, 6, mm); }));
		}
		cases.Add(("shale roof cut", (x0, y0, w, h) => { Fill(x0, y0, w, 6, Mat.Shale); Fill(x0 + w / 2 - 1, y0 + 3, 3, 3, Mat.Empty); }));
		cases.Add(("spark in methane", (x0, y0, w, h) => { Fill(x0, y0, w, h - 3, Mat.Methane); Fill(x0 + w / 2, y0 + h - 3, 1, 1, Mat.Fire); }));
		cases.Add(("hydrogen pocket", (x0, y0, w, h) => { Fill(x0, y0, w, 8, Mat.Shale); Fill(x0 + 2, y0 + 2, w - 4, 5, Mat.Hydrogen); Fill(x0 + w / 2, y0 + 6, 1, 1, Mat.Fire); }));
		cases.Add(("coal dust + ember", (x0, y0, w, h) => { Fill(x0, y0 + h - 6, w, 6, Mat.CoalDust); Fill(x0 + w / 2, y0 + h - 7, 1, 1, Mat.Ember); }));
		cases.Add(("oil on water, lit", (x0, y0, w, h) => { Fill(x0, y0 + h - 6, w, 6, Mat.Water); Fill(x0, y0 + h - 9, w, 3, Mat.Oil); Fill(x0 + 2, y0 + h - 10, 1, 1, Mat.Fire); }));
		cases.Add(("CO2 pooling", (x0, y0, w, h) => Fill(x0, y0, w, 6, Mat.CO2)));
		cases.Add(("roots seek water", (x0, y0, w, h) => { Fill(x0, y0, w, 2, Mat.Dirt); Fill(x0 + w / 2, y0 + 2, 1, 1, Mat.Root); Fill(x0, y0 + h - 3, w, 3, Mat.Water); }));
		cases.Add(("grey lace spreads", (x0, y0, w, h) => { Fill(x0, y0 + h - 3, w, 3, Mat.Rock); Fill(x0, y0 + h - 5, w, 2, Mat.Water); Fill(x0 + 2, y0 + h - 6, 1, 1, Mat.Mycelium); }));
		cases.Add(("skydust falls up", (x0, y0, w, h) => Fill(x0 + 3, y0 + h - 5, w - 6, 4, Mat.Skydust)));
		cases.Add(("ghostwater seeps", (x0, y0, w, h) => { Fill(x0, y0 + 5, w, 3, Mat.Rock); Fill(x0 + 2, y0 + 2, w - 4, 3, Mat.Ghostwater); }));
		cases.Add(("void in rock", (x0, y0, w, h) => { Fill(x0, y0, w, h, Mat.Rock); Fill(x0 + w / 2, y0 + h / 2, 1, 1, Mat.Void); }));
		cases.Add(("void in crystal", (x0, y0, w, h) => { Fill(x0 + w / 2 - 3, y0 + h / 2 - 3, 7, 7, Mat.Crystal); Fill(x0 + w / 2 - 1, y0 + h / 2 - 1, 3, 3, Mat.Void); }));
		cases.Add(("resonite relay", (x0, y0, w, h) => { Fill(x0, y0 + h - 2, w, 1, Mat.Resonite); Fill(x0, y0 + h - 5, 3, 3, Mat.Hydrogen); Fill(x0 + 1, y0 + h - 3, 1, 1, Mat.Fire); }));
		cases.Add(("mites on metal", (x0, y0, w, h) => { Fill(x0, y0 + h - 3, w, 3, Mat.Metal); Fill(x0 + w / 2 - 1, y0 + h - 5, 3, 2, Mat.Mite); }));
		cases.Add(("cryo + methane", (x0, y0, w, h) => { Fill(x0, y0, w, h - 4, Mat.Methane); Fill(x0 + w / 2 - 1, y0 + h - 4, 3, 2, Mat.Cryo); Fill(x0, y0 + h - 2, w, 2, Mat.Water); }));
		cases.Add(("sunstone kettle", (x0, y0, w, h) => { Fill(x0, y0 + h - 2, w, 2, Mat.Sunstone); Fill(x0, y0 + h - 6, w, 4, Mat.Water); }));
		cases.Add(("salt stops lace", (x0, y0, w, h) => { Fill(x0, y0 + h - 3, w, 3, Mat.Rock); Fill(x0 + w / 2, y0 + h - 6, 2, 3, Mat.Salt); Fill(x0 + 2, y0 + h - 4, 1, 1, Mat.Mycelium); Fill(x0, y0 + h - 5, w / 2 - 1, 1, Mat.Water); }));

		// Strange matter first, so the newest things are on page one.
		cases = cases.OrderByDescending(c => c.label.Split('+', ' ').Any(n => MaterialTable.Defs.Any(d => d.Strange && n.StartsWith(d.Name.Split(' ')[0])))).ToList();
		cases = cases.Skip(page * PerPage).Take(PerPage).ToList();
		int cols = 10, bw = (CW - 4) / cols, bh = 26;
		for (int k = 0; k < cases.Count; k++)
		{
			int x0 = 2 + k % cols * bw, y0 = 2 + k / cols * bh;
			if (y0 + bh > CH - 2) break;
			for (int y = y0; y < y0 + bh; y++) for (int x = x0; x < x0 + bw; x++)
				_w.Set(x, y, x == x0 || x == x0 + bw - 1 || y == y0 + bh - 1 || y == y0 ? Mat.Bedrock : Mat.Empty);
			cases[k].build(x0 + 1, y0 + 5, bw - 2, bh - 6);
			AddLabel(cases[k].label, x0 + 1, y0 + 1, 9);
		}
	}

	void AddLabel(string text, int cx, int cy, int size)
	{
		var l = new Label { Text = text, Position = new Vector2(cx * CellPx, cy * CellPx - 2) };
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_color", new Color(.9f, .87f, .8f, .95f));
		l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 1));
		_labels.AddChild(l);
	}

	static string Short(byte m) => MaterialTable.Name[m].Replace("mercury vapour", "Hg vapour").Replace("quicksilver", "Hg").Replace("lantern ", "").Replace("crystal shards", "shards");

	void Fill(int x0, int y0, int w, int h, byte m)
	{
		for (int y = y0; y < y0 + h; y++) for (int x = x0; x < x0 + w; x++) _w.Set(x, y, m, World.DefaultLife(m));
	}
}

/// <summary>Material swatches grouped by phase. Click to choose; hovering shows the name in the inspector line.</summary>
public partial class PaletteView : Control
{
	readonly Sandbox _s;
	const int Sw = 26, Gap = 4, Cols = 9;
	readonly List<(Rect2 rect, byte mat)> _cells = [];
	byte _hover = 255;

	public PaletteView(Sandbox s) { _s = s; MouseFilter = MouseFilterEnum.Stop; }

	public override void _Draw()
	{
		_cells.Clear();
		var font = ThemeDB.FallbackFont;
		float y = 0;
		foreach (var (phase, title) in new[] { (Phase.Solid, "SOLIDS"), (Phase.Powder, "POWDERS"), (Phase.Liquid, "LIQUIDS"), (Phase.Gas, "GASES & ENERGY") })
		{
			DrawString(font, new Vector2(0, y + 11), title, HorizontalAlignment.Left, -1, 10, new Color("8e958f"));
			y += 15;
			int k = 0;
			for (byte m = 1; m < Mat.Count; m++)
			{
				if (MaterialTable.PhaseOf[m] != phase) continue;
				var r = new Rect2(k % Cols * (Sw + Gap), y + k / Cols * (Sw + Gap), Sw, Sw);
				uint c = MaterialTable.Color[m];
				DrawRect(r, new Color(((c >> 16) & 255) / 255f * 1.2f, ((c >> 8) & 255) / 255f * 1.2f, (c & 255) / 255f * 1.2f));
				if (m == _s.Selected) DrawRect(r.Grow(2), new Color("6fdcc0"), false, 2);
				else if (m == _hover) DrawRect(r.Grow(1), new Color("e4ddcf"), false, 1);
				_cells.Add((r, m));
				k++;
			}
			y += (k + Cols - 1) / Cols * (Sw + Gap) + 2;
		}
		string name = _hover != 255 ? MaterialTable.Name[_hover] : MaterialTable.Name[_s.Selected];
		DrawString(font, new Vector2(0, y + 12), name, HorizontalAlignment.Left, -1, 13, new Color("e4ddcf"));
	}

	public override void _GuiInput(InputEvent e)
	{
		if (e is InputEventMouseMotion mm) _hover = Hit(mm.Position);
		if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left) { byte m = Hit(mb.Position); if (m != 255) _s.Select(m); }
	}

	byte Hit(Vector2 p) { foreach (var (r, m) in _cells) if (r.HasPoint(p)) return m; return 255; }
}
