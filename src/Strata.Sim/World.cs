using System.Runtime.CompilerServices;

namespace Strata.Sim;

/// <summary>
/// The falling-sand world. Plain C#, no engine types, so it can be tested and benchmarked on its own and swapped
/// for a native implementation behind the same surface later.
///
/// Layout: one flat array per field (material, life, shade) over the whole world, split logically into
/// 64×64 chunks. Each chunk keeps a dirty rectangle: only cells inside it are visited, and a chunk with an empty
/// rectangle sleeps. Chunks are processed in four checkerboard passes (even/even, odd/even, even/odd, odd/odd), so
/// chunks within one pass never touch each other's cells and can later run on separate threads. Each chunk draws
/// from its own random stream seeded by (world seed, tick, chunk), so results do not depend on scheduling.
/// </summary>
public sealed partial class World
{
    public const int ChunkSize = 64;
    const int ChunkShift = 6;

    public int Width { get; }
    public int Height { get; }
    public int ChunksX { get; }
    public int ChunksY { get; }
    public ulong Seed { get; }
    public long Tick { get; private set; }

    /// <summary>Cells visited in the last <see cref="Step"/>; a measure of how much work dirty rects saved.</summary>
    public int CellsVisitedLastStep { get; private set; }

    /// <summary>Chunks that had a non-empty dirty rectangle in the last step.</summary>
    public int AwakeChunksLastStep { get; private set; }

    readonly byte[] _mat;
    readonly byte[] _life;
    readonly byte[] _shade;
    readonly byte[] _moved;
    byte _stamp = 1;

    // Dirty rectangles in world coordinates, inclusive. Empty when Max < Min.
    readonly int[] _curMinX, _curMinY, _curMaxX, _curMaxY;
    readonly int[] _nxtMinX, _nxtMinY, _nxtMaxX, _nxtMaxY;
    readonly uint[] _version;
    readonly long[] _versionTick;

    public World(int chunksX, int chunksY, ulong seed)
    {
        if (chunksX <= 0 || chunksY <= 0) throw new ArgumentOutOfRangeException(nameof(chunksX));
        ChunksX = chunksX; ChunksY = chunksY; Seed = seed;
        Width = chunksX * ChunkSize; Height = chunksY * ChunkSize;
        int n = Width * Height, c = chunksX * chunksY;
        _mat = new byte[n]; _life = new byte[n]; _shade = new byte[n]; _moved = new byte[n];
        _curMinX = new int[c]; _curMinY = new int[c]; _curMaxX = new int[c]; _curMaxY = new int[c];
        _nxtMinX = new int[c]; _nxtMinY = new int[c]; _nxtMaxX = new int[c]; _nxtMaxY = new int[c];
        _version = new uint[c]; _versionTick = new long[c];
        for (int i = 0; i < c; i++) { ClearRect(_curMinX, _curMinY, _curMaxX, _curMaxY, i); ClearRect(_nxtMinX, _nxtMinY, _nxtMaxX, _nxtMaxY, i); _versionTick[i] = -1; }
        for (int i = 0; i < n; i++) _shade[i] = (byte)Rng.Mix(seed, (ulong)i, 7);
    }

    // ---------- Read access for renderers, physics and networking ----------

    /// <summary>Material id per cell, row-major (index = y * Width + x).</summary>
    public ReadOnlySpan<byte> Materials => _mat;

    /// <summary>Per-cell shade 0..255 for visual variation. Not simulated state; safe to derive elsewhere.</summary>
    public ReadOnlySpan<byte> Shades => _shade;

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public byte Get(int x, int y) => InBounds(x, y) ? _mat[y * Width + x] : Mat.Bedrock;

    /// <summary>Remaining life of a cell (fire burn time, gas lifetime, grey lace reach, shale about to give way).</summary>
    public byte LifeAt(int x, int y) => InBounds(x, y) ? _life[y * Width + x] : (byte)0;

    /// <summary>Increments whenever any cell in the chunk changed during a step or through <see cref="Set"/>.</summary>
    public uint ChunkVersion(int cx, int cy) => _version[cy * ChunksX + cx];

    public bool ChunkAwake(int cx, int cy, out int minX, out int minY, out int maxX, out int maxY)
    {
        int c = cy * ChunksX + cx;
        minX = _curMinX[c]; minY = _curMinY[c]; maxX = _curMaxX[c]; maxY = _curMaxY[c];
        return maxX >= minX;
    }

    /// <summary>Copies one chunk's materials into <paramref name="dst"/> (ChunkSize*ChunkSize bytes, row-major).</summary>
    public void CopyChunk(int cx, int cy, Span<byte> dst)
    {
        if (dst.Length < ChunkSize * ChunkSize) throw new ArgumentException("Destination too small", nameof(dst));
        int x0 = cx * ChunkSize, y0 = cy * ChunkSize;
        for (int y = 0; y < ChunkSize; y++)
            _mat.AsSpan((y0 + y) * Width + x0, ChunkSize).CopyTo(dst.Slice(y * ChunkSize, ChunkSize));
    }

    // ---------- Write access for world generation, tools and players ----------

    /// <summary>Sets a cell from outside the simulation (generation, digging, painting) and wakes its area.</summary>
    public void Set(int x, int y, byte material, byte life = 0)
    {
        if (!InBounds(x, y) || material >= Mat.Count) return;
        int i = y * Width + x;
        _mat[i] = material; _life[i] = life;
        Touch(x, y);
    }

    /// <summary>Fills a disc. Used by tools and tests.</summary>
    public void Paint(int cx, int cy, int radius, byte material, byte life = 0, bool onlyIntoEmpty = false)
    {
        for (int y = cy - radius; y <= cy + radius; y++)
        for (int x = cx - radius; x <= cx + radius; x++)
        {
            int dx = x - cx, dy = y - cy;
            if (dx * dx + dy * dy > radius * radius || !InBounds(x, y)) continue;
            if (onlyIntoEmpty && _mat[y * Width + x] != Mat.Empty) continue;
            Set(x, y, material, life == 0 ? DefaultLife(material) : life);
        }
    }

    /// <summary>Starting life for materials that use it (fire burn time, gas lifetime, mycelium reach).</summary>
    public static byte DefaultLife(byte m) => MaterialTable.DefaultLife[m];

    // ---------- Stepping ----------

    public void Step()
    {
        if (++_stamp == 0) { Array.Clear(_moved); _stamp = 1; }
        Tick++;
        Events.Clear();
        int chunks = ChunksX * ChunksY;
        for (int c = 0; c < chunks; c++)
        {
            _curMinX[c] = _nxtMinX[c]; _curMinY[c] = _nxtMinY[c]; _curMaxX[c] = _nxtMaxX[c]; _curMaxY[c] = _nxtMaxY[c];
            ClearRect(_nxtMinX, _nxtMinY, _nxtMaxX, _nxtMaxY, c);
        }

        int visited = 0, awake = 0;
        for (int pass = 0; pass < 4; pass++)
        {
            int px = pass & 1, py = pass >> 1;
            for (int cy = ChunksY - 1; cy >= 0; cy--)
            {
                if ((cy & 1) != py) continue;
                for (int cx = 0; cx < ChunksX; cx++)
                {
                    if ((cx & 1) != px) continue;
                    int c = cy * ChunksX + cx;
                    if (_curMaxX[c] < _curMinX[c]) continue;
                    awake++;
                    visited += StepChunk(c);
                }
            }
        }
        CellsVisitedLastStep = visited;
        AwakeChunksLastStep = awake;

        // Slow chemistry in still places: every chunk is swept for reactions every few ticks (with the chance
        // scaled up to match), so salt still dissolves in a quiet pool and ice still creeps across still water,
        // without keeping every lake awake every tick.
        for (int c = 0; c < chunks; c++)
            if ((c + Tick) % SweepPeriod == 0) SweepChunk(c);   // awake chunks too: they only update their busy corner
    }

    const int SweepPeriod = 16;
    static readonly bool[] Reactive = BuildReactive();
    static bool[] BuildReactive() { var r = new bool[Mat.Count]; for (byte m = 0; m < Mat.Count; m++) r[m] = Reactions.HasAny(m) && m != Mat.Empty; return r; }

    void SweepChunk(int c)
    {
        var rng = new Rng(Rng.Mix(Seed, (ulong)Tick, (ulong)c + 0x5EED));
        int x0 = (c % ChunksX) * ChunkSize, y0 = (c / ChunksX) * ChunkSize;
        for (int y = y0; y < y0 + ChunkSize; y++)
        for (int x = x0; x < x0 + ChunkSize; x++)
        {
            byte m = _mat[y * Width + x];
            // Slow growth (grey lace, moss, roots, fungus) also runs here, at a sixteenth of a busy cave's pace.
            if (m is Mat.Mycelium or Mat.Moss or Mat.Root or Mat.Fungus or Mat.Lava or Mat.Sunstone or Mat.Ghostwater) Update(ref rng, x, y, y * Width + x, m);
            else if (Reactive[m]) React(ref rng, x, y, m, SweepPeriod);
        }
    }

    int StepChunk(int c)
    {
        var rng = new Rng(Rng.Mix(Seed, (ulong)Tick, (ulong)c));
        int minX = _curMinX[c], maxX = _curMaxX[c], minY = _curMinY[c], maxY = _curMaxY[c];
        int visited = 0;
        for (int y = maxY; y >= minY; y--)
        {
            bool ltr = (rng.Next() & 1) == 0;
            for (int k = minX; k <= maxX; k++)
            {
                int x = ltr ? k : maxX - (k - minX);
                int i = y * Width + x;
                visited++;
                if (_moved[i] == _stamp) continue;
                byte m = _mat[i];
                if (MaterialTable.Inert[m]) continue;
                _updating = m;
                if (MaterialTable.AlwaysActive[m]) MarkNext(x, y);
                Update(ref rng, x, y, i, m);
                _updating = 255;
            }
        }
        return visited;
    }

    // ---------- Cell operations used by the rules ----------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void SetCell(ref Rng rng, int x, int y, byte m, byte life)
    {
        int i = y * Width + x;
        _mat[i] = m; _life[i] = life; _shade[i] = (byte)rng.Next();
        _moved[i] = _stamp;
        Touch(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Swap(int x0, int y0, int x1, int y1)
    {
        int i = y0 * Width + x0, j = y1 * Width + x1;
        (_mat[i], _mat[j]) = (_mat[j], _mat[i]);
        (_life[i], _life[j]) = (_life[j], _life[i]);
        (_shade[i], _shade[j]) = (_shade[j], _shade[i]);
        _moved[i] = _stamp; _moved[j] = _stamp;
        Touch(x0, y0); Touch(x1, y1);
    }

    /// <summary>A cell changed: wake the 3×3 area around it for the next step and bump its chunk's version.</summary>
    void Touch(int x, int y)
    {
        MarkNext(x, y);
        int c = (y >> ChunkShift) * ChunksX + (x >> ChunkShift);
        if (_versionTick[c] != Tick) { _versionTick[c] = Tick; _version[c]++; }
    }

    /// <summary>
    /// Grows the next-step dirty rectangle of every chunk overlapping the 3×3 box around (x, y).
    /// Note for threading: two chunks in the same pass can mark a shared neighbour, so this needs per-thread
    /// accumulation (or an interlocked min/max) before chunks run in parallel.
    /// </summary>
    /// <summary>Diagnostics: how many wake-ups each material caused in the last steps (cleared by the caller).</summary>
    public readonly int[] WakesBy = new int[Mat.Count];
    byte _updating = 255;

    void MarkNext(int x, int y)
    {
        if (_updating != 255) WakesBy[_updating]++;
        int x0 = Math.Max(0, x - 1), x1 = Math.Min(Width - 1, x + 1);
        int y0 = Math.Max(0, y - 1), y1 = Math.Min(Height - 1, y + 1);
        int cx0 = x0 >> ChunkShift, cx1 = x1 >> ChunkShift, cy0 = y0 >> ChunkShift, cy1 = y1 >> ChunkShift;
        for (int cy = cy0; cy <= cy1; cy++)
        for (int cx = cx0; cx <= cx1; cx++)
        {
            int c = cy * ChunksX + cx;
            int bx0 = Math.Max(x0, cx << ChunkShift), bx1 = Math.Min(x1, ((cx + 1) << ChunkShift) - 1);
            int by0 = Math.Max(y0, cy << ChunkShift), by1 = Math.Min(y1, ((cy + 1) << ChunkShift) - 1);
            if (bx0 < _nxtMinX[c]) _nxtMinX[c] = bx0;
            if (bx1 > _nxtMaxX[c]) _nxtMaxX[c] = bx1;
            if (by0 < _nxtMinY[c]) _nxtMinY[c] = by0;
            if (by1 > _nxtMaxY[c]) _nxtMaxY[c] = by1;
        }
    }

    static void ClearRect(int[] minX, int[] minY, int[] maxX, int[] maxY, int c)
    {
        minX[c] = int.MaxValue; minY[c] = int.MaxValue; maxX[c] = int.MinValue; maxY[c] = int.MinValue;
    }
}
