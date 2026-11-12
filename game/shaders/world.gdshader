// The cave world, drawn smoothly instead of as square cells.
//
// The game uploads one texel per cell: rgb is the lit material colour, alpha says what kind of cell it is.
//   alpha   0  open cave: rgb is the light level there (drawn as the back wall)
//   alpha  16  sky: rgb is the sky colour, drawn as is
//   alpha  64  gas      alpha 128  liquid      alpha 192  powder      alpha 255  solid
// Each kind becomes a smooth field (bilinear with a smoothstep, so outlines are rounded, not stair-stepped), and
// the fields are layered: back wall, gas, liquid, then rock on top. Rock gets painted texture in world space, a
// darker rim, a lit top edge, and soft shadow on the back wall around it.
shader_type canvas_item;

uniform sampler2D cells : filter_nearest;   // the cell texture (same as the sprite's)
uniform vec2 cam;          // world cell of texel (0, 0)
uniform float time;
uniform float cell_px = 8.0;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p)
{
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}
float fbm(vec2 p)
{
	float v = 0.0, a = 0.5;
	for (int k = 0; k < 4; k++) { v += a * vnoise(p); p = p * 2.03 + 17.0; a *= 0.5; }
	return v;
}

vec4 cell(ivec2 c, ivec2 size) { return texelFetch(cells, clamp(c, ivec2(0), size - 1), 0); }
float isSolid(vec4 t) { return step(0.7, t.a); }                       // solid or powder
float isLiquid(vec4 t) { return step(0.4, t.a) * (1.0 - step(0.6, t.a)); }
float isGas(vec4 t) { return step(0.2, t.a) * (1.0 - step(0.4, t.a)); }
float isSky(vec4 t) { return step(0.03, t.a) * (1.0 - step(0.2, t.a)); }

// Smooth field of one kind at a point (in cell units), plus the colour of that kind blended from the 4 cells.
float field(vec2 p, ivec2 size, int kind, out vec3 col)
{
	vec2 q = p - 0.5;
	ivec2 i0 = ivec2(floor(q));
	vec2 f = q - floor(q);
	f = f * f * (3.0 - 2.0 * f);
	vec4 t00 = cell(i0, size), t10 = cell(i0 + ivec2(1, 0), size), t01 = cell(i0 + ivec2(0, 1), size), t11 = cell(i0 + ivec2(1, 1), size);
	float s00, s10, s01, s11;
	if (kind == 0) { s00 = isSolid(t00); s10 = isSolid(t10); s01 = isSolid(t01); s11 = isSolid(t11); }
	else if (kind == 1) { s00 = isLiquid(t00); s10 = isLiquid(t10); s01 = isLiquid(t01); s11 = isLiquid(t11); }
	else { s00 = isGas(t00); s10 = isGas(t10); s01 = isGas(t01); s11 = isGas(t11); }
	float w00 = (1.0 - f.x) * (1.0 - f.y), w10 = f.x * (1.0 - f.y), w01 = (1.0 - f.x) * f.y, w11 = f.x * f.y;
	float s = s00 * w00 + s10 * w10 + s01 * w01 + s11 * w11;
	vec3 c = t00.rgb * s00 * w00 + t10.rgb * s10 * w10 + t01.rgb * s01 * w01 + t11.rgb * s11 * w11;
	col = s > 0.0001 ? c / s : vec3(0.0);
	return s;
}

float kindOf(vec4 t, int kind) { return kind == 0 ? isSolid(t) : kind == 1 ? isLiquid(t) : isGas(t); }

// The same field over a wider, round kernel (4x4 cells): bulk shapes get organic, rounded outlines and colours
// blend softly. Combined with the tight field so single grains and one-cell-thin features still show.
float smoothField(vec2 p, ivec2 size, int kind, out vec3 col)
{
	vec3 tightCol;
	float tight = field(p, size, kind, tightCol);
	ivec2 b = ivec2(floor(p - 1.5));
	float s = 0.0, wsum = 0.0;
	vec3 c = vec3(0.0);
	for (int j = 0; j < 4; j++)
		for (int i = 0; i < 4; i++)
		{
			ivec2 q = b + ivec2(i, j);
			vec2 d = vec2(q) + 0.5 - p;
			float w = max(0.0, 1.0 - dot(d, d) / 2.9);
			w *= w;
			vec4 t = cell(q, size);
			float k = kindOf(t, kind);
			s += k * w; c += t.rgb * k * w; wsum += w;
		}
	float wide = s / max(wsum, 0.0001);
	col = s > 0.0001 ? c / s : tightCol;
	// Outlines follow the round kernel, but never lose a cell the tight field shows clearly.
	return max(wide, tight * smoothstep(0.55, 0.9, tight));
}

void fragment()
{
	ivec2 size = textureSize(cells, 0);
	vec2 p = UV * vec2(size);              // position in cells within the texture
	vec2 wp = cam + p;                     // world position in cells
	vec4 here = cell(ivec2(floor(p)), size);

	if (isSky(here) > 0.5) { COLOR = vec4(here.rgb, 1.0); }
	else
	{
		vec3 solidCol, liqCol, gasCol, dummy;
		float S = smoothField(p, size, 0, solidCol);
		float L = smoothField(p, size, 1, liqCol);
		float G = smoothField(p, size, 2, gasCol);
		float aa = max(fwidth(S), 0.02) * 0.9;

		// Back wall: dark rock with large blotches and fine grain, lit by the light where you stand, shadowed near rock.
		float light = here.a < 0.03 ? here.r : 0.62;
		float bgN = fbm(wp * 0.09) * 0.6 + fbm(wp * 0.6 + 40.0) * 0.4;
		vec3 wall = mix(vec3(0.075, 0.062, 0.052), vec3(0.16, 0.135, 0.11), bgN);
		float occl = 0.0;
		for (int k = 0; k < 8; k++)
		{
			float a = float(k) * 0.785;
			vec3 d;
			occl += field(p + vec2(cos(a), sin(a)) * 2.2, size, 0, d);
		}
		wall *= 1.0 - occl / 8.0 * 0.55;
		vec3 col = wall * clamp(light * 1.9, 0.08, 1.4);

		// Gas: soft and see-through, drifting.
		float gA = smoothstep(0.15, 0.85, G) * (0.45 + 0.25 * fbm(wp * 0.35 + vec2(time * 0.3, -time * 0.2)));
		col = mix(col, gasCol, gA);

		// Liquid: smooth surface, gentle ripple, a bright line along the top.
		vec3 lu;
		float Lup = smoothField(p - vec2(0.0, 0.8), size, 1, lu);
		float lA = smoothstep(0.5 - aa, 0.5 + aa, L);
		vec3 lc = liqCol * (0.9 + 0.12 * vnoise(wp * vec2(0.5, 1.5) + vec2(time * 0.8, 0.0)));
		lc += vec3(0.18) * smoothstep(0.25, 0.0, Lup) * lA;
		col = mix(col, lc, lA * 0.88);

		// Rock: painted texture, strata, darker rim, light catching the top edges.
		float sA = smoothstep(0.5 - aa, 0.5 + aa, S);
		bool powder = here.a > 0.7 && here.a < 0.9;
		float grain = powder ? vnoise(wp * 2.2) : fbm(wp * 0.8);
		float strata = vnoise(vec2(wp.x * 0.06, wp.y * 0.9));
		vec3 rc = solidCol * (0.82 + 0.28 * grain) * (0.94 + 0.12 * strata);
		vec3 su;
		float Sup = smoothField(p - vec2(0.0, 1.0), size, 0, su);
		float rim = 1.0 - smoothstep(0.5, 0.95, S);
		rc *= 1.0 - rim * 0.45;
		rc += solidCol * 0.35 * smoothstep(0.6, 0.1, Sup) * smoothstep(0.5, 0.75, S);
		col = mix(col, rc, sA);

		COLOR = vec4(col, 1.0);
	}
}
