using System;
using Godot;
using Strata.Rules;
using Strata.Sim;

namespace Strata.Game;

// Pick tips: with the pick in hand, right mouse works a little of whatever you're aiming at onto the pick's tip.
// The tip then changes what every strike does, and wears off with use (or with time, for heat and cold).
//
// Mostly systemic: a tipped strike applies the reactions table between the tip and what it hits (acid dissolves,
// cryo freezes, lava ignites and turns water to steam...), and a tip that reacts into something else becomes that
// (a lava tip quenched in water is now an obsidian tip). On top of that a few materials have their own character:
//   hard (crystal, shards, obsidian, ore)   bites hard rock much faster
//   hot (lava, fire, embers, sunstone)       sets fuel alight, melts ice and snow; lava cools to obsidian
//   cold (cryo, snow, ice, cold fire)        freezes water and lava, makes rock brittle; thaws away
//   resonite                                 rings: shatters rock in a wide circle, and may go off in your hands
//   void                                     eats what it touches, and now and then a bite of the pick
//   sunstone                                 never cools and lights your way
public partial class Game
{
	byte _tip = Mat.Empty;
	int _tipCharges;
	float _tipTimer;     // seconds until heat or cold fades from the tip (0: doesn't)
	float _tipCooldown, _tipHeatTick;

	void ResetTip() { _tip = Mat.Empty; _tipCharges = 0; _tipTimer = 0; }

	static bool IsHardTip(byte m) => m is Mat.Crystal or Mat.Shards or Mat.Obsidian or Mat.Ore or Mat.Copper;

	string TipName => _tip == Mat.Empty ? "" : MaterialTable.Defs[_tip].Name;

	/// <summary>How much harder a strike bites with this tip.</summary>
	float TipPower() => _tip switch
	{
		Mat.Crystal or Mat.Shards => 3f,
		Mat.Obsidian => 2.5f,
		Mat.Ore or Mat.Copper => 1.6f,
		_ when MaterialTable.Cold[_tip] => 1.5f,     // cold makes rock brittle
		_ => 1f,
	};

	/// <summary>Whether a material can tip the pick at all: it has some character or reacts with something.</summary>
	static bool CanTip(byte m) => m != Mat.Empty && m != Mat.Bedrock && m != Mat.Metal &&
		(IsHardTip(m) || MaterialTable.Hot[m] || MaterialTable.Cold[m] || m is Mat.Resonite or Mat.Void or Mat.Fire or Mat.Ember || Reactions.HasAny(m));

	static (int strikes, float timer) TipLife(byte m) => m switch
	{
		Mat.Crystal or Mat.Shards => (40, 0),
		Mat.Obsidian => (30, 0),
		Mat.Ore or Mat.Copper => (25, 0),
		Mat.Lava => (20, 12),
		Mat.Fire or Mat.Ember => (10, 8),
		Mat.Sunstone => (60, 0),
		Mat.Resonite => (12, 0),
		Mat.Void => (8, 0),
		_ when MaterialTable.Cold[m] => (20, 25),
		_ => (12, 0),
	};

	/// <summary>Right mouse with the pick: work some of the material at the aim onto the tip.</summary>
	void TipPick(float mx, float my, float reach)
	{
		if (_tipCooldown > 0) return;
		_tipCooldown = .5f;
		if (reach > 12) { Say("Too far to reach with the pick."); return; }
		int cx = (int)MathF.Floor(mx), cy = (int)MathF.Floor(my);
		byte m = _world.Get(cx, cy);
		if (m == Mat.Empty)
		{
			// Nearest thing next to the aim.
			for (int r = 1; r <= 1 && m == Mat.Empty; r++)
				for (int dy = -r; dy <= r && m == Mat.Empty; dy++)
					for (int dx = -r; dx <= r && m == Mat.Empty; dx++)
						if (_world.Get(cx + dx, cy + dy) != Mat.Empty) { cx += dx; cy += dy; m = _world.Get(cx, cy); }
		}
		if (m == Mat.Empty) return;
		if (!CanTip(m)) { Say($"The {MaterialTable.Defs[m].Name} won't take on the pick. Nothing special about it."); return; }

		// Loose stuff is used up; from solid rock you scrape a little off.
		byte orig = m;
		if (!MaterialTable.Solid[m] || _rng.Chance(.5f)) _world.Set(cx, cy, Mat.Empty);
		Dust(mx, my, MaterialTable.Color[m], 4, 10, 6);

		// The old tip meets the new material first: if they react into something solid, that's the new tip
		// (a lava tip dipped in water comes out obsidian).
		if (_tip != Mat.Empty && _tip != m)
			foreach (var r in Reactions.For(_tip, m) ?? [])
			{
				bool tipFirst = r.A == _tip;
				byte tipInto = tipFirst ? r.AInto : r.BInto;
				if (tipInto == Reaction.Keep || tipInto == Mat.Empty || !MaterialTable.Solid[tipInto]) continue;
				Dust(mx, my, 0xE8E8E8, 6, 18, 12);
				m = tipInto;
				Say($"The {TipName} on the pick meets the {MaterialTable.Defs[orig].Name} and sets into {MaterialTable.Defs[m].Name}. {TipBlurb(m)}");
				_tip = m; (_tipCharges, _tipTimer) = TipLife(m);
				return;
			}
		_tip = m;
		(_tipCharges, _tipTimer) = TipLife(m);
		Say($"You work {MaterialTable.Defs[m].Name} onto the tip of the pick. {TipBlurb(m)}");
	}

	static string TipBlurb(byte m) => m switch
	{
		Mat.Crystal or Mat.Shards => "Hard enough to bite granite.",
		Mat.Obsidian => "A hard black edge.",
		Mat.Ore or Mat.Copper => "A metal edge.",
		Mat.Lava => "It glows. It won't stay hot for long, and the handle is warming.",
		Mat.Fire or Mat.Ember => "It burns. Mind the fuel.",
		Mat.Sunstone => "It glows warm, and it won't cool.",
		Mat.Resonite => "It hums against your palms.",
		Mat.Void => "It isn't there, and it's hungry.",
		_ when MaterialTable.Cold[m] => "Frost spreads up the head.",
		_ => "Let's see what it does.",
	};

	/// <summary>Called on every pick strike before the dig itself. Returns false if the strike should not dig.</summary>
	bool TipStrike(float mx, float my)
	{
		if (_tip == Mat.Empty) return true;
		int cx = (int)MathF.Floor(mx), cy = (int)MathF.Floor(my);
		bool dig = true;
		bool hot = MaterialTable.Hot[_tip] || _tip is Mat.Fire or Mat.Ember, cold = MaterialTable.Cold[_tip];
		if (cold && _world.Get(cx, cy) is Mat.Ice or Mat.Snow) dig = false;     // a cold tip freezes; it won't break its own ice

		if (_tip == Mat.Void)
		{
			// Eats a small round bite out of anything at all.
			for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
				if (_world.Get(cx + dx, cy + dy) != Mat.Bedrock) _world.Set(cx + dx, cy + dy, Mat.Empty);
			dig = false;
			if (_rng.Chance(.02f))
			{
				if (_pack[Item.Pick] > 0) { _pack.Take(Item.Pick); Say("The void tip turns and eats the pick head. You're holding a handle."); }
				else { _p.Body.Injure(Part.RightArm, Hurt.Hurt); Say("The void tip takes a bite out of your glove, and the hand inside it."); }
				ResetTip();
				return false;
			}
		}
		else if (_tip == Mat.Resonite)
		{
			// The pick rings: brittle rock shatters in a wide circle.
			for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++)
			{
				if (dx * dx + dy * dy > 10) continue;
				byte m = _world.Get(cx + dx, cy + dy);
				if (MaterialTable.Solid[m] && MaterialTable.Hardness[m] is > 0 and <= 2 && m != Mat.Resonite && _rng.Chance(.6f)) _world.Set(cx + dx, cy + dy, Mat.Gravel);
			}
			_shake = MathF.Max(_shake, .3f);
			if (_rng.Chance(.06f))
			{
				var (hx, hy, _, _) = Hands(_p.X, _p.Y);
				for (int k = 0; k < 2; k++) PlaceNear(hx, hy - 1, Mat.Blast, 2);
				Say("The ringing climbs and climbs, and the tip goes off in your hands.");
				ResetTip();
				return false;
			}
		}

		// The reactions table, between the tip and everything it strikes. A strike that changes things doesn't
		// also dig: you see the ice form before the next swing breaks it.
		int changed = 0;
		for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
		{
			int x = cx + dx, y = cy + dy;
			byte m = _world.Get(x, y);
			if (m == Mat.Empty || m == _tip) continue;
			if (hot && MaterialTable.Flammability[m] > 0 && _rng.Chance(.5f)) { _world.Set(x, y, Mat.Fire, World.DefaultLife(Mat.Fire)); changed++; continue; }
			if (hot && m is Mat.Ice or Mat.Snow) { _world.Set(x, y, Mat.Water); changed++; continue; }
			if (cold && m is Mat.Water or Mat.Brine) { _world.Set(x, y, Mat.Ice); changed++; continue; }
			if (cold && m == Mat.Lava) { _world.Set(x, y, Mat.Obsidian); changed++; continue; }
			foreach (var r in Reactions.For(_tip, m) ?? [])
			{
				if (!_rng.Chance(MathF.Min(1, r.Chance * 30))) continue;
				bool tipFirst = r.A == _tip;
				byte mInto = tipFirst ? r.BInto : r.AInto, tipInto = tipFirst ? r.AInto : r.BInto;
				if (mInto != Reaction.Keep) { _world.Set(x, y, mInto, World.DefaultLife(mInto)); changed++; }
				// The tip itself can change: lava quenched to obsidian, and so on.
				if (tipInto != Reaction.Keep && tipInto != Mat.Empty && tipInto != _tip && MaterialTable.Solid[tipInto])
				{
					Say($"The {TipName} tip {(MaterialTable.Hot[_tip] ? "hisses and" : "")} turns to {MaterialTable.Defs[tipInto].Name}.");
					_tip = tipInto; (_tipCharges, _tipTimer) = TipLife(tipInto);
				}
				break;
			}
		}

		if (changed > 0) dig = false;
		if (--_tipCharges <= 0) { Say($"The {TipName} has worn off the pick."); ResetTip(); }
		return dig;
	}

	/// <summary>Heat and cold fade from the tip; a lava tip cools into obsidian, and warms the handle meanwhile.</summary>
	void UpdateTip(float dt)
	{
		_tipCooldown -= dt;
		if (_tip == Mat.Empty) return;
		if (_tip == Mat.Lava && _slot == Tool.Pick)
		{
			_tipHeatTick += dt;
			if (_tipHeatTick > 4) { _tipHeatTick = 0; if (_rng.Chance(.35f)) { _p.Body.Injure(Part.RightArm, Hurt.Hurt); Say("The heat works up the handle. Your hand burns."); } }
		}
		// Dunk a hot tip in water, or a cold one in lava, and it changes at once.
		var (hx, hy, dx, dy) = Hands(_p.X, _p.Y);
		byte at = _world.Get((int)MathF.Floor(hx + dx * 1.7f), (int)MathF.Floor(hy + dy * 1.7f));
		if (_tip == Mat.Lava && at is Mat.Water or Mat.Brine) { Say("The lava tip hisses in the water and sets hard: obsidian."); _tip = Mat.Obsidian; (_tipCharges, _tipTimer) = TipLife(_tip); return; }
		if (MaterialTable.Cold[_tip] && at == Mat.Lava) { Say($"The {TipName} flashes off the tip in the lava."); ResetTip(); return; }
		if (_tipTimer > 0)
		{
			_tipTimer -= dt;
			if (_tipTimer <= 0)
			{
				if (_tip == Mat.Lava) { _tip = Mat.Obsidian; (_tipCharges, _tipTimer) = TipLife(_tip); Say("The lava on the tip has cooled to obsidian. A hard black edge."); }
				else { Say($"The {TipName} on the pick has {(MaterialTable.Cold[_tip] ? "thawed" : "burned out")}."); ResetTip(); }
			}
		}
	}

	/// <summary>Colour of the tip for drawing, and whether it glows.</summary>
	(Color col, bool glow) TipLook()
	{
		if (_tip == Mat.Empty) return (default, false);
		uint c = MaterialTable.Color[_tip];
		bool glow = MaterialTable.Emission[_tip] > .1f || _tip is Mat.Fire or Mat.Ember or Mat.Lava;
		float pulse = glow ? 1.2f + .2f * MathF.Sin(_frame * .2f) : 1;
		return (C(c, pulse), glow);
	}
}
