using System;
using System.Collections.Generic;
using System.Numerics;
using SpeciesTracker;

// Lightweight console assertion harness for the pure performance logic.
// Run with: dotnet run --project tests/SpeciesTracker.Tests
// Exits non-zero on the first failed assertion.

var tests = new (string name, Action run)[]
{
    ("WithinRangeSq includes in-range points", () =>
    {
        Assert(TrackerPerf.WithinRangeSq(500f * 500f, 250f * 250f), "250m inside 500m range must be tracked");
        Assert(!TrackerPerf.WithinRangeSq(500f * 500f, 600f * 600f), "600m outside 500m range must be skipped");
        Assert(TrackerPerf.WithinRangeSq(500f * 500f, 500f * 500f), "exact boundary must be tracked");
        Assert(TrackerPerf.WithinRangeSq(150f * 150f, 0f), "zero distance always in range");
    }),

    ("CanCreateMarker respects both caps", () =>
    {
        Assert(TrackerPerf.CanCreateMarker(0, 0), "empty per-user, empty budget -> allow");
        Assert(TrackerPerf.CanCreateMarker(399, 59), "just under both caps -> allow");
        Assert(!TrackerPerf.CanCreateMarker(400, 0), "at per-user ceiling -> deny");
        Assert(!TrackerPerf.CanCreateMarker(401, 0), "over per-user ceiling -> deny");
        Assert(!TrackerPerf.CanCreateMarker(0, 60), "at per-tick budget -> deny");
        Assert(!TrackerPerf.CanCreateMarker(0, 61), "over per-tick budget -> deny");
    }),

    ("Marker keys keep the legacy formats (no flicker on upgrade)", () =>
    {
        Assert(TrackerPerf.AnimalKey(123) == "a123", "animal key must match legacy \"a<ID>\"");
        Assert(TrackerPerf.AnimalKey(0) == "a0", "animal key for ID 0");
        var p = new Vector3(12.5f, 3.75f, -2f);
        var legacy = "p" + p.X + "," + p.Y + "," + p.Z;
        Assert(TrackerPerf.PlantKey(p) == legacy, "plant key must match legacy \"p<X>,<Y>,<Z>\" format");
        Assert(TrackerPerf.PlantKey(new Vector3(1f, 2f, 3f)) == "p1,2,3", "plant key plain values");
    }),

    ("Fingerprint detects list changes and is stable for equal lists", () =>
    {
        var a = new List<string> { "Deer", "Bison", "Oak Tree" };
        var b = new List<string> { "Deer", "Bison", "Oak Tree" };
        var c = new List<string> { "Deer", "Bison" };
        var d = new List<string> { "Deer", "Bison", "Oak" };
        var e = new List<string> { "deer", "bison", "oak tree" }; // case change -> set semantics same (OrdinalIgnoreCase)

        Assert(TrackerPerf.Fingerprint(a) == TrackerPerf.Fingerprint(b), "same contents -> same fingerprint");
        Assert(TrackerPerf.Fingerprint(a) != TrackerPerf.Fingerprint(c), "removing an entry changes fingerprint");
        Assert(TrackerPerf.Fingerprint(a) != TrackerPerf.Fingerprint(d), "changing an entry changes fingerprint");
        // A case-only change yields a different fingerprint (cache rebuilds once) but the
        // hash set contents are identical, so behavior is preserved.
        Assert(TrackerPerf.Fingerprint(a) != TrackerPerf.Fingerprint(e), "case change alters fingerprint (harmless rebuild)");
        Assert(TrackerPerf.Fingerprint(Array.Empty<string>()) != 0UL, "empty list fingerprint is non-zero (never collides with missing-cache marker)");
    }),

    ("Squared-distance path is numerically equivalent to plain distance comparison", () =>
    {
        var range = 500f;
        var rangeSq = range * range;
        for (var d = 0f; d <= 600f; d += 7.3f)
        {
            var expected = d <= range;
            var actual = TrackerPerf.WithinRangeSq(rangeSq, d * d);
            Assert(actual == expected, $"mismatch at distance {d}: expected {expected}, got {actual}");
        }
    }),

    ("Animal move threshold only triggers after real movement", () =>
    {
        Assert(!TrackerPerf.ShouldUpdateAnimalMarker(0f), "zero distance -> no update");
        Assert(!TrackerPerf.ShouldUpdateAnimalMarker(1.9f * 1.9f), "just under 2m -> no update");
        Assert(!TrackerPerf.ShouldUpdateAnimalMarker(2f * 2f), "exactly 2m -> no update (strict >)");
        Assert(TrackerPerf.ShouldUpdateAnimalMarker(2.01f * 2.01f), "just over 2m -> update");
        Assert(TrackerPerf.ShouldUpdateAnimalMarker(50f * 50f), "50m jump -> update");
    }),

    ("SpatialHashGrid returns only animals in queried cells", () =>
    {
        var grid = new SpatialHashGrid<string>(100f); // 100-unit cells, no wrap
        grid.Add(new Vector3(10f, 0f, 10f), "near");
        grid.Add(new Vector3(250f, 0f, 10f), "mid-cell");
        grid.Add(new Vector3(510f, 0f, 510f), "far-cell");
        grid.Add(new Vector3(1010f, 0f, -1010f), "diagonal");

        var hits = new List<string>(grid.Query(new Vector3(0f, 0f, 0f), 250f));
        Assert(hits.Contains("near"), "near must be found");
        Assert(hits.Contains("mid-cell"), "cell edge (250) must be found when span includes it");
        Assert(!hits.Contains("far-cell"), "510 units away must NOT be in a 250 range query");
        Assert(!hits.Contains("diagonal"), "diagonal beyond range must NOT be found");
    }),

    ("SpatialHashGrid wraps cells at world edges", () =>
    {
        // 4x4 grid of 100-unit cells -> world spans [0, 400) on both axes.
        var grid = new SpatialHashGrid<string>(100f, cellsX: 4, cellsZ: 4);
        grid.Add(new Vector3(10f, 0f, 10f), "corner-near");
        grid.Add(new Vector3(390f, 0f, 390f), "corner-far"); // wraps to cell (-1,-1) == (3,3)

        var hits = new List<string>(grid.Query(new Vector3(390f, 0f, 390f), 100f));
        Assert(hits.Contains("corner-far"), "far corner animal must be found near its own location");
        // In a wrapped world (10,10) is only 20 units away across the edge, so it MUST appear as
        // a candidate; the exact wrapped-distance filter is the caller's responsibility.
        Assert(hits.Contains("corner-near"), "wrapped world: cross-edge neighbor must be a candidate");

        // Cross-edge query: center near right edge, radius reaches the wrapped left side.
        var cross = new List<string>(grid.Query(new Vector3(390f, 0f, 10f), 30f));
        Assert(cross.Contains("corner-far"), "wrapped neighbor on the far side must be found across the edge (span 1 covers it)");
    }),

    ("SpatialHashGrid negative coordinates bucket correctly", () =>
    {
        var grid = new SpatialHashGrid<string>(100f);
        grid.Add(new Vector3(-5f, 0f, -5f), "negative");
        var hits = new List<string>(grid.Query(new Vector3(-5f, 0f, -5f), 10f));
        Assert(hits.Count == 1 && hits[0] == "negative", "negative-coordinate animal must be found at its own cell");
        // Cells are coarse (100u), so a 10u query still scans the surrounding ring (conservative
        // pruning); something in a non-visited cell must NOT appear.
        var miss = new List<string>(grid.Query(new Vector3(-205f, 0f, -5f), 10f));
        Assert(miss.Count == 0, "animal 200 units away in a non-visited cell must not appear");
    }),
};

var failures = 0;
foreach (var t in tests)
{
    try
    {
        t.run();
        Console.WriteLine($"PASS  {t.name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL  {t.name}: {ex.Message}");
    }
}

Console.WriteLine(failures == 0
    ? $"\nAll {tests.Length} test groups passed."
    : $"\n{failures} test group(s) FAILED.");
return failures == 0 ? 0 : 1;

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
