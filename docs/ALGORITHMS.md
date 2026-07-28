# Algorithms, formulas, and where they live in the code

This document states what the library actually computes. For every algorithm it gives the formula
in the notation of the paper it comes from, a citation precise enough to check (equation number or
algorithm line), and the type that implements it.

It exists because of a defect. L-SHADE updated its crossover-rate memory with the wrong mean for
four releases. The code was clear, the tests were green, and the XML documentation said
"SHADE plus linear population size reduction" — which was true, and said nothing about which of
SHADE's two memory-update rules was meant. Nowhere did the repository *state the formula*, so
nobody could notice it was the wrong one. A test suite pins the behaviour the code has; only a
specification pins the behaviour it should have.

Read [§9 Deliberate deviations](#9-deliberate-deviations-from-the-papers) if you are comparing
results against a reference implementation. Every knowing divergence from a literal reading of the
papers is listed there, with its reason.

**Contents**

1. [Notation and conventions](#1-notation-and-conventions)
2. [The engine](#2-the-engine)
3. [Classic differential evolution](#3-classic-differential-evolution)
4. [jDE](#4-jde--brest-et-al-2006)
5. [JADE](#5-jade--zhang--sanderson-2009)
6. [SHADE](#6-shade--tanabe--fukunaga-2013)
7. [L-SHADE](#7-l-shade--tanabe--fukunaga-2014)
8. [Randomness and reproducibility](#8-randomness-and-reproducibility)
9. [Deliberate deviations from the papers](#9-deliberate-deviations-from-the-papers)
10. [Bibliography](#10-bibliography)

---

## 1. Notation and conventions

The library minimises. Everywhere below, "better" means "smaller".

| Symbol | Meaning | In the code |
|---|---|---|
| $D$ | problem dimension, genome size | `PopulationView.GenomeSize` |
| $N$ | population size in the current generation | `PopulationView.Count`, `Population.PopulationSize` |
| $N^{init}$, $N^{min}$ | initial and final population size under L-SHADE | `LShadeStrategy` constructor arguments |
| $G$ | generation index | — (the engine does not name it) |
| $x_{i,G}$ | individual $i$ of generation $G$ | a $D$-slice of `PopulationView.Genes` |
| $v_{i,G}$ | mutant vector | the trial buffer, before crossover |
| $u_{i,G}$ | trial vector | `MutationContext.TrialIndividual` |
| $f$ | objective function | `IFitnessFunctionEvaluator.Evaluate` |
| $F$ | mutation factor (scaling factor) | `MutationContext.MutationForce` |
| $CR$ | crossover probability | `MutationContext.CrossoverProbability` |
| $\underline{x}_j,\ \overline{x}_j$ | box bounds of gene $j$ | `ProblemContext.GenesLowerBound` / `GenesUpperBound` |
| $A$ | external archive of displaced parents | `ProblemContext.Archive` |
| $\text{nfe}$ | fitness evaluations consumed so far | `ProblemContext.EvaluationCount` |
| $W$ | worker (thread) count | `ProblemContext.WorkersCount` |

**Citation convention.** `[5, Alg. 2 line 12]` means reference 5 in the
[bibliography](#10-bibliography), Algorithm 2, line 12. Equations are cited by the number the paper
itself prints, e.g. `[4, Eq. (17)]`.

**Code references** name a file and a type or member, never a line number — line numbers rot within
a release. CI verifies that every path named here exists; the type and member names are checked by
review. Where a row says *pinned by*, a named test asserts that formula's arithmetic on scripted
draws, so a silent change to the formula fails the build.

**`BaseRandomProvider`** and `IFitnessFunctionEvaluator` come from the
[`DotNetOptimization.Abstractions`](https://www.nuget.org/packages/DotNetOptimization.Abstractions/)
package, not from this repository.

---

## 2. The engine

### 2.1 Initialization

The initial population is sampled uniformly from the box:

```math
x_{j,i,0} = \underline{x}_j + \text{rand}[0,1) \cdot (\overline{x}_j - \underline{x}_j),
\qquad j = 1 \ldots D,\ i = 1 \ldots N
```

Implemented in [UniformRandomSamplingMaker.cs](../src/DotNetDifferentialEvolution/PopulationSamplingMaker/UniformRandomSamplingMaker.cs).
Replaceable through `IPopulationSamplingMaker`.

The initial population is evaluated once, and $\text{nfe}$ starts at $N$ rather than at 0 — those
$N$ evaluations were really performed. See
[§9.6](#96-the-evaluation-count-starts-at-n-not-0).

### 2.2 The generation loop

A generation is a superstep: $W$ workers each build, evaluate and select a fixed stripe of the
population, then all of them meet at a barrier where the single-threaded bookkeeping runs.

Worker $k$ handles individuals $\{k,\ k+W,\ k+2W,\ \ldots\}$ — cyclic striping, fixed for the
lifetime of the run. For each individual $i$ in its stripe, in
[AlgorithmExecutor.cs](../src/DotNetDifferentialEvolution/AlgorithmExecutors/AlgorithmExecutor.cs):

1. draw $F_i$, $CR_i$ from the `IControlParameterProvider`;
2. build $v_i$ — `IMutationStrategy.Mutate`;
3. crossover and bound repair into $u_i$ — `CrossoverHelper.BinomialCrossoverAndRepair`;
4. evaluate $f(u_i)$;
5. select — `ISelectionStrategy.Select`, writing the winner into the *trial* population buffer;
6. record the outcome in `TrialRecord[i]`.

Then, once, on the orchestrator thread — in
[OrchestratorWorkerHandler.cs](../src/DotNetDifferentialEvolution/Controllers/WorkerControllerEventHandlers/OrchestratorWorkerHandler.cs):

7. swap the two population buffers, so the winners become the current population and the
   *displaced parents* remain addressable in the other buffer;
8. $\text{nfe} \mathrel{+}= N$;
9. rebuild the fitness ranking, if the mutation strategy declared `MutationRequirements.FitnessRanking`;
10. `IGenerationStrategy.AfterGeneration` — archive maintenance, parameter adaptation, LPSR;
11. recompute the best individual's index;
12. run the memetic local-search refiner, if one is configured;
13. test the termination condition.

The ordering of 9 and 10 matters: the generation hook sees a ranking of the population it is about
to act on, which is what lets L-SHADE pick survivors without ranking them itself. The ordering of
10 and 11 matters too — L-SHADE moves individuals in step 10, so any index computed before it
would be stale.

Step 7 is why the archive can be filled without copying: after the swap, the parent that individual
$i$ lost with is exactly slice $i$ of the other buffer (`GenerationContext.DiscardedParents`).

### 2.3 Selection

Both thresholds of the papers are implemented, and they are not the same threshold:

```math
x_{i,G+1} = \begin{cases} u_{i,G} & \text{if } f(u_{i,G}) \le f(x_{i,G}) \\ x_{i,G} & \text{otherwise} \end{cases}
```

— survival, `[4, Eq. (6)]` and `[5, Alg. 2 line 12]` — while the archive and the parameter
adaptation record a *success* only on the strict comparison, `[5, Alg. 2 lines 16–18]`:

```math
f(u_{i,G}) < f(x_{i,G})
```

SHADE states the reason for the second one explicitly: the adaptation weights are fitness
differences, "so when $f(u_{i,G})$ and $f(x_{i,G})$ are identical, the weight becomes 0, resulting
in inappropriate parameter updates" `[4, §V-C]`. Accepting ties is what lets a population drift
across a plateau; keeping success strict is what stops a run of zero-gain ties from dragging the
adaptation with it.

[SelectionStrategy.cs](../src/DotNetDifferentialEvolution/SelectionStrategies/SelectionStrategy.cs)
returns the two answers as one
[`SelectionOutcome`](../src/DotNetDifferentialEvolution/SelectionStrategies/SelectionOutcome.cs) —
`ParentKept`, `TrialAccepted`, `TrialImproved` — so that "improved but not replaced" cannot be
expressed. `TrialRecord` exposes `Replaced` and `Improved` derived from it, and each consumer takes
the one it means:

| | rule | consumers |
|---|---|---|
| survived (`Replaced`) | $f(u) \le f(x)$ | the next generation; jDE's parameter inheritance |
| improved (`Improved`) | $f(u) < f(x)$ | the external archive; JADE/SHADE/L-SHADE adaptation |

Comparison is centralised in
[FitnessComparisonHelper.cs](../src/DotNetDifferentialEvolution/Helpers/FitnessComparisonHelper.cs),
which orders NaN as worse than every real value — see [§9.2](#92-nan-is-worse-than-every-real-value).

*Pinned by* `SelectionStrategyTests`, `TrialOutcomeReportingTests`.

### 2.4 Termination

| Strategy | Stops when |
|---|---|
| `LimitGenerationNumberTerminationStrategy` | the generation count reaches its limit |
| `LimitEvaluationNumberTerminationStrategy` | $\text{nfe}$ reaches its limit |
| `StagnationStreakTerminationStrategy` | the best value fails to improve by more than a threshold for $k$ consecutive generations |

All in [TerminationStrategies/](../src/DotNetDifferentialEvolution/TerminationStrategies/). The
condition is tested at the barrier, so a run stops within one generation of the condition becoming
true, not instantly.

---

## 3. Classic differential evolution

Storn and Price `[1]`. A generation applies mutation, crossover and selection to every individual.

### 3.1 Mutation

Index draws are mutually distinct and all differ from $i$, per
[RandomIndexSelector.cs](../src/DotNetDifferentialEvolution/MutationStrategies/Helpers/RandomIndexSelector.cs).

| Strategy | Mutant vector | Type | Min. $N$ |
|---|---|---|---|
| DE/rand/1 | $v_i = x_{r_1} + F(x_{r_2} - x_{r_3})$ | [MutationStrategy.cs](../src/DotNetDifferentialEvolution/MutationStrategies/MutationStrategy.cs) (constant $F$, $CR$), [RandMutationStrategy.cs](../src/DotNetDifferentialEvolution/MutationStrategies/RandMutationStrategy.cs) (provider) | 4 |
| DE/rand/2 | $v_i = x_{r_1} + F(x_{r_2} - x_{r_3}) + F(x_{r_4} - x_{r_5})$ | [RandTwoMutationStrategy.cs](../src/DotNetDifferentialEvolution/MutationStrategies/RandTwoMutationStrategy.cs) | 6 |
| DE/best/1 | $v_i = x_{best} + F(x_{r_1} - x_{r_2})$ | [BestMutationStrategy.cs](../src/DotNetDifferentialEvolution/MutationStrategies/BestMutationStrategy.cs) | 3 |
| DE/best/2 | $v_i = x_{best} + F(x_{r_1} - x_{r_2}) + F(x_{r_3} - x_{r_4})$ | [BestTwoMutationStrategy.cs](../src/DotNetDifferentialEvolution/MutationStrategies/BestTwoMutationStrategy.cs) | 5 |
| DE/current-to-best/1 | $v_i = x_i + F(x_{best} - x_i) + F(x_{r_1} - x_{r_2})$ | [CurrentToBestMutationStrategy.cs](../src/DotNetDifferentialEvolution/MutationStrategies/CurrentToBestMutationStrategy.cs) | 3 |
| DE/current-to-pbest/1 | see [§5.1](#51-mutation) | [CurrentToPBestMutationStrategy.cs](../src/DotNetDifferentialEvolution/MutationStrategies/CurrentToPBestMutationStrategy.cs) | 4 |

The arithmetic is one SIMD loop per term, in
[MutationMath.cs](../src/DotNetDifferentialEvolution/MutationStrategies/Helpers/MutationMath.cs);
`MinimumPopulationSize` is a member of `IMutationStrategy` and the builder rejects a smaller
population.

*Pinned by* `MutationMathTests`, `RandomIndexSelectorTests`, `MutationStrategyConvergenceTests`.

### 3.2 Binomial crossover

```math
u_{j,i} = \begin{cases} v_{j,i} & \text{if } \text{rand}_j[0,1] \le CR \ \text{ or }\ j = j_{rand} \\ x_{j,i} & \text{otherwise} \end{cases}
```

with $j_{rand}$ drawn uniformly from $1 \ldots D$ so that $u_i$ differs from $x_i$ in at least one
dimension. In
[CrossoverHelper.cs](../src/DotNetDifferentialEvolution/MutationStrategies/Helpers/CrossoverHelper.cs).

The per-gene Bernoulli trial is decided by an integer comparison rather than a floating-point one:
$CR$ is scaled once per call onto the generator's 64-bit output domain and each raw draw is compared
against it — see [§8.4](#84-the-per-gene-crossover-test). This is the hottest loop in the library.

*Pinned by* `CrossoverHelperTests`, `RandomThresholdTests`.

### 3.3 Bound repair

A gene taken from the mutant may leave the box. It is reflected halfway back toward the parent:

```math
u_{j,i} = \begin{cases} (\underline{x}_j + x_{j,i})/2 & \text{if } u_{j,i} < \underline{x}_j \\ (\overline{x}_j + x_{j,i})/2 & \text{if } u_{j,i} > \overline{x}_j \end{cases}
```

This is the JADE/SHADE/L-SHADE repair rule. It is applied by every strategy in this library, not
only the adaptive ones — see [§9.1](#91-midpoint-bound-repair-applies-to-every-strategy) — and it
keeps the population inside the box for the whole run: the parent is in the box by induction, the
bound is in the box, so their midpoint is too.

The papers repair $v_i$ before crossover; this repairs $u_i$ after it. The results are identical,
because a gene is repaired exactly when it came from $v_i$, and a gene that came from $x_i$ was
already in the box.

### 3.4 Control parameters

| Provider | $F$ | $CR$ |
|---|---|---|
| `ConstantControlParameterProvider` | fixed | fixed |
| `DitheredControlParameterProvider` | $F \sim \text{rand}[F_{min}, F_{max}]$, redrawn per individual | fixed |

In [ControlParameterProviders/](../src/DotNetDifferentialEvolution/ControlParameterProviders/).
The self-adaptive variants of §§4–7 are themselves `IControlParameterProvider` implementations.

---

## 4. jDE — Brest et al., 2006

Reference `[2]`. Every individual carries its own $(F_i, CR_i)$. Before a trial is built, each is
regenerated with a small probability; if the trial *survives*, its parameters replace the
individual's, so parameter values that work propagate with the vectors that used them.

```math
F_{i,G+1} = \begin{cases} F_l + \text{rand}_1 \cdot F_u & \text{if } \text{rand}_2 < \tau_1 \\ F_{i,G} & \text{otherwise} \end{cases}
\qquad
CR_{i,G+1} = \begin{cases} \text{rand}_3 & \text{if } \text{rand}_4 < \tau_2 \\ CR_{i,G} & \text{otherwise} \end{cases}
```

| Parameter | Value | In the code |
|---|---|---|
| $\tau_1, \tau_2$ | 0.1 | `DefaultFAdaptationProbability`, `DefaultCrAdaptationProbability` |
| $F_l$, $F_u$ | 0.1, 0.9 — so $F \in [0.1, 1.0]$ | `DefaultMinMutationForce`, `DefaultMutationForceRange` |
| $F_{i,0}$, $CR_{i,0}$ | 0.5, 0.9 | `DefaultInitialMutationForce`, `DefaultInitialCrossoverProbability` |

Implemented in [JdeStrategy.cs](../src/DotNetDifferentialEvolution/Algorithms/Jde/JdeStrategy.cs);
wired to DE/rand/1/bin by
[JdeVariant.cs](../src/DotNetDifferentialEvolution/Variants/JdeVariant.cs). The decision draw is
made before the value draw, which fixes the order in which a seeded stream is consumed.

**jDE keys inheritance on survival, not improvement** — `Replaced`, not `Improved`. The parameters
belong to the individual, and after a tie the individual *is* the trial; crediting improvement here
would leave an individual holding the parameters of a vector no longer in the population.

*Pinned by* `JdeStrategyTests`.

---

## 5. JADE — Zhang & Sanderson, 2009

Reference `[3]`. Two adaptive means, $\mu_{CR}$ and $\mu_F$, plus an optional external archive.

> The JADE paper is not openly available, so the formulas below are cited to the paper as a whole
> rather than to numbered equations, unlike §§6–7. They are the scheme as restated in `[4, §IV]`,
> which is open.

### 5.1 Mutation

DE/current-to-pbest/1 with archive:

```math
v_i = x_i + F_i \cdot (x_{pbest} - x_i) + F_i \cdot (x_{r_1} - \tilde{x}_{r_2})
```

- $x_{pbest}$ is drawn uniformly from the top $100p\%$ of the population by fitness;
- $x_{r_1}$ is drawn from the population, $r_1 \ne i$;
- $\tilde{x}_{r_2}$ is drawn from the **union** of the population and the archive,
  $r_2 \ne i,\ r_2 \ne r_1$.

In `CurrentToPBestMutationStrategy`, archive members are addressed by indices $\ge N$ in the union,
which is why they can never collide with $i$ or $r_1$. The p-best pool size is
$\max(2,\ \text{round}(p N))$ — the floor of 2 is Tanabe's, not the paper's, see
[§9.4](#94-the-p-best-pool-floor-is-2).

### 5.2 Parameter sampling

```math
CR_i = \text{clamp}_{[0,1]}\big(\text{randn}(\mu_{CR},\ 0.1)\big)
```

```math
F_i = \text{randc}(\mu_F,\ 0.1), \quad \text{redrawn while } F_i \le 0, \quad \text{truncated to } 1 \text{ if } F_i > 1
```

$CR$ is clamped; $F$ is *regenerated* when non-positive rather than clamped, because a Cauchy
distribution has enough mass below zero that clamping would pile probability onto $F = 0$, which
disables the differential term outright.

### 5.3 Adaptation

At the end of a generation, over the successful trials only:

```math
\mu_{CR} = (1-c)\,\mu_{CR} + c \cdot \text{mean}_A(S_{CR}), \qquad
\mu_F = (1-c)\,\mu_F + c \cdot \text{mean}_L(S_F)
```

where $\text{mean}_A$ is the arithmetic mean and $\text{mean}_L$ the Lehmer mean

```math
\text{mean}_L(S_F) = \frac{\sum_{k} S_{F,k}^2}{\sum_{k} S_{F,k}}
```

The Lehmer mean is deliberately biased upward relative to the arithmetic mean — the identity
$\text{mean}_L - \text{mean}_A = \operatorname{Var}(S)/\operatorname{E}(S) \ge 0$ — which counteracts
DE's tendency to let $F$ decay.

### 5.4 Archive

Parents that were *beaten* are appended to the archive; when it is full, an existing entry is
overwritten at random. Capacity is $\text{round}(r_{arc} \cdot N)$; the library's default
$r_{arc} = 1$ gives $\lvert A \rvert = N$, and $r_{arc} = 0$ disables the archive.

Implemented in [JadeStrategy.cs](../src/DotNetDifferentialEvolution/Algorithms/Jade/JadeStrategy.cs)
and the shared
[AdaptiveStrategyBase.cs](../src/DotNetDifferentialEvolution/Algorithms/Common/AdaptiveStrategyBase.cs);
wired by [JadeVariant.cs](../src/DotNetDifferentialEvolution/Variants/JadeVariant.cs) with defaults
$p = 0.1$, $r_{arc} = 1.0$, $c = 0.1$.

*Pinned by* `JadeStrategyTests` — the scripted draws collapse the Gaussian to exactly $\mu_{CR}$ and
the Cauchy to exactly $\mu_F$, which is how the private means are read back.

---

## 6. SHADE — Tanabe & Fukunaga, 2013

Reference `[4]`. JADE's mutation and archive, but the two scalar means are replaced by a memory of
$H$ pairs. The paper calls the memory size $LP$; L-SHADE renames it $H$, which is the name used
here and in the code.

### 6.1 Sampling

Each individual picks a memory slot at random and samples from it, `[4, Eq. (15)–(16)]`:

```math
r_i = \text{randint}[1, H], \qquad
CR_i = \text{randn}(M_{CR, r_i},\ 0.1), \qquad
F_i = \text{randc}(M_{F, r_i},\ 0.1)
```

with the same clamp/regenerate treatment as JADE. All slots are initialised to 0.5.

SHADE also samples the greediness parameter per individual, `[4, Eq. (20)]`:

```math
p_i = \text{rand}[p_{min},\ 0.2], \qquad p_{min} = 2/N
```

### 6.2 Memory update

The weights are the fitness improvements, `[4, Eq. (14)]`:

```math
w_k = \frac{\Delta f_k}{\sum_{l} \Delta f_l}, \qquad \Delta f_k = \lvert f(u_{k,G}) - f(x_{k,G}) \rvert
```

One slot $k$ is overwritten per generation, cycling $1 \ldots H$, and only if there were successes
at all, `[4, Eq. (17)–(18)]`:

```math
M_{CR,k,G+1} = \text{mean}_{WA}(S_{CR}) = \sum_k w_k \cdot S_{CR,k}
\qquad \text{[4, Eq. (13), (17)]}
```

```math
M_{F,k,G+1} = \text{mean}_{WL}(S_F) = \frac{\sum_k w_k \cdot S_{F,k}^2}{\sum_k w_k \cdot S_{F,k}}
\qquad \text{[4, Eq. (18), (19)]}
```

**The two means are different.** SHADE 2013 updates $M_{CR}$ with the weighted *arithmetic* mean and
$M_F$ with the weighted *Lehmer* mean. That asymmetry is the paper's, not an oversight in it — and
it does not carry over to L-SHADE, which is the subject of
[§7.2](#72-the-lehmer-mean-for-the-cr-memory).

In [ShadeStrategy.cs](../src/DotNetDifferentialEvolution/Algorithms/Shade/ShadeStrategy.cs), the
choice of mean is the virtual `UseLehmerCrMean`, `false` here. Defaults from
[ShadeVariant.cs](../src/DotNetDifferentialEvolution/Variants/ShadeVariant.cs): $H = 100$,
$p_{max} = 0.2$, $r_{arc} = 1.0$ — matching the paper's $LP = N = 100$ setting.

*Pinned by* `ShadeStrategyTests`.

---

## 7. L-SHADE — Tanabe & Fukunaga, 2014

Reference `[5]`, the CEC-2014 competition winner. L-SHADE is SHADE **1.1** plus Linear Population
Size Reduction. SHADE 1.1 differs from the SHADE of §6 in two ways, both in the memory update, and
both are implemented here.

[LShadeStrategy.cs](../src/DotNetDifferentialEvolution/Algorithms/Lshade/LShadeStrategy.cs) extends
`ShadeStrategy`; the deltas below are exactly what it overrides.

### 7.1 The terminal CR memory value

`[5, Alg. 1 lines 2–3]`: if a slot's successful crossover rates are all zero — or the slot is
already terminal — it is fixed at $\perp$ and stays there:

```math
M_{CR,k,G+1} = \perp \quad \text{if } M_{CR,k,G} = \perp \ \text{ or }\ \max(S_{CR}) = 0
```

and a slot holding $\perp$ deterministically yields $CR_i = 0$ instead of a Gaussian draw,
`[5, Alg. 2 line 8]`. The effect is a "change one parameter at a time" search, which the paper
reports as effective on multimodal problems.

Stored as the sentinel $-1$ — any value outside $[0,1]$ is unambiguous — behind the virtual
`UseTerminalCr`, `true` here and `false` on `ShadeStrategy`. A terminal slot consumes no random
draws.

### 7.2 The Lehmer mean for the CR memory

`[5, Alg. 1 line 5]`, using $\text{mean}_{WL}$ from `[5, Eq. (7)]`:

```math
M_{CR,k,G+1} = \text{mean}_{WL}(S_{CR}) = \frac{\sum_k w_k \cdot S_{CR,k}^2}{\sum_k w_k \cdot S_{CR,k}}
```

The same weighted Lehmer mean SHADE already used for $M_F$ — so under SHADE 1.1 both memories use
it, and the asymmetry of §6.2 is gone.

**This is the formula the library got wrong until version 5.1.0.** It inherited SHADE 2013's
weighted arithmetic mean, and the error had a fixed sign:

```math
\text{mean}_{WL} - \text{mean}_{WA} = \frac{\operatorname{Var}_w(S)}{\operatorname{E}_w(S)} \ge 0
```

so $M_{CR}$ was always reported low — exactly the downward bias the Lehmer mean was introduced to
remove — and the error compounded with §7.1, because a lower $M_{CR}$ makes $\max(S_{CR}) = 0$ and
the permanent $CR = 0$ lock more likely.

The two halves of SHADE 1.1's memory update are kept as two separate virtuals rather than one
"SHADE 1.1" flag, so each can be enabled and tested alone.

*Pinned by* `LShadeStrategyTests.AfterGeneration_UpdatesMemoryCrWithTheWeightedLehmerMean`, which
asserts the Lehmer value on scripted draws **and** that it differs from the arithmetic value the
sibling test in `ShadeStrategyTests` pins. The two tests are meant to be read as a pair: they are
what stops the two variants from silently converging again.

### 7.3 Linear population size reduction

`[5, Eq. (10)]`:

```math
N_{G+1} = \text{round}\left[ \frac{N^{min} - N^{init}}{\text{MAX\_NFE}} \cdot \text{nfe} + N^{init} \right]
```

with $N^{min} = 4$, the smallest population `current-to-pbest/1` can operate on. When
$N_{G+1} < N_G$, the $N_G - N_{G+1}$ worst-ranking individuals are deleted and the archive is
resized, `[5, Alg. 2 lines 21–24]`:

```math
\lvert A \rvert = \text{round}(r_{arc} \cdot N)
```

Rounding is half-away-from-zero, not .NET's default banker's rounding — see
[§9.5](#95-round-half-away-from-zero).

> **A typo in the paper.** `[5, Alg. 2 line 22]` prints `if N_G < N_{G+1} then ... delete lowest
> N_G − N_{G+1} members`, which deletes a negative number of individuals. The intended comparison
> is `>`; the library reduces only when the schedule calls for a smaller population.

Because the schedule is a function of the budget, the budget given to `WithLShade` must equal the
one the termination strategy enforces, or reduction will not reach its minimum exactly as the run
ends. `LShadeVariant.Validate` rejects the mismatch, and the constructor rejects a non-positive
budget and a negative archive rate — both of which used to produce a wrong run rather than an error.

Defaults from
[LShadeVariant.cs](../src/DotNetDifferentialEvolution/Variants/LShadeVariant.cs): $p = 0.11$,
$r_{arc} = 2.6$, $H = 6$ — the tuned settings of `[5, Table II]`. $N^{init}$ is the caller's
population size; the paper sets $N^{init} = \text{round}(18 D)$, which this library does not impose.

*Pinned by* `LShadeStrategyTests`, `PopulationSizeReportingTests`.

---

## 8. Randomness and reproducibility

### 8.1 The generator

[SeededRandomProvider.cs](../src/DotNetDifferentialEvolution/RandomProviders/SeededRandomProvider.cs)
implements **xoshiro256\*\*** `[6]`. Its 256-bit state is expanded from the 32-bit seed with
**SplitMix64** `[7]`, so worker seeds that differ by one still produce well-separated streams.

The provider is deliberately **not** thread-safe. Reproducibility requires that a stream be consumed
in a fixed order, and sharing one instance between workers would let the thread interleaving decide
who gets which number.

### 8.2 Derived draws

| Draw | Method |
|---|---|
| $\text{rand}[0,1)$ | top 53 bits of the raw output, scaled by $2^{-53}$ |
| $\text{randint}[0,m)$ | Lemire multiply-shift `[8]`; bias below $m/2^{32}$, i.e. under $10^{-7}$ for any realistic $N$ |
| $\text{randn}(\mu,\sigma)$ | Box–Muller `[9]`, **both** normals kept — see below |
| $\text{randc}(\mu_0,\gamma)$ | $\mu_0 + \gamma \tan\big(\pi (u - 0.5)\big)$ |

Box–Muller produces two independent standard normals per pair of uniforms. The second is cached on
the provider instance and returned by the next call. It is cached *on the instance* by necessity: a
`static` cache would be a data race, and a `[ThreadStatic]` one would be race-free but would untie
the stream from the seed, silently destroying reproducibility.

*Pinned by* `SeededRandomProviderTests`, `SeededRandomProviderGaussianTests`,
`RandomDistributionHelperTests`.

### 8.3 Seed layout

From a root seed $s$ — `WithSeed(s)`, or one draw from `Random.Shared` if the run is unseeded:

| Consumer | Seed |
|---|---|
| worker $k$, $k = 0 \ldots W-1$ | $s + k$ |
| the generation strategy (archive eviction) | $s + W$ |
| the population sampling maker | $s + W + 1$ |

### 8.4 The per-gene crossover test

[RandomThreshold.cs](../src/DotNetDifferentialEvolution/RandomProviders/RandomThreshold.cs) maps a
probability onto the generator's 64-bit domain once per call, and the per-gene test compares raw
integers. Because the map is monotone, `u <= p` and `Scale(u) <= Scale(p)` disagree only when both
fall in the same $2^{-64}$ bucket — below the rounding error of the floating-point comparison it
replaces.

### 8.5 What reproducibility guarantees

A seeded run is **bit-reproducible for a given worker count**, regardless of how the workers'
threads interleave: each individual is built, evaluated and selected end to end by one worker, from
that worker's own stream.

It is **not** reproducible across worker counts, and cannot be: individual $i$ draws from worker
$i \bmod W$'s stream, so changing $W$ changes which numbers each individual sees. This is the price
of per-worker streams, and per-worker streams are what make a parallel run reproducible at all.

Seeds are also not stable across minor versions. Any change to the generator, to the order draws are
consumed in, or to a formula that consumes draws will move a seeded run. `CHANGELOG.md` records
each such break.

*Pinned by* `SeededReproducibilityTests`, `ParallelDeterminismTests`.

---

## 9. Deliberate deviations from the papers

Each of these is a knowing divergence from a literal reading of the source paper. If you are
comparing this library against a reference implementation, this is the list that explains the
differences.

### 9.1 Midpoint bound repair applies to every strategy

The $(\text{bound} + x_i)/2$ rule of §3.3 is specified by JADE/SHADE/L-SHADE; Storn and Price do not
specify a repair rule at all, and implementations commonly re-sample the gene uniformly. This
library applies the midpoint rule everywhere, including classic DE. It preserves the box invariant,
keeps the trial near its parent instead of injecting a fresh random gene, and makes the variants
comparable to each other. Version 3.0.0 removed the alternative; there is no opt-out.

### 9.2 NaN is worse than every real value

A user objective may return NaN, and NaN loses every IEEE comparison — both `NaN < x` and `x < NaN`
are false. Under a plain `<` a NaN individual would be impossible to replace *and* impossible to
displace as the best. The engine therefore orders NaN as worse than every real value, in
`FitnessComparisonHelper` and in
[PopulationSortHelper.cs](../src/DotNetDifferentialEvolution/Helpers/PopulationSortHelper.cs), where
NaN is substituted with $+\infty$ in the sort keys. .NET's default `double` comparer sorts NaN
*first*, which would otherwise rank a NaN individual as the best, put it in every p-best pool, and
make L-SHADE's reduction preferentially keep it.

*Pinned by* `NaNFitnessTests`, `PopulationSortHelperTests`.

### 9.3 Two NaNs are not a tie

$f(u) \le f(x)$ accepts ties, but two NaNs do not count as one. Swapping one unusable value for
another is not an acceptance, and treating it as one would rewrite a NaN individual's genes every
generation for nothing. A NaN parent is still replaced by any real value.

### 9.4 The p-best pool floor is 2

The pool size is $\max(2,\ \text{round}(pN))$. The floor of 2 comes from Tanabe's reference
implementation ("choose at least two best solutions"), not from the papers' formulas: a pool of one
would degrade DE/current-to-pbest/1 into the greedier DE/current-to-best/1. Raised from 1 in 4.1.0.

### 9.5 Round half away from zero

The papers write $\text{round}(\cdot)$ meaning round-half-up. .NET's `Math.Round` defaults to
banker's rounding, which would resolve `2.5` to `2`. Every schedule rounding — the LPSR population
size, the archive capacity, the p-best pool size — passes `MidpointRounding.AwayFromZero`
explicitly. Fixed in 4.1.0.

### 9.6 The evaluation count starts at N, not 0

The initial population is evaluated before the first generation, and those $N$ evaluations are
counted. An evaluation budget therefore means total objective calls, which is what the CEC protocols
measure. Under L-SHADE this also shifts the reduction schedule by one population's worth of budget
relative to an implementation that starts the counter at zero.

### 9.7 Non-finite improvement weights are skipped

$\Delta f_k = \lvert f(u) - f(x) \rvert$ is assumed finite by `[4, Eq. (14)]`. It need not be: a
trial that replaces a parent scored NaN, or one scored $-\infty$, is a genuine success whose
improvement cannot be weighted. Such a record is excluded from the weighted means. Letting it
through would put NaN into the weight sum, which the "no successes" guard cannot catch — every
comparison against NaN is false — permanently poisoning $M_F$ and $M_{CR}$ for the rest of the run.

### 9.8 Guards where a paper's formula is undefined

- The memory update is skipped when the total weight is not strictly positive, matching
  `[4, Alg. 1 line 27]` ("the memory is not updated") but also covering the degenerate case where
  every improvement is zero.
- The Lehmer branch for $M_{CR}$ divides by $\sum w_k S_{CR,k}$, which is zero only when every
  successful $CR$ is zero. Under L-SHADE that case is §7.1's terminal rule, so the division is
  unreachable there; the guard exists so the mean stays defined for a configuration that takes
  SHADE 1.1's mean without its terminal rule.
- JADE's $\mu_F$ update is skipped when $\sum S_F = 0$.
- An archive capacity that is zero *or negative* disables the archive. A generation hook may write
  the capacity — L-SHADE rescales it on every reduction — and a negative value used to slip past an
  equality test and throw from inside a running generation.

### 9.9 L-SHADE's progress is clamped

$\text{nfe}/\text{MAX\_NFE}$ is clamped to 1, so overshooting the budget cannot drive the schedule
below $N^{min}$. The population size is additionally clamped to $[N^{min},\ N_G]$ — LPSR never
grows a population.

### 9.10 Reproducibility is per worker count

See [§8.5](#85-what-reproducibility-guarantees). The papers describe sequential algorithms and say
nothing about this; it is a property of the parallel execution model, not of the algorithms.

---

## 10. Bibliography

1. R. Storn and K. Price. "Differential Evolution – A Simple and Efficient Heuristic for Global
   Optimization over Continuous Spaces." *Journal of Global Optimization* 11(4):341–359, 1997.
   [doi:10.1023/A:1008202821328](https://doi.org/10.1023/A:1008202821328)
2. J. Brest, S. Greiner, B. Bošković, M. Mernik, V. Žumer. "Self-Adapting Control Parameters in
   Differential Evolution: A Comparative Study on Numerical Benchmark Problems." *IEEE Transactions
   on Evolutionary Computation* 10(6):646–657, 2006.
   [doi:10.1109/TEVC.2006.872133](https://doi.org/10.1109/TEVC.2006.872133)
3. J. Zhang and A. C. Sanderson. "JADE: Adaptive Differential Evolution With Optional External
   Archive." *IEEE Transactions on Evolutionary Computation* 13(5):945–958, 2009.
   [doi:10.1109/TEVC.2009.2014613](https://doi.org/10.1109/TEVC.2009.2014613)
4. R. Tanabe and A. Fukunaga. "Success-History Based Parameter Adaptation for Differential
   Evolution." *IEEE CEC 2013*, pp. 71–78.
   [doi:10.1109/CEC.2013.6557555](https://doi.org/10.1109/CEC.2013.6557555) ·
   [PDF](https://metahack.org/cec2013de.pdf)
5. R. Tanabe and A. Fukunaga. "Improving the Search Performance of SHADE Using Linear Population
   Size Reduction." *IEEE CEC 2014*, pp. 1658–1665.
   [doi:10.1109/CEC.2014.6900380](https://doi.org/10.1109/CEC.2014.6900380) ·
   [PDF](https://metahack.org/CEC2014-Tanabe-Fukunaga.pdf)
6. D. Blackman and S. Vigna. "Scrambled Linear Pseudorandom Number Generators." *ACM Transactions on
   Mathematical Software* 47(4):1–32, 2021.
   [doi:10.1145/3460772](https://doi.org/10.1145/3460772)
7. G. L. Steele Jr., D. Lea, C. H. Flood. "Fast Splittable Pseudorandom Number Generators."
   *OOPSLA 2014*, pp. 453–472.
   [doi:10.1145/2660193.2660195](https://doi.org/10.1145/2660193.2660195)
8. D. Lemire. "Fast Random Integer Generation in an Interval." *ACM Transactions on Modeling and
   Computer Simulation* 29(1):1–12, 2019.
   [doi:10.1145/3230636](https://doi.org/10.1145/3230636)
9. G. E. P. Box and M. E. Muller. "A Note on the Generation of Random Normal Deviates." *Annals of
   Mathematical Statistics* 29(2):610–611, 1958.
   [doi:10.1214/aoms/1177706645](https://doi.org/10.1214/aoms/1177706645)
10. F. Peng, K. Tang, G. Chen, X. Yao. "Multi-start JADE with Knowledge Transfer for Numerical
    Optimization." *IEEE CEC 2009*, pp. 1889–1895. — the source of the weighted arithmetic mean
    SHADE uses for $M_{CR}$, `[4, Eq. (13)]`.
    [doi:10.1109/CEC.2009.4983171](https://doi.org/10.1109/CEC.2009.4983171)
