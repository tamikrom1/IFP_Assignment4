# DeliveryQuotes

Course: Introduction to Functional Programming - Assignment 4.

**Topic:** calculate delivery quotes. **Intended users:** a small courier company's dispatcher, who gets a list of
delivery requests and needs a price for every request that can be served, under either the *Standard* or the
*Express* price policy.

## Team

| Member | Group | Implemented logic | Tests | Documentation |
|---|---|---|---|---|
| Tamerlan Iskakov | IT-2504 | `Models.cs`, `DeliveryParser.cs`, `Program.cs` | parser, empty/invalid/missing-input, duplicate and line-number tests | input format, dataset table, failure demonstration |
| Madina Kairbayeva | IT-2504 | `Stages.cs`, `PricingPolicies.cs`, `DeliveryWorkflow.cs` | stage, boundary, two-policy, purity and captured-configuration tests | stage contracts, requirement map |


## Workflow

```
raw text lines --(boundary)--> ParseDataset --> validated DeliveryRequest records
   --> Stage 1 SelectDeliverable --> Stage 2 Price (calls the policy delegate) --> Stage 3 BuildReport --> QuoteReport
```

Input: one CSV line per request, `id,distanceKm,weightKg` (invariant culture, `.` as decimal point).
Output: a `QuoteReport` per policy: quotes ordered by cost then id, the total, and the ids that were excluded.

### Business rules

1. **Validation (boundary).** Three fields; id not blank; distance and weight are numbers and **not negative**.
   Zero is legitimate (zero distance = pick-up and delivery at the same place). Duplicate ids (case-insensitive) are
   rejected. Blank lines are ignored but still counted in line numbers. A rejected line is reported with its line number,
   raw text and a message; the other lines are still processed. Nothing is replaced by a default.
2. **Service limits (stage 1), inclusive:** distance ≤ 50 km and weight ≤ 30 kg. Requests above a limit are excluded
   (reported, not priced).
3. **Cost (stage 2):** `cost = fixedFee + ratePerKm × distanceKm`.
   - Standard policy: 500 + 100 per km.
   - Express policy: 800 + 150 per km.

### Dataset and expected results (written before running)

`data/deliveries.csv` (8 lines):

| Line | Raw | Expected outcome | Standard | Express |
|---|---|---|---|---|
| 1 | `D1,2,3` | accepted | 700 | 1100 |
| 2 | `D2,10,5` | accepted | 1500 | 2300 |
| 3 | `D3,50,30` | accepted — **boundary**, exactly on both limits | 5500 | 8300 |
| 4 | `D4,60,5` | valid but **excluded**: 60 km > 50 km | — | — |
| 5 | `D5,0,1` | accepted — zero distance is valid | 500 | 800 |
| 6 | `D6,-4,2` | **rejected** at the boundary: negative distance | — | — |
| 7 | `D7,abc,2` | **rejected** at the boundary: not a number | — | — |
| 8 | `D8,20,45` | valid but **excluded**: 45 kg > 30 kg | — | — |

Expected report (ordered by cost, then id): **Standard** D5 500, D1 700, D2 1500, D3 5500, total **8200**;
**Express** D5 800, D1 1100, D2 2300, D3 8300, total **12500**. Excluded: D4, D8. Rejected lines: 6 and 7.
The policies differ on every accepted record (for example D1: 700 vs 1100).

Other data files: `data/empty.csv` (valid empty dataset) and `data/invalid.csv` (duplicate id, wrong field count, blank id).

## Stages (pure functions in `src/DeliveryQuotes/Stages.cs`)

| # | Function | Input → output | Rule | Example |
|---|---|---|---|---|
| 1 | `Stages.SelectDeliverable` | `IEnumerable<DeliveryRequest>`, `ServiceLimits` → `Selection` (Eligible, Excluded) | keep requests with distance ≤ max and weight ≤ max (inclusive), keep input order | limits (50, 30): `(D3,50,30)` eligible; `(D4,60,5)` excluded |
| 2 | `Stages.Price` | `IEnumerable<DeliveryRequest>`, `Func<DeliveryRequest,decimal>` → `ImmutableArray<PricedRequest>` | calls the supplied cost rule once per request | Standard rule: `(D1,2 km)` → cost 700 |
| 3 | `Stages.BuildReport` | policy name, `IEnumerable<PricedRequest>`, excluded requests → `QuoteReport` | order quotes by cost then ordinal id, sum the total, list excluded ids | `(D1,700)`, `(D5,500)` → quotes D5, D1; total 1200 |

The workflow is visible in one place, `DeliveryWorkflow.Run`: `SelectDeliverable` → `Price(selection.Eligible, policy.CostRule)` → `BuildReport`.

**Trace of record `D1,2,3` (Standard):** raw text `"D1,2,3"` → `DeliveryRequest("D1", 2, 3)` → stage 1: 2 ≤ 50 and 3 ≤ 30, so it is in `Eligible` →
stage 2: `PricedRequest(D1, 500 + 100 × 2 = 700)` → stage 3: `DeliveryQuote("D1", 700)` inside the `QuoteReport`.

### Higher-order function and policies

`Stages.Price` is the higher-order function: it takes a `Func<DeliveryRequest, decimal>` and invokes it (`costRule(r)`)
for every request. `PricingPolicies.Linear(name, fixedFee, ratePerKm)` builds a policy; `Standard` and `Express` are the two
policies for the same rule. The policy is chosen in `Program.cs` (`PricingPolicies.ByName`) and passed as an argument; no mutable
global is read. The decimals are copied into the closure, so later changes to the caller's variables do not change the policy
(tested). The service limits are also an argument (`ServiceLimits`), so a threshold can be changed without touching the stages.

### Purity

Parsing and stages do no file or Console access, no clock, no randomness, no shared mutable state. They do not modify input:
results are new `ImmutableArray` values and records. `File.ReadAllLines` and all `Console` output are only in `Program.cs`,
after/before the pure workflow.

## Requirement map

| Requirement | Where |
|---|---|
| Task 1 workflow, dataset ≥ 5 records, expected results | this README, `data/deliveries.csv` |
| Task 2 typed immutable models | `Models.cs` (records, `ImmutableArray`) |
| Task 2 boundary parsing, missing vs empty vs zero, clear messages | `DeliveryParser.TryParseLine`, `DeliveryParser.ParseDataset` |
| Task 3 three composed pure stages | `Stages.SelectDeliverable`, `Stages.Price`, `Stages.BuildReport`; composed in `DeliveryWorkflow.Run` |
| Task 4 higher-order function + two policies | `Stages.Price` (invokes `costRule`), `PricingPolicies.Standard` / `Express` / `Linear` |
| Task 4 policy chosen outside the pipeline | `Program.cs`, `PricingPolicies.ByName`, parameter `policy` of `DeliveryWorkflow.Run` |
| Task 5 pure core, I/O at the edge | stages/parser vs `Program.cs` |
| Tests | `tests/DeliveryQuotes.Tests/DeliveryTests.cs` |

## Tests (xUnit, `tests/DeliveryQuotes.Tests/DeliveryTests.cs`)

| Required case | Test(s) | Expected |
|---|---|---|
| Normal workflow | `NormalWorkflow_Standard_...`, `NormalWorkflow_Express_...` | tables above; totals 8200 and 12500; excluded D4, D8; error lines 6, 7 |
| Empty dataset | `EmptyDataset_GivesEmptyReport_...` | no quotes, total 0, no exception |
| Boundary value | `Boundary_ExactLimitsAreIncluded_...`, `Boundary_ZeroDistance_...` | 50 km / 30 kg included, 50.01 / 30.01 excluded; zero distance costs 500 / 800 |
| Invalid and missing input | `InvalidLine_IsRejected_...` (10 inputs), `MissingInput_IsRejected_...`, `Dataset_RejectsDuplicates_...` | no request, message present; `null` throws `ArgumentNullException` |
| Two policies | `TwoPolicies_SameInput_DifferentResults_SamePipeline` | D1: 700 vs 1100 |
| Purity and stages | `Stage1_Alone_...`, `Stage2_Alone_...`, `Stage3_Alone_...`, `Workflow_IsPure_...` | input arrays unchanged; delegate called once per request; same input gives same output |
| Configuration stability | `Policy_CapturesItsConfiguration_...`, `ChangingTheLimits_...` | later changes to variables do not alter the policy; wider limit includes D4 |

16 test methods (24 test cases with the `Theory` rows).
**Latest `dotnet test` result:** _paste the summary line here after running the tests on your machine, e.g. "Passed: 24, Failed: 0"._

## How to build, run and test

- **SDK:** .NET SDK 8.0 (`dotnet --version` should start with `8.`). Test packages (xUnit) are restored from NuGet, so internet access is needed once.
- **Working directory:** the solution folder (the one containing `DeliveryQuotes.sln`).

```
dotnet build
dotnet test
dotnet run --project src/DeliveryQuotes                                   # data/deliveries.csv, both policies
dotnet run --project src/DeliveryQuotes -- data/deliveries.csv express    # one policy: standard | express | both
```

Demonstrations:
- **Normal:** `dotnet run --project src/DeliveryQuotes` → output as in the tables above.
- **Empty dataset:** `dotnet run --project src/DeliveryQuotes -- data/empty.csv` → "(no quotes)", total 0.
- **Invalid input:** `dotnet run --project src/DeliveryQuotes -- data/invalid.csv` → three rejected lines with messages; the valid line is still priced.
- **Missing file:** `dotnet run --project src/DeliveryQuotes -- nope.csv` → error message, exit code 1.

## Git (optional for this assignment, required for the final project)

`.gitignore` excludes `bin/`, `obj/`, `.vs/`, `.idea/` and local secrets. Basic flow: `git init`, `git add .`, `git commit -m "Add initial pipeline"`,
create a repository on GitHub/GitLab, `git remote add origin REPOSITORY_URL`, `git push -u origin main`; teammates `git clone`, work in a feature branch and open a pull request.
