# DotNetDifferentialEvolution

[![NuGet](https://img.shields.io/nuget/v/DotNetDifferentialEvolution.svg)](https://www.nuget.org/packages/DotNetDifferentialEvolution/)
[![Downloads](https://img.shields.io/nuget/dt/DotNetDifferentialEvolution.svg)](https://www.nuget.org/packages/DotNetDifferentialEvolution/)
[![CI](https://github.com/baryon-asymm/DotNetDifferentialEvolution/actions/workflows/ci.yml/badge.svg)](https://github.com/baryon-asymm/DotNetDifferentialEvolution/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/baryon-asymm/DotNetDifferentialEvolution/blob/main/LICENSE)

## Introduction

Differential Evolution (DE) is a stochastic optimization algorithm used for finding global minima or maxima of functions in multi-dimensional spaces.
It was introduced by Kenneth Price and Rainer Storn in 1997.
DE is known for its simplicity and effectiveness, especially for complex optimization problems. For more details on the algorithm, you can refer to the [Wikipedia page](https://en.wikipedia.org/wiki/Differential_evolution).

📐 **[Algorithms, formulas, and where they live in the code](https://github.com/baryon-asymm/DotNetDifferentialEvolution/blob/main/docs/ALGORITHMS.md)** — every variant stated in the notation of the paper it comes from, cited down to the equation or algorithm line, with the type that implements it and the deliberate deviations listed in one place.

## Features

- **Extensible Design**: Easily extend the library with custom components.
- **Parallel Execution**: Utilize multiple processors to speed up the optimization process.
- **SIMD Support**: Leverage SIMD through `System.Numerics.Vector<T>` for performance improvements.
- **Flexible Termination Strategies**: Implement custom termination strategies to control the evolution process.
- **Customizable Mutation and Selection Strategies**: Define your own mutation and selection strategies to suit your optimization needs.

## Installation

To use this library, you need:
- .NET SDK version 8.0 or higher.

To install the library via NuGet, you can use the following command:

```sh
dotnet add package DotNetDifferentialEvolution
```

> **Version 4.0 (breaking change).** The objective contract `IFitnessFunctionEvaluator` (and the
> `BaseRandomProvider`/`RandomProvider` and the new `ISolution` types) now live in the shared
> [`DotNetOptimization.Abstractions`](https://www.nuget.org/packages/DotNetOptimization.Abstractions/)
> package, which this library depends on (and pulls in automatically). The only source change for
> consumers is the namespace: replace `using DotNetDifferentialEvolution.Interfaces;` with
> `using DotNetOptimization.Abstractions;` where you implement `IFitnessFunctionEvaluator`. The
> shared package lets one objective implementation drive other optimizers in the family (e.g.
> DotNetNelderMead) with no adapters.

## Usage

Here is a basic example of how to use the library to optimize the `MyFitnessFunctionEvaluator` function:

```csharp
// Define the fitness function evaluator
// MyFitnessFunctionEvaluator must implement IFitnessFunctionEvaluator
var fitnessFunctionEvaluator = new MyFitnessFunctionEvaluator();

// Define the bounds of the search space
// The bounds must have the same length as the number of dimensions of the fitness function
var lowerBound = new double[] { -5.0, -5.0 };
var upperBound = new double[] { 5.0, 5.0 };

// Define the termination strategy
// Custom termination strategies can be implemented by extending the ITerminationStrategy interface
var terminationStrategy = new LimitGenerationNumberTerminationStrategy(
    maxGenerationNumber: 100);

var de = DifferentialEvolutionBuilder
    .ForFunction(fitnessFunctionEvaluator)
    .WithBounds(lowerBound, upperBound)
    .WithPopulationSize(50)
    .WithUniformPopulationSampling() // ... or own population sampling strategy implementing IPopulationSamplingMaker
    .WithDefaultMutationStrategy(mutationForce: 0.8, crossoverProbability: 0.9)
    .WithDefaultSelectionStrategy()
    .WithTerminationCondition(terminationStrategy)
    .UseAllProcessors() // ... or UseProcessors(int processorsCount)
    .Build();

var result = await de.RunAsync();
Console.WriteLine($"Best solution: {result.IndividualCursor.FitnessFunctionValue}");
```

MyFitnessFunctionEvaluator calculates the value of the fitness function. Below is an example using the Rosenbrock function.
Wikipedia has more information on the [Rosenbrock function](https://en.wikipedia.org/wiki/Rosenbrock_function).

```csharp
using DotNetOptimization.Abstractions;

public class RosenbrockEvaluator : IFitnessFunctionEvaluator
{
    public const double A = 1.0;
    public const double B = 100.0;
    
    public double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var x = genes[0];
        var y = genes[1];
        
        return Math.Pow(A - x, 2) + B * Math.Pow(y - x * x, 2);
    }

    public double Evaluate(
        int workerIndex,
        ReadOnlySpan<double> genes) => Evaluate(genes);
}
```

---

The result of the optimization algorithm is represented by the `Population` object.
To use the `Population` object correctly, follow these guidelines:

1. **Accessing Individuals**: The `Population` class provides efficient access to individuals without allocating additional memory. It uses spans and memory slices to reference the underlying data.

2. **Moving the Cursor**: Use the `MoveCursorTo` method to move the cursor to a specific individual index or `MoveCursorToBestIndividual` to move it to the best individual.

3. **Creating Snapshots**:  
   The `IndividualCursor` class provides a mechanism for creating a snapshot of an individual. You can specify whether the snapshot should include a deep copy of the genes or just reference the original data. Use the `GetSnapshot` method for this purpose:
    - **Shallow Copy**: Maintains a reference to the original genes for efficiency.
    - **Deep Copy**: Creates a new array for the genes to ensure the snapshot is independent.

Here is an example:

```csharp
// Assuming you have a Population object named population

// Move the cursor to the best individual
population.MoveCursorToBestIndividual();

// Access the best individual's genes and fitness function value
var bestIndividual = population.IndividualCursor;

// Create a snapshot of the best individual (deep copy)
var bestIndividualSnapshot = bestIndividual.GetSnapshot(deepCopy: true);

// Access the copied data
var copiedGenes = bestIndividualSnapshot.Genes.ToArray(); // Deep copy of genes
var copiedFitnessValue = bestIndividualSnapshot.FitnessFunctionValue;

// Now copiedGenes and copiedFitnessValue are isolated from any future changes to the Population
```

4. **Deep Copy via `GetSnapshot`**:  
   When creating a deep copy, the `GetSnapshot` method generates a new instance of the `IndividualCursor` class. This ensures that the genes and fitness value of the individual are independent of the original population data.

This approach ensures efficient access and manipulation of individuals while providing the flexibility to isolate snapshots from future modifications to the Population.

## Algorithm Variants

In addition to the classic `DE/rand/1/bin` scheme, the library ships a range of
well-established mutation strategies and self-adaptive algorithms, all selectable through
the fluent builder.

For the mathematics behind each of them — formulas, citations, and the code that implements
them — see [docs/ALGORITHMS.md](https://github.com/baryon-asymm/DotNetDifferentialEvolution/blob/main/docs/ALGORITHMS.md).

### Mutation strategies (constant parameters)

Replace `WithDefaultMutationStrategy(...)` (which is `DE/rand/1/bin`) with any of:

```csharp
.WithBestMutationStrategy(mutationForce: 0.5, crossoverProbability: 0.9)          // DE/best/1/bin
.WithCurrentToBestMutationStrategy(mutationForce: 0.5, crossoverProbability: 0.9) // DE/current-to-best/1/bin
.WithRandTwoMutationStrategy(mutationForce: 0.5, crossoverProbability: 0.9)       // DE/rand/2/bin
.WithBestTwoMutationStrategy(mutationForce: 0.5, crossoverProbability: 0.9)       // DE/best/2/bin
```

You can also supply a mutation strategy together with a control-parameter provider — for
example, to **dither** the mutation factor (sample F per individual from a range), which
often improves robustness:

```csharp
.WithMutationStrategy(
    new BestMutationStrategy(),
    new DitheredControlParameterProvider(minMutationForce: 0.3, maxMutationForce: 0.9, crossoverProbability: 0.9))
```

All strategies use binomial crossover with a guaranteed gene from the mutant (the standard
`jrand` rule), so a trial always differs from its parent.

### Self-adaptive algorithms

These presets configure mutation, crossover, parameter adaptation and selection in one
call (so they replace both `With...MutationStrategy(...)` and the selection step). They
remove the need to hand-tune F and CR:

```csharp
// jDE (Brest et al., 2006) — per-individual self-adapting F and CR
.WithJde()

// JADE (Zhang & Sanderson, 2009) — DE/current-to-pbest/1 with optional archive + adaptive F/CR
.WithJade(pBestRate: 0.1, archiveSizeRate: 1.0)

// SHADE (Tanabe & Fukunaga, 2013) — JADE with success-history based parameter adaptation
.WithShade(pBestRate: 0.2, archiveSizeRate: 1.0, memorySize: 100)

// L-SHADE (Tanabe & Fukunaga, 2014) — SHADE with linear population size reduction
//   (the CEC-2014 competition winner). The population size set above is the *initial*
//   size and shrinks toward 4 as the evaluation budget is consumed.
.WithLShade(maxEvaluationNumber: 300_000)
```

> **The adaptive variants follow their source papers (since 3.0).** Bound violations are
> reflected halfway back toward the parent (`(bound + x_i)/2`); SHADE samples its p-best rate per
> individual from `[2/N, pBestRate]`; and L-SHADE applies the terminal-`M_CR` rule. When using
> `WithLShade`, the `maxEvaluationNumber` must match the evaluation budget of
> `LimitEvaluationNumberTerminationStrategy` — the builder enforces this.

A complete L-SHADE example:

```csharp
const long maxEvaluations = 300_000;

var de = DifferentialEvolutionBuilder
    .ForFunction(fitnessFunctionEvaluator)
    .WithBounds(lowerBound, upperBound)
    .WithPopulationSize(18 * dimensions) // recommended initial size for L-SHADE
    .WithUniformPopulationSampling()
    .WithLShade(maxEvaluationNumber: maxEvaluations)
    .WithTerminationCondition(new LimitEvaluationNumberTerminationStrategy(maxEvaluations))
    .UseAllProcessors()
    .Build();

var result = await de.RunAsync();
```

Recommended starting point: for most problems, **L-SHADE** gives the strongest results out
of the box; **jDE** is a simpler, robust self-adaptive baseline. You can compare all
variants on the Rastrigin and Ackley functions by running
`dotnet run -c Release --project benchmarks/DotNetDifferentialEvolution.Benchmark -- convergence`.

### Your own variant

The four presets above are `IDeVariant` implementations, and so is anything you write. A variant
installs its mutation operator, control-parameter source, generation hook, selection rule and
archive size as one bundle, which is what keeps the pieces consistent — an adaptive scheme is
meaningless without the operator that reads the parameters it adapts.

```csharp
public sealed class MyVariant : IDeVariant
{
    public DeVariantSetup Configure(in DeVariantConfiguration configuration) => new()
    {
        MutationStrategy = new CurrentToPBestMutationStrategy(pBestRate: 0.1),
        ControlParameterProvider = new DitheredControlParameterProvider(0.3, 0.9, 0.9),
        GenerationStrategy = new MyAdaptation(configuration.PopulationSize),
        ArchiveCapacity = configuration.PopulationSize
    };

    // Optional: cross-check the completed configuration and throw to reject it.
    public void Validate(in DeVariantConfiguration configuration, ITerminationStrategy termination) { }
}

// …
.WithVariant(new MyVariant())
```

A variant configured this way is held to the same rules as a built-in one: its mutation strategy's
declared `Requirements` are checked against what the variant installed, the population size is
checked against the operator's minimum, and the engine maintains whatever the operator declared it
needs — including the p-best fitness ranking, which no strategy has to maintain for itself.

### Cancellation

```csharp
var result = await de.RunAsync(cancellationToken);
```

Cancellation is observed at the next generation barrier — the one point at which every worker has
finished its stripe and none has started the next — so the workers stop with the population in a
consistent state. A run with an expensive objective therefore stops within roughly one generation
of the request rather than instantly. The task completes as canceled (`OperationCanceledException`)
and every worker thread is stopped.

### Reproducible runs

```csharp
.WithSeed(20260728)
```

Every worker gets its own generator derived from the seed, which is what makes a *parallel* run
reproducible: the striping is fixed and each individual is built, evaluated and selected
end-to-end by one worker, so nothing depends on how the workers interleave. The seed covers the
initial population, mutation and crossover, control-parameter sampling and archive eviction.

Two caveats worth knowing before you rely on it:

- **The worker count is part of the seed's meaning.** Individual *i* draws from worker *i mod W*'s
  stream, so the same seed under a different `UseProcessors(...)` is a different run. Reproducible,
  not portable across worker counts.
- **A seed is reproducible within a minor version.** Changing how the engine consumes randomness
  reshuffles every seeded run without being a defect in either version.

A custom `IPopulationSamplingMaker` or `IGenerationStrategy` is offered the seeded source through
`UseRandomProvider` and is reproducible if it uses what it is given; an `ILocalSearchRefiner` owns
its randomness entirely and must seed itself.

### Hybrid / memetic local search

You can interleave a local-search refinement into the evolutionary loop: supply an
`ILocalSearchRefiner` and it runs single-threaded between generations — every *N* generations,
after the best individual is identified — with read/write access to the population. This is the
seam a local optimizer (e.g. Nelder–Mead) plugs into to polish the best solution in place and feed
the improvement straight back into the search, while the adaptive algorithm's own state (SHADE /
L-SHADE memory and archive) is preserved.

```csharp
.WithLocalSearch(refiner, everyNGenerations: 10)
```

A refiner must add any fitness evaluations it performs to `ProblemContext.EvaluationCount` so an
evaluation-budget termination stays accurate. Because the result's `IndividualCursor` implements
`DotNetOptimization.Abstractions.ISolution`, it can seed another optimizer's run directly.

## Contributing

Contributions are welcome! For bug reports or requests, please submit an issue.
For code contributions, please follow the guidelines:

1. **Fork the Repository**: Start by forking the repository on GitHub.

2. **Clone the Repository**: Clone your forked repository to your local machine.
    ```sh
    git clone https://github.com/your-username/DotNetDifferentialEvolution.git
    ```

3. **Create a Branch**: Create a new branch for your feature or bug fix.
    ```sh
    git checkout -b feature-or-bugfix-name
    ```

4. **Make Changes**: Make your changes to the codebase. Ensure that your code follows the project's coding standards and includes appropriate tests.

5. **Commit Changes**: Commit your changes with a clear and concise commit message.
    ```sh
    git commit -m "Description of the feature or fix"
    ```

6. **Push Changes**: Push your changes to your forked repository.
    ```sh
    git push origin feature-or-bugfix-name
    ```

7. **Create a Pull Request**: Open a pull request to the main repository. Provide a detailed description of your changes and any relevant information.

8. **Review Process**: Your pull request will be reviewed by the maintainers. Be prepared to make any necessary changes based on feedback.

Thank you for contributing!

## License

This project is licensed under the MIT License. See the `LICENSE` file for more details.

```markdown
MIT License

Copyright (c) 2025 Eduard Burachek

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

For more information, please refer to the `LICENSE` file in the repository.