using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Strata.Rules;
using Strata.Sim;

namespace Strata.Game;

// The hot bar: the tool in your hands decides what the mouse buttons (or triggers) do.
//   Pick     LMB digs (as before)
//   Shovel   LMB scoops loose stuff onto the blade, RMB throws the load
//   Sprayer  LMB or RMB sprays the loaded canister
//   Grapple  LMB fires the hook (G works with any tool)
//   Vacuum   a glass tank worn on your back, with a hose to a nozzle in your hands: LMB hoovers up liquid, gas or
//            powder, RMB blows it back out
//
// The tank is glass. What's inside keeps reacting, heats or chills it, and builds pressure; hot glass meeting cold
// cracks, overheated glass crazes, too much gas makes it creak, a hard landing or a nearby blast can crack it, and
// fuel with a flame in it goes off on your back. Cracked glass leaks; enough cracks and it shatters.
public enum Tool { Pick, Shovel, Sprayer, Grapple, Vacuum }

public partial class Game
{
	static readonly Tool[] Hotbar = [Tool.Pick, Tool.Shovel, Tool.Sprayer, Tool.Grapple, Tool.Vacuum];
	Tool _slot = Tool.Pick;
	bool _primaryWas;
	HotbarPanel _hotbar = null!;

	void ResetTools()
	{
		_slot = Tool.Pick;
		_shovel.Clear();
		ResetTip();
		Array.Clear(_jar); _jarTotal = 0; _hasJar = true; _jarCracks = 0; _jarTemp = 20; _jarPress = 0; _jarStress = 0;
	}

	void SelectSlot(int i)
	{
		i = ((i % Hotbar.Length) + Hotbar.Length) % Hotbar.Length;
		_slot = Hotbar[i];
	}

	/// <summary>Where the hands are: the front hand reaches 2.4 cells from the shoulder towards the aim.</summary>
	(float x, float y, float dx, float dy) Hands(float px, float py)
	{
		var (ax, ay) = AimPoint(px, py - Player.Height + 2);
		float sx = px + .25f * (_p.Facing ? 1 : -1), sy = py - 4.7f;
		float dx = ax - sx, dy = ay - sy, d = MathF.Max(.01f, MathF.Sqrt(dx * dx + dy * dy));
		return (sx + dx / d * 2.4f, sy + dy / d * 2.4f, dx / d, dy / d);
	}

	/// <summary>Runs the tool in your hands for one tick. Returns true if the grappling hook should fire.</summary>
	bool UseTools(float dt, float hx, float hy, float mx, float my, float reach)
	{
		bool primary = _ctl.Dig && !_p.Body.Downed, secondary = _ctl.Spray && !_p.Body.Downed;
		bool primaryPressed = primary && !_primaryWas;
		_primaryWas = primary;
		_shovelCooldown -= dt; _noTankSaid -= dt;
		switch (_slot)
		{
			case Tool.Pick:
				if (primary && _digCooldown <= 0 && reach < 12) { if (TipStrike(mx, my)) Dig(mx, my); else _digCooldown = .15f; }
				else if (secondary && !primary) TipPick(mx, my, reach);
				break;
			case Tool.Shovel:
				if (primary && _shovelCooldown <= 0) ShovelScoop(mx, my, reach);
				else if (secondary && _shovelCooldown <= 0 && _shovel.Count > 0) ShovelThrow(hx, hy, mx, my);
				break;
			case Tool.Sprayer:
				if (primary || secondary) Spray(hx, hy, mx, my);
				break;
			case Tool.Grapple:
				return primaryPressed;
			case Tool.Vacuum:
				if (primary) JarFill(mx, my, reach, dt);
				else if (secondary) JarEmpty(hx, hy, mx, my, dt);
				else _jarAcc = 0;
				break;
		}
		return false;
	}

	// ---------- Shovel ----------

	const int ShovelCap = 14;
	readonly List<byte> _shovel = [];
	float _shovelCooldown;

	static bool Shovelable(byte m) => MaterialTable.Powder[m] || MaterialTable.Solid[m] && MaterialTable.Hardness[m] is > 0 and <= .6f;

	void ShovelScoop(float mx, float my, float reach)
	{
		_shovelCooldown = .16f * (_p.Body[Part.RightArm] >= Hurt.Hurt ? 2 : 1);
		if (reach > 10) { Say("Too far to reach with the shovel."); return; }
		if (_shovel.Count >= ShovelCap) { Say("The shovel's full. Right mouse to throw the load."); return; }
		int got = 0;
		int cx = (int)MathF.Floor(mx), cy = (int)MathF.Floor(my);
		for (int r = 0; r <= 2 && got < 5 && _shovel.Count < ShovelCap; r++)
			for (int dy = -r; dy <= r && got < 5 && _shovel.Count < ShovelCap; dy++)
				for (int dx = -r; dx <= r && got < 5 && _shovel.Count < ShovelCap; dx++)
				{
					if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
					byte m = _world.Get(cx + dx, cy + dy);
					if (!Shovelable(m)) continue;
					_world.Set(cx + dx, cy + dy, Mat.Empty);
					_shovel.Add(m == Mat.Shale ? Mat.Gravel : m);
					got++;
				}
		if (got > 0) { Dust(mx, my, MaterialTable.Color[_shovel[^1]], 3, 10, 8); return; }
		byte at = _world.Get(cx, cy);
		if (MaterialTable.Solid[at]) { Sparks(mx, my, 2); Say($"The shovel rings off the {MaterialTable.Defs[at].Name}. Too hard: use the pick."); }
		else if (MaterialTable.Liquid[at] || MaterialTable.Gas[at]) Say($"It runs straight off the blade. The vacuum takes {MaterialTable.Defs[at].Name}.");
	}

	void ShovelThrow(float hx, float hy, float mx, float my)
	{
		_shovelCooldown = .3f;
		float ax = mx - hx, ay = my - hy, d = MathF.Max(.01f, MathF.Sqrt(ax * ax + ay * ay));
		float a0 = MathF.Atan2(ay / d, ax / d);
		foreach (var m in _shovel)
		{
			float a = a0 + (_rng.Float() - .5f) * .3f, v = 42 + _rng.Float() * 16;
			_spray.Add(new Drop { X = hx + ax / d * 2, Y = hy + ay / d * 2, VX = MathF.Cos(a) * v + _p.VX * .5f, VY = MathF.Sin(a) * v - 6, Life = 2.5f, M = m });
		}
		_shovel.Clear();
	}

	// ---------- Shared: putting single units of material back into the world ----------

	/// <summary>One unit out of a nozzle: gases straight into open cells in a short cone, the rest as droplets.</summary>
	void EmitUnit(byte m, float nx, float ny, float dx, float dy, float spread = .35f)
	{
		float a = MathF.Atan2(dy, dx) + (_rng.Float() - .5f) * spread;
		if (MaterialTable.Gas[m])
		{
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

	/// <summary>Puts a unit in the nearest open cell around a point (not inside the delver). False if there's no room.</summary>
	bool PlaceNear(float fx, float fy, byte m, int radius = 5)
	{
		int x = (int)MathF.Floor(fx), y = (int)MathF.Floor(fy);
		for (int r = 0; r <= radius; r++)
			for (int dy = -r; dy <= r; dy++)
				for (int dx = -r; dx <= r; dx++)
				{
					if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
					if (_world.Get(x + dx, y + dy) != Mat.Empty || InBody(x + dx, y + dy)) continue;
					_world.Set(x + dx, y + dy, m, World.DefaultLife(m));
					return true;
				}
		return false;
	}

	// ---------- The vacuum tank: glass, on your back ----------

	const int JarCap = 80;
	readonly int[] _jar = new int[Mat.Count];
	int _jarTotal, _jarCracks;           // cracks: 0 sound, 1 cracked, 2 badly cracked, 3 gone
	bool _hasJar = true;
	float _jarTemp = 20, _jarAcc, _jarPress, _jarStress, _jarLeak, _noTankSaid;

	/// <summary>The tank sits on your back, between the shoulders.</summary>
	(float x, float y) JarPos() => (_p.X - 1.4f * (_p.Facing ? 1 : -1), _p.Y - 4.2f);
	(float x, float y) NozzlePos() { var h = Hands(_p.X, _p.Y); return (h.x + h.dx * 1.6f, h.y + h.dy * 1.6f); }

	static bool Suckable(byte m) => m != Mat.Empty && !MaterialTable.Solid[m];

	byte RandomContent()
	{
		int r = _rng.Int(_jarTotal);
		for (int m = 1; m < Mat.Count; m++) { r -= _jar[m]; if (r < 0) return (byte)m; }
		return Mat.Empty;
	}

	void JarTake(byte m) { _jar[m]--; _jarTotal--; }
	void JarPut(byte m) { if (m == Mat.Empty) return; _jar[m]++; _jarTotal++; }

	void JarFill(float mx, float my, float reach, float dt)
	{
		if (!_hasJar) { if (_noTankSaid <= 0) Say("No tank on your back. There are spares in the locker."); _noTankSaid = 3; return; }
		if (reach > 11) { Say("Too far: get the nozzle closer."); return; }
		_jarAcc += dt * 45;
		int cx = (int)MathF.Floor(mx), cy = (int)MathF.Floor(my);
		while (_jarAcc >= 1 && _jarTotal < JarCap && _hasJar)
		{
			_jarAcc -= 1;
			int bx = 0, by = 0, best = int.MaxValue;
			for (int dy = -3; dy <= 3; dy++)
				for (int dx = -3; dx <= 3; dx++)
				{
					int d = dx * dx + dy * dy;
					if (d < best && Suckable(_world.Get(cx + dx, cy + dy))) { best = d; bx = cx + dx; by = cy + dy; }
				}
			if (best == int.MaxValue) { _jarAcc = 0; break; }
			byte m = _world.Get(bx, by);
			_world.Set(bx, by, Mat.Empty);
			JarPut(m);
			// Thermal shock as it goes in: cold into hot glass, or something hot into frozen glass.
			if (MaterialTable.Cold[m] && _jarTemp > 90) JarCrack(2, $"The {MaterialTable.Defs[m].Name} pours into the hot tank. The glass cracks right across.");
			else if (MaterialTable.Hot[m] && _jarTemp < -20) JarCrack(2, $"The {MaterialTable.Defs[m].Name} pours into the frozen tank. The glass cracks right across.");
		}
		if (_hasJar && _jarTotal >= JarCap) { _jarAcc = 0; Say("The tank is full."); }
	}

	void JarEmpty(float hx, float hy, float mx, float my, float dt)
	{
		if (!_hasJar) { if (_noTankSaid <= 0) Say("No tank on your back. There are spares in the locker."); _noTankSaid = 3; return; }
		if (_jarTotal == 0) return;
		var (jx, jy) = NozzlePos();
		float ax = mx - jx, ay = my - jy, d = MathF.Max(.01f, MathF.Sqrt(ax * ax + ay * ay));
		_jarAcc += dt * 50;
		while (_jarAcc >= 1 && _jarTotal > 0)
		{
			_jarAcc -= 1;
			byte m = RandomContent();
			JarTake(m);
			EmitUnit(m, jx + ax / d * .8f, jy + ay / d * .8f, ax / d, ay / d, .5f);
		}
	}

	/// <summary>What happens inside the jar each tick: reactions, burning, heat, pressure, leaks.</summary>
	void UpdateJar(float dt)
	{
		if (!_hasJar) return;
		var (jx, jy) = JarPos();

		if (_jarTotal >= 2)
		{
			// Fuel and a flame: burn, or with enough fuel gas, go off.
			int hotUnits = 0, fuelGas = 0, fuel = 0;
			for (int m = 1; m < Mat.Count; m++)
			{
				if (_jar[m] == 0) continue;
				if (m is Mat.Fire or Mat.Ember or Mat.Lava) hotUnits += _jar[m];
				if (MaterialTable.Flammability[m] > 0) { fuel += _jar[m]; if (MaterialTable.Gas[m]) fuelGas += _jar[m]; }
			}
			if (hotUnits > 0 && fuelGas >= 6) { JarExplode(); return; }
			if (hotUnits > 0 && fuel > 0 && _rng.Chance(dt * 6))
			{
				byte f = RandomFuel();
				if (f != Mat.Empty) { JarTake(f); JarPut(Mat.Fire); _jarTemp += 25; }
			}

			// Ordinary reactions, sped up: everything in a jar is touching everything else.
			for (int t = 0; t < 3; t++)
			{
				byte a = RandomContent(), b = RandomContent();
				if (a == b && _jar[a] < 2) continue;
				var rs = Reactions.For(a, b);
				if (rs == null) continue;
				foreach (var r in rs)
				{
					if (!_rng.Chance(MathF.Min(1, r.Chance * 10))) continue;
					bool aFirst = r.A == a;
					byte aInto = aFirst ? r.AInto : r.BInto, bInto = aFirst ? r.BInto : r.AInto;
					if (aInto == Mat.Blast || bInto == Mat.Blast) { JarExplode(); return; }
					if (aInto != Reaction.Keep) { JarTake(a); JarPut(aInto); }
					if (bInto != Reaction.Keep) { JarTake(b); JarPut(bInto); }
					break;
				}
			}
		}
		// Flames die down to smoke, embers to ash.
		if (_jar[Mat.Fire] > 0 && _rng.Chance(dt * _jar[Mat.Fire] * .5f)) { JarTake(Mat.Fire); JarPut(Mat.Smoke); }
		if (_jar[Mat.Ember] > 0 && _rng.Chance(dt * _jar[Mat.Ember] * .1f)) { JarTake(Mat.Ember); JarPut(Mat.Ash); }

		// Temperature: follows what's inside, and what the jar is sitting in.
		float hot = 0, cold = 0, gas = 0;
		for (int m = 1; m < Mat.Count; m++)
		{
			if (_jar[m] == 0) continue;
			float w = _jar[m] / (float)JarCap;
			if (MaterialTable.Hot[m]) hot += w * (m == Mat.Lava ? 2.5f : 1);
			if (MaterialTable.Cold[m]) cold += w;
			if (MaterialTable.Gas[m]) gas += _jar[m];
		}
		float target = 20 + 420 * hot - 260 * cold;
		_jarTemp += (target - _jarTemp) * MathF.Min(1, dt * .6f);
		byte outside = _world.Get((int)MathF.Floor(jx), (int)MathF.Floor(jy));
		if (outside is Mat.Lava or Mat.Fire) _jarTemp += 150 * dt;
		if (_jarTemp > 90 && (MaterialTable.Cold[outside] || outside is Mat.Water or Mat.Brine))
		{ JarCrack(3, "The hot tank on your back hits the cold. It bursts with a crack like a gunshot."); return; }

		if (_jarTemp > 170)
		{
			_jarStress += dt;
			if (_jarStress > 1.5f) { _jarStress = 0; JarCrack(1, "The glass sags and crazes in the heat."); if (!_hasJar) return; }
		}
		else _jarStress = 0;

		// Pressure: lots of gas, sealed in.
		if (gas > 45 && _jarCracks == 0)
		{
			_jarPress += dt * (gas - 45) / 20;
			if (_jarPress > 1) { _jarPress = 0; JarCrack(1, "The tank creaks under the pressure. A crack hisses."); if (!_hasJar) return; }
		}
		else _jarPress = MathF.Max(0, _jarPress - dt);

		// Cracked glass leaks.
		if (_jarCracks > 0 && _jarTotal > 0)
		{
			_jarLeak += dt * (_jarCracks == 1 ? 2 : 7) * (gas > 30 ? 2 : 1);
			while (_jarLeak >= 1 && _jarTotal > 0)
			{
				_jarLeak -= 1;
				byte m = RandomContent();
				JarTake(m);
				PlaceNear(jx, jy + (MaterialTable.Gas[m] ? -1 : 1), m, 3);
			}
		}
	}

	byte RandomFuel()
	{
		int total = 0;
		for (int m = 1; m < Mat.Count; m++) if (MaterialTable.Flammability[m] > 0) total += _jar[m];
		if (total == 0) return Mat.Empty;
		int r = _rng.Int(total);
		for (int m = 1; m < Mat.Count; m++) if (MaterialTable.Flammability[m] > 0) { r -= _jar[m]; if (r < 0) return (byte)m; }
		return Mat.Empty;
	}

	void JarCrack(int n, string why)
	{
		if (!_hasJar) return;
		_jarCracks += n;
		if (_jarCracks >= 3) JarShatter(why);
		else { Say(why); Sparks(JarPos().x, JarPos().y, 2); }
	}

	void SpillJar(float jx, float jy)
	{
		for (int m = 1; m < Mat.Count; m++)
			for (int k = 0; k < _jar[m]; k++) PlaceNear(jx + (_rng.Float() - .5f) * 2, jy + (_rng.Float() - .5f) * 2, (byte)m, 6);
		Array.Clear(_jar); _jarTotal = 0;
	}

	void JarShatter(string why)
	{
		var (jx, jy) = JarPos();
		SpillJar(jx, jy);
		for (int k = 0; k < 4; k++) PlaceNear(jx, jy + 1, Mat.Shards, 3);
		Dust(jx, jy, 0xBFE6F0, 10, 26, 10);
		_hasJar = false; _jarCracks = 0; _jarTemp = 20;
		if (_rng.Chance(.4f)) { _p.Body.Injure(Part.Torso, Hurt.Hurt); why += " Glass cuts into your back."; }
		Say(why);
		_shake = MathF.Max(_shake, .25f);
	}

	void JarExplode()
	{
		var (jx, jy) = JarPos();
		SpillJar(jx, jy);
		for (int k = 0; k < 3; k++) PlaceNear(jx, jy, Mat.Blast, 2);
		for (int k = 0; k < 4; k++) PlaceNear(jx, jy + 1, Mat.Shards, 4);
		_hasJar = false; _jarCracks = 0; _jarTemp = 20;
		Say("Flame met fuel inside the tank. It goes off on your back.");
		_shake = 1;
	}

	/// <summary>A hard landing jars the glass.</summary>
	void JarImpact(float speed)
	{
		if (!_hasJar) return;
		if (speed > 130) JarCrack(3, "You land hard. The tank on your back shatters.");
		else if (speed > 100 && _rng.Chance(.6f)) JarCrack(1, "You land hard. The tank on your back cracks.");
	}

	/// <summary>A blast nearby: k is how close (0 far .. 1 on top of it).</summary>
	void JarBlast(float k)
	{
		if (!_hasJar) return;
		if (k > .5f) JarCrack(3, "The blast shatters the tank on your back.");
		else if (k > .2f) JarCrack(1, "The blast cracks the tank on your back.");
	}

	string JarContents()
	{
		if (_jarTotal == 0) return "empty";
		var top = Enumerable.Range(1, Mat.Count - 1).Where(m => _jar[m] > 0).OrderByDescending(m => _jar[m]).Take(3)
			.Select(m => $"{MaterialTable.Defs[m].Name} {_jar[m]}");
		return string.Join(", ", top) + (Enumerable.Range(1, Mat.Count - 1).Count(m => _jar[m] > 0) > 3 ? ", …" : "");
	}

	string ToolHint() => _slot switch
	{
		Tool.Pick => _tip == Mat.Empty ? "LMB: dig · RMB: tip the pick in a material"
			: $"LMB: dig ({TipName} tip, {_tipCharges} strikes left{(_tipTimer > 0 ? $", {_tipTimer:F0}s" : "")}) · RMB: re-tip",
		Tool.Shovel => _shovel.Count > 0 ? $"RMB: throw the load ({_shovel.Count}) · LMB: scoop more" : "LMB: scoop loose stuff (sand, gravel, dirt, soft rock)",
		Tool.Sprayer => $"LMB: spray {MaterialTable.Defs[SprayMat].Name} · Z/X: change canister",
		Tool.Grapple => "LMB or G: fire the hook",
		Tool.Vacuum => !_hasJar ? "no tank: there are spares in the locker"
			: $"LMB: hoover up · RMB: blow out · tank: {JarContents()} · {_jarTemp:F0}°{(_jarCracks > 0 ? (_jarCracks == 1 ? " · cracked" : " · badly cracked") : "")}",
		_ => "",
	};

	// ---------- Drawing the tool in your hands ----------

	void DrawTool()
	{
		if (_p.Body.Downed || _p.Clinging || _inTruck) return;
		var (hx, hy, dx, dy) = Hands(_playerX, _playerY);
		float l = Lt(hx, hy);
		float ang = MathF.Atan2(dy, dx);
		int f = dx >= 0 ? 1 : -1;
		var slot = _slot;
		var shovel = _shovel.ToArray();
		uint sprayCol = MaterialTable.Color[SprayMat];
		bool hookOut = _hookFlying || _grappled;
		var (tx, ty) = (_playerX - 1.4f * (_p.Facing ? 1 : -1), _playerY - 3.4f);
		bool tank = _hasJar;
		var tip = TipLook();
		bool tipped = _tip != Mat.Empty;
		Shape(ci =>
		{
			var h = S(hx, hy);
			if (slot == Tool.Vacuum && tank)
			{
				// The hose: from the bottom of the tank, sagging, to the nozzle.
				var a = S(tx, ty); var b = h; var mid = (a + b) / 2 + new Vector2(0, Px(1.6f));
				var hose = new Vector2[12];
				for (int k = 0; k < 12; k++) { float t = k / 11f; hose[k] = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * mid + t * t * b; }
				ci.DrawPolyline(hose, C(0x2E3236, l), Px(.42f), true);
				ci.DrawPolyline(hose, C(0x4A5056, l), Px(.18f), true);
			}
			ci.DrawSetTransform(h, ang, new Vector2(1, f));
			Color wood = C(0x8A6440, l), steel = C(0xA8B2B8, l), dark = C(0x3A3E42, l);
			switch (slot)
			{
				case Tool.Pick:
					ci.DrawLine(new Vector2(-Px(1), 0), new Vector2(Px(1.6f), 0), wood, Px(.32f), true);
					ci.DrawPolyline([new Vector2(Px(1.2f), -Px(1.1f)), new Vector2(Px(1.7f), 0), new Vector2(Px(1.2f), Px(1.1f))], steel, Px(.35f), true);
					if (tipped)
					{
						var tipAt = new Vector2(Px(1.2f), Px(1.1f));
						if (tip.glow) Glow(ci, tipAt, Px(1.3f), new Color(tip.col.R, tip.col.G, tip.col.B, .6f));
						ci.DrawLine(new Vector2(Px(1.45f), Px(.55f)), tipAt, tip.col, Px(.45f), true);
						ci.DrawCircle(tipAt, Px(.28f), tip.col, true, -1, true);
					}
					break;
				case Tool.Shovel:
					ci.DrawLine(new Vector2(-Px(1.4f), 0), new Vector2(Px(1.5f), 0), wood, Px(.3f), true);
					ci.DrawColoredPolygon([new Vector2(Px(1.4f), -Px(.75f)), new Vector2(Px(2.7f), -Px(.6f)), new Vector2(Px(3), 0), new Vector2(Px(2.7f), Px(.6f)), new Vector2(Px(1.4f), Px(.75f))], steel);
					for (int k = 0; k < shovel.Length; k++)
						ci.DrawCircle(new Vector2(Px(1.8f + (k % 4) * .3f), -Px(.7f + (k / 4) * .3f)), Px(.28f), C(MaterialTable.Color[shovel[k]], l), true, -1, true);
					break;
				case Tool.Sprayer:
					Box(ci, new Rect2(-Px(.6f), -Px(.45f), Px(2.4f), Px(.9f)), Px(.3f), C(0x5A6670, l), C(0x3A444C, l), 1.2f);
					ci.DrawLine(new Vector2(Px(1.8f), 0), new Vector2(Px(2.6f), 0), dark, Px(.3f), true);
					Box(ci, new Rect2(-Px(.2f), Px(.4f), Px(.9f), Px(1.2f)), Px(.3f), C(sprayCol, MathF.Max(l, .8f)).Lightened(.1f), C(sprayCol, l).Darkened(.2f), 1);
					break;
				case Tool.Grapple:
					Box(ci, new Rect2(-Px(.5f), -Px(.4f), Px(2), Px(.8f)), Px(.25f), C(0x6A5A48, l), C(0x4A3E30, l), 1.2f);
					if (!hookOut)
						for (int k = 0; k < 3; k++) { float a = -1 + k; ci.DrawLine(new Vector2(Px(1.6f), 0), new Vector2(Px(1.6f + .6f * MathF.Cos(a)), Px(.6f * MathF.Sin(a))), steel, 2, true); }
					break;
				case Tool.Vacuum:
					Box(ci, new Rect2(-Px(.4f), -Px(.32f), Px(1.9f), Px(.64f)), Px(.2f), C(0x7A848A, l), C(0x4E565C, l), 1.2f);
					ci.DrawColoredPolygon([new Vector2(Px(1.4f), -Px(.35f)), new Vector2(Px(2.2f), -Px(.65f)), new Vector2(Px(2.2f), Px(.65f)), new Vector2(Px(1.4f), Px(.35f))], C(0x9AA4AA, l));
					break;
			}
			ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
		});
	}

	/// <summary>A snapshot of how the jar looks: layers by phase, bottom to top, plus cracks and heat.</summary>
	(bool has, float powder, Color powderC, float liquid, Color liquidC, float gas, Color gasC, int cracks, float temp) JarLook()
	{
		float pw = 0, lq = 0, gs = 0;
		Vector3 pc = default, lc = default, gc = default;
		for (int m = 1; m < Mat.Count; m++)
		{
			int n = _jar[m];
			if (n == 0) continue;
			uint c = MaterialTable.Color[m];
			var v = new Vector3(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f) * n;
			if (MaterialTable.Gas[m]) { gs += n; gc += v; }
			else if (MaterialTable.Liquid[m]) { lq += n; lc += v; }
			else { pw += n; pc += v; }
		}
		static Color Avg(Vector3 v, float n) => n > 0 ? new Color(v.X / n, v.Y / n, v.Z / n) : Colors.Black;
		return (_hasJar, pw / JarCap, Avg(pc, pw), lq / JarCap, Avg(lc, lq), gs / JarCap, Avg(gc, gs), _jarCracks, _jarTemp);
	}

	/// <summary>Draws the jar centred at a screen point, at a scale (1 = in the hand).</summary>
	static void DrawJar(CanvasItem ci, Vector2 c, float scale,
		(bool has, float powder, Color powderC, float liquid, Color liquidC, float gas, Color gasC, int cracks, float temp) j, float light)
	{
		float w = Px(1.6f) * scale, h = Px(2.2f) * scale;
		var r = new Rect2(c.X - w / 2, c.Y - h / 2, w, h);
		// Contents, bottom up: powder, liquid, then gas filling the rest faintly.
		float y = r.End.Y - 2 * scale;
		float inner = h - 4 * scale;
		void Layer(float frac, Color col, float alpha)
		{
			if (frac <= 0) return;
			float hh = MathF.Min(frac, 1) * inner;
			RoundRect(ci, new Rect2(r.Position.X + 2 * scale, y - hh, w - 4 * scale, hh), 2 * scale, new Color(col.R * light, col.G * light, col.B * light, alpha));
			y -= hh;
		}
		Layer(j.powder, j.powderC, 1);
		Layer(j.liquid, j.liquidC, .9f);
		if (j.gas > 0) RoundRect(ci, new Rect2(r.Position.X + 2 * scale, r.Position.Y + 3 * scale, w - 4 * scale, MathF.Max(0, y - r.Position.Y - 3 * scale)), 2 * scale, new Color(j.gasC.R, j.gasC.G, j.gasC.B, MathF.Min(.6f, .15f + j.gas)));
		// Glass: tinted by heat or frost.
		var glass = j.temp > 100 ? new Color(1, .55f, .3f, MathF.Min(.6f, (j.temp - 100) / 200)) : j.temp < -10 ? new Color(.8f, .95f, 1, .35f) : new Color(.75f, .92f, 1, .12f);
		RoundRect(ci, r, 4 * scale, glass);
		var pts = RoundRectPoints(r, 4 * scale);
		var loop = new Vector2[pts.Length + 1]; pts.CopyTo(loop, 0); loop[^1] = pts[0];
		ci.DrawPolyline(loop, new Color(.85f, .95f, 1, .85f), 1.6f * scale, true);
		ci.DrawLine(r.Position + new Vector2(w * .25f, h * .15f), r.Position + new Vector2(w * .2f, h * .6f), new Color(1, 1, 1, .5f), 1.5f * scale, true);
		// Lid.
		RoundRect(ci, new Rect2(r.Position.X + w * .15f, r.Position.Y - 3 * scale, w * .7f, 4 * scale), 1.5f * scale, new Color(.45f, .48f, .5f));
		// Cracks.
		if (j.cracks >= 1) ci.DrawPolyline([r.Position + new Vector2(w * .7f, h * .1f), r.Position + new Vector2(w * .5f, h * .35f), r.Position + new Vector2(w * .65f, h * .55f)], new Color(1, 1, 1, .9f), 1.2f * scale, true);
		if (j.cracks >= 2) ci.DrawPolyline([r.Position + new Vector2(w * .2f, h * .9f), r.Position + new Vector2(w * .45f, h * .7f), r.Position + new Vector2(w * .35f, h * .45f), r.Position + new Vector2(w * .8f, h * .8f)], new Color(1, 1, 1, .9f), 1.2f * scale, true);
	}

	// ---------- The hot bar ----------

	void DrawHotbar(CanvasItem ci)
	{
		const float size = 56, gap = 8;
		var font = ThemeDB.FallbackFont;
		var jar = JarLook();
		var tipLook = TipLook();
		for (int i = 0; i < Hotbar.Length; i++)
		{
			var tool = Hotbar[i];
			var r = new Rect2(i * (size + gap), 0, size, size);
			bool on = tool == _slot;
			RoundRect(ci, r, 8, new Color(0, 0, 0, on ? .65f : .45f));
			var border = RoundRectPoints(r, 8);
			var loop = new Vector2[border.Length + 1]; border.CopyTo(loop, 0); loop[^1] = border[0];
			ci.DrawPolyline(loop, on ? new Color(.44f, .86f, .75f) : new Color(1, 1, 1, .18f), on ? 2.5f : 1.2f, true);
			ci.DrawString(font, r.Position + new Vector2(6, 15), (i + 1).ToString(), HorizontalAlignment.Left, -1, 12, new Color(1, 1, 1, .6f));
			var c = r.GetCenter() + new Vector2(0, -2);
			var steel = new Color(.66f, .7f, .72f); var wood = new Color(.54f, .39f, .25f);
			switch (tool)
			{
				case Tool.Pick:
					ci.DrawLine(c + new Vector2(-12, 12), c + new Vector2(10, -10), wood, 4, true);
					ci.DrawPolyline([c + new Vector2(-2, -16), c + new Vector2(10, -10), c + new Vector2(16, 2)], steel, 4.5f, true);
					if (_tip != Mat.Empty) { if (tipLook.glow) Glow(ci, c + new Vector2(16, 2), 9, new Color(tipLook.col.R, tipLook.col.G, tipLook.col.B, .6f)); ci.DrawCircle(c + new Vector2(16, 2), 4, tipLook.col, true, -1, true); }
					break;
				case Tool.Shovel:
					ci.DrawLine(c + new Vector2(-12, -14), c + new Vector2(4, 4), wood, 4, true);
					ci.DrawColoredPolygon([c + new Vector2(0, 2), c + new Vector2(8, -4), c + new Vector2(16, 6), c + new Vector2(12, 16), c + new Vector2(4, 12)], steel);
					if (_shovel.Count > 0) ci.DrawCircle(c + new Vector2(10, 6), 4, C(MaterialTable.Color[_shovel[0]]), true, -1, true);
					break;
				case Tool.Sprayer:
					RoundRect(ci, new Rect2(c + new Vector2(-14, -6), new Vector2(26, 9)), 3, new Color(.4f, .45f, .5f));
					ci.DrawLine(c + new Vector2(12, -2), c + new Vector2(18, -2), new Color(.25f, .27f, .3f), 3, true);
					RoundRect(ci, new Rect2(c + new Vector2(-8, 3), new Vector2(9, 13)), 3, C(MaterialTable.Color[SprayMat], 1.1f));
					break;
				case Tool.Grapple:
					RoundRect(ci, new Rect2(c + new Vector2(-14, -4), new Vector2(18, 8)), 3, new Color(.42f, .35f, .28f));
					for (int k = 0; k < 3; k++) { float a = -1 + k; ci.DrawLine(c + new Vector2(6, 0), c + new Vector2(6 + 10 * MathF.Cos(a), 10 * MathF.Sin(a)), steel, 3, true); }
					break;
				case Tool.Vacuum:
					if (jar.has) DrawJar(ci, c + new Vector2(0, 3), 1.15f, jar, 1);
					else ci.DrawString(font, c + new Vector2(-14, 4), "none", HorizontalAlignment.Left, -1, 12, new Color(1, 1, 1, .5f));
					break;
			}
			string name = tool.ToString().ToLowerInvariant();
			float tw = font.GetStringSize(name, HorizontalAlignment.Left, -1, 11).X;
			ci.DrawString(font, new Vector2(r.GetCenter().X - tw / 2, r.End.Y + 13), name, HorizontalAlignment.Left, -1, 11, on ? new Color(.9f, .95f, .9f) : new Color(1, 1, 1, .5f));
		}
	}
}

/// <summary>The hot bar along the bottom of the screen; the game paints it.</summary>
public partial class HotbarPanel : Control
{
	public Action<CanvasItem>? Paint;
	public override void _Draw() => Paint?.Invoke(this);
}
