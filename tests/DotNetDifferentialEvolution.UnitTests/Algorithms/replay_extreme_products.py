"""Replay the SHADE and L-SHADE constant and mixed matrices with the checkout's CPU library."""

import math
from fractions import Fraction
from pathlib import Path
import random
import subprocess
import tempfile
from xml.sax.saxutils import quoteattr


# Each row names the strategy followed by one (weight exponent, trial multiplier,
# F exponent, CR exponent) tuple per improved record. Remaining records stay default.
CONSUMER = """
using System.Globalization;
using System.Reflection;
using DotNetDifferentialEvolution.Algorithms.Lshade;
using DotNetDifferentialEvolution.Algorithms.Shade;
using DotNetDifferentialEvolution.GenerationStrategies;
using DotNetDifferentialEvolution.Models;
using DotNetDifferentialEvolution.SelectionStrategies;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
const BindingFlags slotFields = BindingFlags.Instance | BindingFlags.NonPublic;
var memoryF = typeof(ShadeStrategy).GetField("_memoryF", slotFields)!;
var memoryCr = typeof(ShadeStrategy).GetField("_memoryCr", slotFields)!;
while (Console.ReadLine() is { } line)
{
    var values = line.Split(' ').Select(int.Parse).ToArray();
    var records = new TrialRecord[4];
    for (int k = 1; k < values.Length; k += 4)
    {
        double parent = Math.ScaleB(1, values[k]);
        records[(k - 1) / 4] = new TrialRecord
        {
            Outcome = SelectionOutcome.TrialImproved,
            ParentFfValue = parent,
            TrialFfValue = values[k + 1] * parent,
            UsedF = Math.ScaleB(1, values[k + 2]),
            UsedCr = Math.ScaleB(1, values[k + 3]),
        };
    }

    var context = new ProblemContext(4, 1, 1, new[] { 0d }, new[] { 1d }, null!, null!,
        new double[4], new double[4], new double[4], new double[4]);
    ShadeStrategy strategy = values[0] == 0 ? new ShadeStrategy(4, 1) : new LShadeStrategy(4, 100, 0, 1);
    strategy.AfterGeneration(new GenerationContext(context), records);
    double f = ((double[])memoryF.GetValue(strategy)!)[0];
    double cr = ((double[])memoryCr.GetValue(strategy)!)[0];
    Console.WriteLine($"{f:R} {cr:R}");
}
"""


def input_row(lshade, records):
    return " ".join(map(str, [lshade, *(value for record in records for value in record)]))


def constant_cases():
    for lshade in (0, 1):
        for w in (-1074, -1022, -970, -969, -900, -500, 0, 500, 1023):
            for p in (-1074, -1022, -600, -250, -54, -20, -4, 0):
                # Improvements are 2**w and 2**(w+1), even when the latter overflows.
                row = input_row(lshade, [(w, 0, p, p), (w, -1, p, p)])
                parameter = math.ldexp(1.0, p)
                yield row, (parameter, parameter)


def mixed_cases():
    rng = random.Random(53247)
    sets = [[(1000, -1074, -1074), (-1074, 0, 0), (-900, -250, -40), (30, -600, -900)]]
    for _ in range(192):
        sets.append([(rng.randint(-1074, 1023), -rng.randint(0, 1074), -rng.randint(0, 1074))
                     for _ in range(4)])

    two = Fraction(2)
    for triples in sets:
        weights, forces, probabilities = zip(*[(two**w, two**f, two**cr) for w, f, cr in triples])
        records = [(w, 0, f, cr) for w, f, cr in triples]
        for lshade in (0, 1):
            yield input_row(lshade, records), (mean(weights, forces, True), mean(weights, probabilities, lshade))


def mean(weights, parameters, lehmer):
    first = sum(w * p for w, p in zip(weights, parameters))
    if lehmer:
        return float(sum(w * p * p for w, p in zip(weights, parameters)) / first)
    return float(first / sum(weights))


def run_consumer(rows):
    root = Path(__file__).resolve().parents[3]
    library = root / "src/DotNetDifferentialEvolution/DotNetDifferentialEvolution.csproj"
    with tempfile.TemporaryDirectory() as directory:
        work = Path(directory)
        (work / "Replay.csproj").write_text(f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <RollForward>Major</RollForward>
  </PropertyGroup>
  <ItemGroup><ProjectReference Include={quoteattr(str(library))} /></ItemGroup>
</Project>
""", encoding="utf-8")
        (work / "Program.cs").write_text(CONSUMER, encoding="utf-8")
        subprocess.run(["dotnet", "build", str(work / "Replay.csproj"), "-c", "Release"], cwd=root, check=True)
        run = subprocess.run(["dotnet", str(work / "bin/Release/net8.0/Replay.dll")], cwd=root,
                             input="\n".join(rows) + "\n", stdout=subprocess.PIPE, text=True, check=True, timeout=60)
        return run.stdout.splitlines()


def main():
    constant = list(constant_cases())
    mixed = list(mixed_cases())
    if (len(constant), len(mixed)) != (144, 386):
        raise ValueError("Expected 144 constant cases and 193 mixed sets for two strategies")
    cases = constant + mixed
    lines = run_consumer(row for row, _ in cases)
    if len(lines) != len(cases):
        raise ValueError(f"Expected {len(cases)} output pairs, got {len(lines)}")

    constant_failures = mixed_failures = 0
    for i, ((row, expected), line) in enumerate(zip(cases, lines)):
        f, cr = map(float, line.split())
        if i < len(constant):
            if (f, cr) != expected:
                constant_failures += 1
                print(f"{row} | (F, CR): {(f, cr)} vs {expected}")
        else:
            for field, actual, reference in zip(("F", "CR"), (f, cr), expected):
                if not math.isfinite(actual) or abs(actual - reference) > 3 * math.ulp(reference):
                    mixed_failures += 1
                    print(f"{row} | {field}: {actual!r} vs {reference!r}")
    print(f"{constant_failures} mismatches / {len(constant)} equal-parameter cases")
    print(f"{mixed_failures} deviations >3 ULP or nonfinite / {2 * len(mixed)} mixed memory values")
    return bool(constant_failures or mixed_failures)


if __name__ == "__main__":
    raise SystemExit(main())
