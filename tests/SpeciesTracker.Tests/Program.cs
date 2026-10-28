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
