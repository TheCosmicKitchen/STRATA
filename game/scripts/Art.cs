using System;
using System.Collections.Generic;
using Godot;
using Strata.Rules;
using Strata.Sim;

namespace Strata.Game;

// Everything that isn't the cave itself: the delver, the base and its stations, the truck, the line and hook,
// flares, droplets, dust and the bubble. Drawn as smooth, antialiased shapes on the actor layer, at world positions
// in cells (converted to screen pixels here), tinted by the light where they are.
public partial class Game
{
	Vector2 S(float x, float y) => new((x - _camFX) * CellPx, (y - _camFY) * CellPx);
	Rect2 R(float x, float y, float w, float h) => new(S(x, y), new Vector2(w * CellPx, h * CellPx));
	static float Px(float cells) => cells * CellPx;

	/// <summary>How lit a point is, for tinting shapes: never fully black, a touch of headroom for bright spots.</summary>
	float Lt(float x, float y)
	{
		int vx = (int)MathF.Floor(x) - _camX, vy = (int)MathF.Floor(y) - _camY;
		float l = (uint)vx < (uint)_vw && (uint)vy < (uint)_vh ? _light[vy * _vw + vx] : 0;
		return Math.Clamp(l * 1.35f + .18f, .22f, 1.1f);
	}

	static Color C(uint rgb, float k = 1, float a = 1) =>
		new(MathF.Min(1, ((rgb >> 16) & 255) / 255f * k), MathF.Min(1, ((rgb >> 8) & 255) / 255f * k), MathF.Min(1, (rgb & 255) / 255f * k), a);

	void Shape(Action<CanvasItem> draw) => _actors.Shapes.Add(draw);

	static Vector2[] RoundRectPoints(Rect2 r, float rad, int seg = 5)
	{
		rad = MathF.Min(rad, MathF.Min(r.Size.X, r.Size.Y) / 2);
		var pts = new List<Vector2>(seg * 4 + 4);
		void Corner(float cx, float cy, float a0)
		{
			for (int i = 0; i <= seg; i++) { float a = a0 + MathF.PI / 2 * i / seg; pts.Add(new Vector2(cx + MathF.Cos(a) * rad, cy + MathF.Sin(a) * rad)); }
		}
		Corner(r.End.X - rad, r.Position.Y + rad, -MathF.PI / 2);
		Corner(r.End.X - rad, r.End.Y - rad, 0);
		Corner(r.Position.X + rad, r.End.Y - rad, MathF.PI / 2);
		Corner(r.Position.X + rad, r.Position.Y + rad, MathF.PI);
		return pts.ToArray();
	}

	static void RoundRect(CanvasItem ci, Rect2 r, float rad, Color c) => ci.DrawColoredPolygon(RoundRectPoints(r, rad), c);

	/// <summary>A rounded box shaded top to bottom, with a thin dark outline: the house style for props.</summary>
	static void Box(CanvasItem ci, Rect2 r, float rad, Color top, Color bottom, float outline = 1.5f)
	{
		var pts = RoundRectPoints(r, rad);
		var cols = new Color[pts.Length];
		for (int i = 0; i < pts.Length; i++) cols[i] = top.Lerp(bottom, Math.Clamp((pts[i].Y - r.Position.Y) / r.Size.Y, 0, 1));
		ci.DrawPolygon(pts, cols);
		if (outline > 0)
		{
			var loop = new Vector2[pts.Length + 1]; pts.CopyTo(loop, 0); loop[^1] = pts[0];
			ci.DrawPolyline(loop, new Color(0, 0, 0, .55f), outline, true);
		}
	}

	static void Limb(CanvasItem ci, Vector2 a, Vector2 b, Color c, float w)
	{
		ci.DrawLine(a, b, c, w, true);
		ci.DrawCircle(a, w / 2, c, true, -1, true);
		ci.DrawCircle(b, w / 2, c, true, -1, true);
	}

	static void Glow(CanvasItem ci, Vector2 at, float r, Color c)
	{
		for (int k = 4; k >= 1; k--) ci.DrawCircle(at, r * k / 4, new Color(c.R, c.G, c.B, c.A * .22f), true, -1, true);
	}

	// ---------- The whole scene, in drawing order ----------

	void DrawScene()
	{
		DrawBase();
		DrawSample();
		DrawTruck();
		DrawLineAndHook();
		if (!_inTruck) { DrawPlayer(); DrawTool(); DrawBubble(); DrawReticle(); DrawStamina(); }
		DrawFlaresAndBits();
	}

	// ---------- The delver ----------

	float _walkPhase;

	void DrawPlayer()
	{
		float x = _playerX, y = _playerY;
		var b = _p.Body;
		float l = Lt(x, y - 4);
		int f = _p.Facing ? 1 : -1;
		Color suit = C(b[Part.Torso] >= Hurt.Hurt ? 0xB0532Eu : 0xD0703Cu, l), suitShade = C(0x8E4424, l);
		Color helmet = C(0xE8E2D4, l), visor = C(0x1C3238, MathF.Max(l, .7f)), boot = C(0x34302C, l), pack = C(0x55604F, l), packShade = C(0x3C4538, l);
		Color LimbCol(Part p, Color fine) => b[p] == Hurt.Fine ? fine : b[p] == Hurt.Hurt ? C(0xD8963A, l) : C(0xC8402E, l);
		bool lamp = _p.LampOn;
		bool sample = _p.CarryingSample;
		var tank = JarLook();

		if (b.Downed)
		{
			Shape(ci =>
			{
				Box(ci, R(x - 2.6f, y - 1.7f, 4.6f, 1.6f), Px(.7f), suit, suitShade);
				Limb(ci, S(x - 2.4f * f, y - .6f), S(x - 4f * f, y - .4f), LimbCol(Part.LeftLeg, suitShade), Px(.85f));
				ci.DrawCircle(S(x + 2.6f * f, y - 1.1f), Px(1.05f), helmet, true, -1, true);
				ci.DrawCircle(S(x + 3f * f, y - 1.2f), Px(.5f), visor, true, -1, true);
			});
			return;
		}

		// Legs: a simple walk cycle when moving on the ground, tucked in the air.
		float speed = MathF.Min(1, MathF.Abs(_p.VX) / 25);
		float sw = _p.Grounded ? MathF.Sin(_walkPhase) * .95f * speed : 0;
		float tuck = _p.Grounded ? 0 : .8f;
		var hip = (x, y - 3.1f);
		(float, float) Foot(float s) => (x + s * f, y - .35f - tuck - MathF.Max(0, MathF.Cos(_walkPhase + (s > 0 ? 0 : MathF.PI))) * .45f * speed);
		(float, float) Knee((float x, float y) foot, float s) => ((hip.Item1 + foot.x) / 2 + .35f * f + s * .1f, (hip.Item2 + foot.y) / 2 - tuck * .3f);
		var fb = Foot(-sw); var ff = Foot(sw);
		var kb = Knee(fb, -1); var kf = Knee(ff, 1);

		// Arms: the front one reaches towards the aim; while climbing both reach up the wall.
		var (ax, ay) = AimPoint(x, y - Player.Height + 2);
		float adx = ax - x, ady = ay - (y - 4.6f), ad = MathF.Max(.01f, MathF.Sqrt(adx * adx + ady * ady));
		var shoulder = (x + .25f * f, y - 4.7f);
		(float, float) hand, elbow, backHand, backElbow;
		if (_p.Clinging)
		{
			int d = _p.ClingDir;
			hand = (x + 1.6f * d, y - 7.2f); elbow = (x + 1.3f * d, y - 5.9f);
			backHand = (x + 1.5f * d, y - 6.2f); backElbow = (x + 1f * d, y - 5.2f);
		}
		else
		{
			hand = (shoulder.Item1 + adx / ad * 2.4f, shoulder.Item2 + ady / ad * 2.4f);
			elbow = (shoulder.Item1 + adx / ad * 1.1f - .1f * f, shoulder.Item2 + ady / ad * 1.1f + .35f);
			backHand = (x - .7f * f - sw * .4f, y - 2.7f); backElbow = (x - .6f * f, y - 3.7f);
		}

		Shape(ci =>
		{
			float limbW = Px(.82f);
			// Back limbs first, a shade darker.
			Limb(ci, S(hip.Item1, hip.Item2), S(kb.Item1, kb.Item2), LimbCol(Part.LeftLeg, suitShade).Darkened(.15f), limbW);
			Limb(ci, S(kb.Item1, kb.Item2), S(fb.Item1, fb.Item2), LimbCol(Part.LeftLeg, suitShade).Darkened(.15f), limbW);
			RoundRect(ci, R(fb.Item1 - .55f + .15f * f, fb.Item2 - .45f, 1.1f, .6f), Px(.25f), boot);
			Limb(ci, S(shoulder.Item1 - .4f * f, shoulder.Item2), S(backElbow.Item1, backElbow.Item2), LimbCol(Part.LeftArm, suitShade).Darkened(.2f), Px(.7f));
			Limb(ci, S(backElbow.Item1, backElbow.Item2), S(backHand.Item1, backHand.Item2), LimbCol(Part.LeftArm, suitShade).Darkened(.2f), Px(.7f));

			// On your back: the glass vacuum tank if you have one, else a plain pack; the sample rides on top.
			if (tank.has)
			{
				Box(ci, R(x - 1.4f * f - .55f, y - 3.1f, 1.1f, .5f), Px(.2f), pack, packShade, 1);           // harness
				DrawJar(ci, S(x - 1.4f * f, y - 4.3f), 1.25f, tank, l);
			}
			else Box(ci, R(f > 0 ? x - 1.85f : x + .75f, y - 5.4f, 1.1f, 2.6f), Px(.35f), pack, packShade, 1.2f);
			if (sample)
			{
				var at = S(x - 1.4f * f, y - 6.6f);
				Glow(ci, at, Px(1.4f), new Color(.44f, .86f, .75f, .8f));
				ci.DrawCircle(at, Px(.4f), new Color(.7f, 1, .92f), true, -1, true);
			}

			// Torso.
			Box(ci, R(x - 1.05f, y - 5.45f, 2.1f, 2.75f), Px(.75f), suit, suitShade, 1.4f);
			ci.DrawLine(S(x - .95f, y - 3.25f), S(x + .95f, y - 3.25f), C(0x3A3430, l), Px(.3f), true);   // belt

			// Front leg.
			Limb(ci, S(hip.Item1, hip.Item2), S(kf.Item1, kf.Item2), LimbCol(Part.RightLeg, suit), limbW);
			Limb(ci, S(kf.Item1, kf.Item2), S(ff.Item1, ff.Item2), LimbCol(Part.RightLeg, suit), limbW);
			RoundRect(ci, R(ff.Item1 - .55f + .15f * f, ff.Item2 - .45f, 1.1f, .6f), Px(.25f), boot);

			// Helmet with visor and headlamp.
			var head = S(x + .1f * f, y - 6.25f);
			ci.DrawCircle(head, Px(1.18f), new Color(0, 0, 0, .5f), true, -1, true);
			ci.DrawCircle(head, Px(1.08f), helmet, true, -1, true);
			RoundRect(ci, new Rect2(head + new Vector2(f > 0 ? Px(.05f) : -Px(1.05f), -Px(.35f)), new Vector2(Px(1f), Px(.75f))), Px(.3f), visor);
			ci.DrawCircle(head + new Vector2(f * Px(.55f), -Px(.15f)), Px(.13f), new Color(1, 1, 1, .55f), true, -1, true);
			var lampAt = head + new Vector2(f * Px(.55f), -Px(.95f));
			ci.DrawCircle(lampAt, Px(.32f), C(0x555048, l), true, -1, true);
			if (lamp) { Glow(ci, lampAt, Px(1.1f), new Color(1, .9f, .6f, .7f)); ci.DrawCircle(lampAt, Px(.2f), new Color(1, .97f, .85f), true, -1, true); }

			// Front arm, and whatever you're holding.
			Limb(ci, S(shoulder.Item1, shoulder.Item2), S(elbow.Item1, elbow.Item2), LimbCol(Part.RightArm, suit), Px(.72f));
			Limb(ci, S(elbow.Item1, elbow.Item2), S(hand.Item1, hand.Item2), LimbCol(Part.RightArm, suit), Px(.72f));
			ci.DrawCircle(S(hand.Item1, hand.Item2), Px(.42f), C(0x3A3632, l), true, -1, true);
		});
	}

	void DrawBubble()
	{
		if (!_bubbleOn || _inTruck) return;
		float cx = _playerX, cy = _playerY - Player.Height / 2f;
		float r = 4.7f + .12f * MathF.Sin(_frame * .2f);
		bool low = _bubbleBattery < 3 && (_frame / 6) % 2 == 0;
		float a = low ? .3f : .7f;
		float g = _frame * .03f;
		Shape(ci =>
		{
			var c = S(cx, cy);
			ci.DrawCircle(c, Px(r), new Color(.55f, .9f, 1f, .09f), true, -1, true);
			ci.DrawArc(c, Px(r), 0, MathF.Tau, 64, new Color(.6f, .93f, 1f, a), 2.2f, true);
			ci.DrawArc(c, Px(r - .35f), -2.6f + MathF.Sin(g) * .2f, -1.6f + MathF.Sin(g) * .2f, 16, new Color(1, 1, 1, a * .8f), 3f, true);
			ci.DrawArc(c, Px(r - .35f), .5f, .9f, 8, new Color(1, 1, 1, a * .35f), 2f, true);
		});
	}

	// ---------- The base ----------

	void DrawBase()
	{
		int g = BaseY, bx = _site.BaseX;
		if (MathF.Abs(_playerX - bx) > BaseHalf + _vw && !_inTruck) return;
		float l = Lt(bx, g - 6);
		Color wood = C(0x7A5A3C, l), woodDark = C(0x54402C, l), metal = C(0x6A757C, l), metalDark = C(0x3E464C, l), steel = C(0x9AA5AC, l);
		int frame = _frame;

		Shape(ci =>
		{
			// Timber posts and a beam along the roof, in the background.
			foreach (int px in new[] { bx - 104, bx - 30, bx + 30, bx + 100 })
			{
				Box(ci, R(px - .7f, g - 26, 1.4f, 26), Px(.2f), C(0x4C3A28, l * .8f), C(0x3A2C1F, l * .8f), 1);
				Box(ci, R(px - 2.2f, g - 26.6f, 4.4f, 1.1f), Px(.2f), C(0x4C3A28, l * .8f), C(0x3A2C1F, l * .8f), 1);
			}

			// Work lamps on cables, with a warm pool of light.
			foreach (int lx in new[] { -100, -50, 0, 50, 100 })
			{
				float x = bx + lx;
				ci.DrawLine(S(x, g - 30), S(x, g - 23.6f), C(0x2A2622, l), 2, true);
				ci.DrawColoredPolygon([S(x - 1.4f, g - 22.6f), S(x - .6f, g - 23.8f), S(x + .6f, g - 23.8f), S(x + 1.4f, g - 22.6f)], C(0x3E3A34, l));
				Glow(ci, S(x, g - 22.4f), Px(4.5f), new Color(1, .85f, .55f, .35f));
				ci.DrawCircle(S(x, g - 22.5f), Px(.55f), new Color(1, .93f, .72f), true, -1, true);
			}

			// Generator: a squat machine with vents, a fuel cap and two status lights.
			float gx = GenX;
			Box(ci, R(gx - 5.5f, g - 7.5f, 11, 7.5f), Px(1), C(0x9A7A48, l), C(0x6E5532, l), 1.8f);
			for (int k = 0; k < 4; k++) ci.DrawLine(S(gx - 4, g - 6 + k * 1.2f), S(gx + 1, g - 6 + k * 1.2f), C(0x3E3020, l), 2, true);
			Box(ci, R(gx - 4.5f, g - 11, 1.4f, 3.6f), Px(.3f), metal, metalDark, 1.2f);
			Box(ci, R(gx - 6.2f, g - 1.2f, 12.4f, 1.2f), Px(.3f), metalDark, C(0x2A3034, l), 1);
			Glow(ci, S(gx + 3, g - 5.5f), Px(1.2f), new Color(.44f, .86f, .75f, .8f));
			ci.DrawCircle(S(gx + 3, g - 5.5f), Px(.38f), new Color(.6f, 1, .9f), true, -1, true);
			var blink = (frame / 20) % 2 == 0 ? new Color(1, .7f, .3f) : new Color(.3f, .25f, .2f);
			ci.DrawCircle(S(gx + 3, g - 3.6f), Px(.38f), blink, true, -1, true);

			// Specimen tanks: glass fronts and metal bands over the real material inside.
			foreach (int tx in new[] { bx - 96, bx - 86 })
			{
				ci.DrawRect(R(tx - 2.6f, g - 9, 5.2f, 9), new Color(.7f, .9f, 1f, .08f));
				ci.DrawLine(S(tx - 1.6f, g - 8.4f), S(tx - 2.2f, g - 2.5f), new Color(1, 1, 1, .35f), 2, true);
				ci.DrawLine(S(tx - .8f, g - 8.4f), S(tx - 1.1f, g - 6), new Color(1, 1, 1, .2f), 1.5f, true);
				Box(ci, R(tx - 3.6f, g - 9.6f, 7.2f, .9f), Px(.3f), steel, metal, 1.2f);
				Box(ci, R(tx - 3.6f, g - 1, 7.2f, 1), Px(.3f), metal, metalDark, 1.2f);
			}

			// Supply locker.
			float lk = LockerX;
			Box(ci, R(lk - 3.2f, g - 11.5f, 6.4f, 11.5f), Px(.5f), C(0x6E7C84, l), C(0x4A555C, l), 1.8f);
			ci.DrawLine(S(lk, g - 11.2f), S(lk, g - .3f), C(0x2C3236, l), 2, true);
			for (int k = 0; k < 3; k++) { ci.DrawLine(S(lk - 2.4f, g - 10.3f + k * .6f), S(lk - .6f, g - 10.3f + k * .6f), C(0x2C3236, l), 1.5f, true); ci.DrawLine(S(lk + .6f, g - 10.3f + k * .6f), S(lk + 2.4f, g - 10.3f + k * .6f), C(0x2C3236, l), 1.5f, true); }
			RoundRect(ci, R(lk - 1.1f, g - 6.6f, .45f, 1.6f), Px(.2f), steel);
			RoundRect(ci, R(lk + .65f, g - 6.6f, .45f, 1.6f), Px(.2f), steel);

			// Cot.
			float cx = CotX;
			ci.DrawLine(S(cx - 6, g - 2.6f), S(cx + 6, g - 2.6f), metalDark, Px(.45f), true);
			foreach (float lx in new[] { -5.6f, 5.6f }) ci.DrawLine(S(cx + lx, g - 2.6f), S(cx + lx, g), metalDark, Px(.4f), true);
			Box(ci, R(cx - 6, g - 4.1f, 12, 1.5f), Px(.6f), C(0x76885F, l), C(0x56663F, l), 1.2f);
			Box(ci, R(cx - 5.7f, g - 4.9f, 2.8f, 1.2f), Px(.5f), C(0xE6DFCC, l), C(0xC2B9A2, l), 1);
			Box(ci, R(cx - 1.5f, g - 4.35f, 7.3f, 1.1f), Px(.5f), C(0x8C5A48, l), C(0x6A3E30, l), 1);

			// Workbench, a vice, and whatever's laid on it.
			float wb = BenchX;
			foreach (float lx in new[] { -6, 6f }) Box(ci, R(wb + lx - .5f, g - 5, 1, 5), Px(.2f), wood, woodDark, 1);
			ci.DrawLine(S(wb - 6, g - 1.8f), S(wb + 6, g - 1.8f), woodDark, Px(.4f), true);
			Box(ci, R(wb - 7.3f, g - 5.7f, 14.6f, 1.2f), Px(.35f), C(0x9C7248, l), C(0x6E4E30, l), 1.4f);
			Box(ci, R(wb + 4.6f, g - 7.2f, 2.2f, 1.6f), Px(.3f), metal, metalDark, 1.2f);
			for (int k = 0; k < _bench.On.Count; k++)
			{
				var c = C(ItemColor(_bench.On[k]), l * 1.1f);
				ci.DrawCircle(S(wb - 4 + k * 3, g - 6.3f), Px(.75f), new Color(0, 0, 0, .4f), true, -1, true);
				ci.DrawCircle(S(wb - 4 + k * 3, g - 6.35f), Px(.65f), c, true, -1, true);
			}

			// Map table: a tilted survey sheet that glows faintly.
			float mx = MapX;
			foreach (float lx in new[] { -5f, 5f }) Box(ci, R(mx + lx - .4f, g - 6, .8f, 6), Px(.2f), wood, woodDark, 1);
			ci.DrawColoredPolygon([S(mx - 6.5f, g - 5.7f), S(mx + 6.5f, g - 7.3f), S(mx + 6.5f, g - 6.2f), S(mx - 6.5f, g - 4.8f)], woodDark);
			ci.DrawColoredPolygon([S(mx - 6f, g - 5.85f), S(mx + 6f, g - 7.3f), S(mx + 6f, g - 6.55f), S(mx - 6f, g - 5.15f)], C(0xD8CCA4, l));
			for (int k = 0; k < 4; k++)
			{
				float t = (k + .5f) / 4;
				var a = S(mx - 5 + t * 10, g - 5.75f - t * 1.3f);
				ci.DrawCircle(a, Px(.18f), (k + frame / 30) % 4 == 0 ? new Color(.44f, .86f, .75f) : C(0x7A6A48, l), true, -1, true);
			}
			ci.DrawPolyline([S(mx - 4.5f, g - 5.55f), S(mx - 2, g - 6.1f), S(mx + 1, g - 6.05f), S(mx + 4.5f, g - 6.9f)], C(0x8A5A3A, l), 1.5f, true);

			// Forge: a stone furnace with a chimney; the mouth glows when it's working.
			float fx = ForgeX;
			Box(ci, R(fx + 1.3f, g - 16.5f, 2.4f, 9), Px(.3f), C(0x4E4642, l), C(0x3A3432, l), 1.4f);
			Box(ci, R(fx - 4.5f, g - 8.5f, 9, 8.5f), Px(1.2f), C(0x6A605A, l), C(0x46403C, l), 1.8f);
			var mouth = R(fx - 2.6f, g - 5.4f, 5.2f, 3.2f);
			if (_forge.Content != null)
			{
				bool hot = _forge.Hot;
				Box(ci, mouth, Px(1.2f), hot ? new Color(1, .75f, .35f) : C(0x7A4A30, l), hot ? new Color(.85f, .3f, .1f) : C(0x4A2A1A, l), 1.2f);
				if (hot) Glow(ci, S(fx, g - 3.8f), Px(5), new Color(1, .55f, .2f, .5f));
				uint ic = _forge.Done ? ItemColor(_forge.Result ?? Item.Junk) : ItemColor(_forge.Content.Value);
				ci.DrawCircle(S(fx, g - 3.4f), Px(.8f), C(ic, hot ? 1.4f : l), true, -1, true);
			}
			else Box(ci, mouth, Px(1.2f), C(0x2A1E18, l), C(0x140E0C, l), 1.2f);

			// Canister rack: one canister per sprayable material; the loaded one is lifted out and lit.
			int perShelf = (SprayList.Length + 1) / 2;
			float rx0 = RackX - perShelf / 2f - 1.2f, rw = perShelf + 2.4f;
			foreach (float sy in new[] { g - 11.2f, g - 5.2f }) Box(ci, R(rx0, sy, rw, .6f), Px(.2f), metal, metalDark, 1);
			foreach (float sx in new[] { rx0, rx0 + rw - .7f }) Box(ci, R(sx, g - 12.5f, .7f, 12.5f), Px(.2f), metal, metalDark, 1);
			for (int k = 0; k < SprayList.Length; k++)
			{
				int shelf = k / perShelf;
				float cxk = RackX - perShelf / 2f + k % perShelf + .5f, bottom = shelf == 0 ? g - 11.2f : g - 5.2f;
				bool on = k == _sprayIndex;
				if (on) bottom -= 1.2f;
				var col = C(MaterialTable.Color[SprayList[k]], on ? 1.25f : l * 1.05f);
				if (on) Glow(ci, S(cxk, bottom - 1.2f), Px(2), new Color(col.R, col.G, col.B, .6f));
				Box(ci, R(cxk - .38f, bottom - 2.4f, .76f, 2.4f), Px(.3f), col.Lightened(.15f), col.Darkened(.25f), 1);
				RoundRect(ci, R(cxk - .25f, bottom - 2.75f, .5f, .45f), Px(.12f), steel);
			}
		});

		// Labels over the stations.
		void Label(float x, float y, string t) => _actors.Texts.Add((S(x, y), t, new Color("e4ddcf")));
		Label(GenX, g - 12.5f, "GENERATOR"); Label(bx - 91, g - 11.5f, "SPECIMEN TANKS"); Label(LockerX, g - 13, "LOCKER");
		Label(CotX, g - 6.5f, "COT"); Label(BenchX, g - 9, "WORKBENCH"); Label(MapX, g - 9.5f, "MAP TABLE");
		Label(ForgeX, g - 18, "FORGE"); Label(RackX, g - 15, "CANISTERS"); Label(bx, g - 31, "SHAFT ↑");
	}

	// ---------- The truck ----------

	void DrawTruck()
	{
		var t = _truck;
		int dir = t.Facing ? 1 : -1;
		float tx = _truckX;
		const int axle = Truck.HalfLength - Truck.WheelInset;
		float ang = MathF.Atan2(_truckFrontY - _truckRearY, 2 * axle), cy = (_truckRearY + _truckFrontY) / 2;
		if (MathF.Abs(tx - _camFX - _vw / 2f) > _vw) return;
		float l = Lt(tx, cy - 6);
		bool sample = _p.CarryingSample && _inTruck;
		var (bx, by) = BoomTip();
		float mx = tx - dir * Truck.HalfLength, baseY = (dir > 0 ? _truckRearY : _truckFrontY) - 5;
		var rear = (x: tx - axle * dir, y: dir > 0 ? _truckRearY : _truckFrontY);
		var front = (x: tx + axle * dir, y: dir > 0 ? _truckFrontY : _truckRearY);

		Shape(ci =>
		{
			// Winch mast and boom.
			Limb(ci, S(mx, baseY), S(mx, by), C(0x8A949A, l), Px(.6f));
			Limb(ci, S(mx, by), S(bx, by), C(0x8A949A, l), Px(.5f));
			ci.DrawCircle(S(bx, by), Px(.45f), C(0xC96A3B, l), true, -1, true);

			// Body, tilted with the wheels: local x along the truck (mirrored to face the way it drives).
			ci.DrawSetTransform(S(tx, cy), ang, new Vector2(dir, 1));
			float HL = Px(Truck.HalfLength);
			Box(ci, new Rect2(-HL, -Px(10), 2 * HL, Px(5.4f)), Px(1), C(0x6F7D66, l), C(0x4B5745, l), 2);
			Box(ci, new Rect2(HL - Px(8.5f), -Px(14.5f), Px(8.5f), Px(5)), Px(1.4f), C(0x7A886F, l), C(0x5A6650, l), 2);
			RoundRect(ci, new Rect2(HL - Px(6.5f), -Px(13.8f), Px(5.2f), Px(2.8f)), Px(.8f), C(0x2C4448, MathF.Max(l, .6f)));
			ci.DrawLine(new Vector2(HL - Px(6), -Px(13.2f)), new Vector2(HL - Px(4.5f), -Px(11.6f)), new Color(1, 1, 1, .3f), 2, true);
			ci.DrawLine(new Vector2(-HL + Px(1), -Px(7.2f)), new Vector2(HL - Px(1), -Px(7.2f)), C(0x3A4436, l), 2, true);
			Glow(ci, new Vector2(HL - Px(.3f), -Px(8)), Px(1.2f), new Color(1, .9f, .6f, .6f));
			if (sample) { Glow(ci, new Vector2(-Px(4), -Px(11)), Px(1.6f), new Color(.44f, .86f, .75f, .8f)); ci.DrawCircle(new Vector2(-Px(4), -Px(11)), Px(.5f), new Color(.7f, 1, .92f), true, -1, true); }
			ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);

			// Wheels.
			foreach (var w in new[] { rear, front })
			{
				var c = S(w.x, w.y - 2);
				ci.DrawCircle(c, Px(2.1f), C(0x24282B, l), true, -1, true);
				ci.DrawCircle(c, Px(1f), C(0x8A9095, l), true, -1, true);
				float spin = _truckX * .6f;
				for (int k = 0; k < 3; k++) { float a = spin + k * 2.094f; ci.DrawLine(c, c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * Px(.9f), C(0x4A5055, l), 2, true); }
			}
		});
	}

	// ---------- Lines, hooks, sample, flares, droplets, dust ----------

	void DrawLineAndHook()
	{
		var (ax0, ay0) = _grappled ? (_hookX, _hookY) : BoomTip();
		var (ex, ey) = _p.Clipped ? (_playerX, _playerY - Player.Height + .5f) : (ax0, MathF.Min(ay0 + _p.RopeLen, _truck.GroundAt(ax0) - 1));
		bool flying = _hookFlying, grappled = _grappled, clipped = _p.Clipped;
		float hx = _hookX, hy = _hookY, px = _playerX, py = _playerY - Player.Height + 2;
		var rope = C(0xD8CFB8, Lt(ax0, ay0));
		Shape(ci =>
		{
			ci.DrawLine(S(ax0, ay0), S(ex, ey), rope, 2, true);
			if (!clipped) ci.DrawCircle(S(ex, ey), Px(.45f), C(0xC96A3B), true, -1, true);
			if (flying) ci.DrawLine(S(px, py), S(hx, hy), rope, 2, true);
			if (flying || grappled)
			{
				var h = S(hx, hy);
				ci.DrawCircle(h, Px(.45f), new Color(.88f, .9f, .92f), true, -1, true);
				for (int k = 0; k < 3; k++) { float a = k * 2.094f + .5f; ci.DrawLine(h, h + new Vector2(MathF.Cos(a), MathF.Sin(a)) * Px(.8f), new Color(.75f, .78f, .8f), 2, true); }
			}
		});
	}

	void DrawSample()
	{
		if (_p.CarryingSample) return;
		float x = _site.SampleX + .5f, y = _site.SampleY - 1.2f, pulse = .8f + .2f * MathF.Sin(_frame * .08f);
		Shape(ci =>
		{
			Glow(ci, S(x, y), Px(3.5f), new Color(.44f, .86f, .75f, .55f * pulse));
			ci.DrawColoredPolygon([S(x, y - 1.3f), S(x + .8f, y), S(x, y + 1.1f), S(x - .8f, y)], new Color(.5f, .95f, .82f));
			ci.DrawColoredPolygon([S(x, y - 1.3f), S(x + .8f, y), S(x, y)], new Color(.8f, 1, .95f));
		});
	}

	void DrawFlaresAndBits()
	{
		// Copy what we need now: the lists change before the layer draws.
		var flares = new List<(float x, float y, float life)>();
		foreach (var f in _flares) flares.Add((f.X, f.Y, f.Life));
		var drops = new List<(float x, float y, Color c)>();
		foreach (var d in _spray) drops.Add((d.X, d.Y, C(MaterialTable.Color[d.M], MathF.Max(.7f, Lt(d.X, d.Y) + MaterialTable.Emission[d.M]) * 1.1f)));
		var bits = new List<(float x, float y, float vx, float vy, float r, Color c, bool spark)>();
		foreach (var b in _bits)
		{
			float a = MathF.Min(1, b.Life / b.Max * 1.6f);
			var c = b.Spark ? new Color(1, .85f, .45f, a) : C(b.Color, Lt(b.X, b.Y), a);
			bits.Add((b.X, b.Y, b.VX, b.VY, b.Size * CellPx / 6f, c, b.Spark));
		}
		Shape(ci =>
		{
			foreach (var (x, y, life) in flares)
			{
				float k = MathF.Min(1, life / 3) * (.85f + .15f * MathF.Sin(_frame * .9f + x));
				Glow(ci, S(x, y), Px(3), new Color(1, .45f, .25f, .6f * k));
				ci.DrawCircle(S(x, y), Px(.45f), new Color(1, .9f, .7f), true, -1, true);
			}
			foreach (var (x, y, c) in drops) ci.DrawCircle(S(x, y), Px(.42f), c, true, -1, true);
			foreach (var (x, y, vx, vy, r, c, spark) in bits)
			{
				if (spark) ci.DrawLine(S(x, y), S(x - vx * .02f, y - vy * .02f), c, 2, true);
				else ci.DrawCircle(S(x, y), MathF.Max(1.2f, r), c, true, -1, true);
			}
		});
	}
}
