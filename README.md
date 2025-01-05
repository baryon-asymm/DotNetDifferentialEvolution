# DotNetDifferentialEvolution

## Introduction

Differential Evolution (DE) is a stochastic optimization algorithm used for finding global minima or maxima of functions in multi-dimensional spaces.
It was introduced by Kenneth Price and Rainer Storn in 1997.
DE is known for its simplicity and effectiveness, especially for complex optimization problems. For more details on the algorithm, you can refer to the [Wikipedia page](https://en.wikipedia.org/wiki/Differential_evolution).

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
using DotNetDifferentialEvolution.Interfaces;

public class RosenbrockEvaluator : IFitnessFunctionEvaluator
{
    public const double A = 1.0;
    public const double B = 100.0;
    
    public virtual double Evaluate(
        ReadOnlySpan<double> genes)
    {
        var x = genes[0];
        var y = genes[1];
        
        return Math.Pow(A - x, 2) + B * Math.Pow(y - x * x, 2);
    }
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

Copyright (c) 2024 Eduard Burachek

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