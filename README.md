# Delivery Quotes Calculator

**Course:** Introduction to Functional Programming - Assignment 4  
**Project Operation:** Calculate delivery quotes based on distance, weight, and configurable pricing policies.  
**Intended Users:** Logistics dispatchers who need to quickly evaluate a batch of delivery requests, filter out those exceeding service limits, and calculate final costs using either a standard or express rate.

## Team & Responsibilities

The workload is divided to ensure both members implement meaningful domain logic, contribute pure functions, and write automated tests.

| Member | Group | Implemented Logic | Automated Tests & Docs |
| :--- | :--- | :--- | :--- |
| **Tamerlan Iskakov** | IT-2504 | `Models.cs` (immutable structures), `DeliveryParser.cs` (I/O boundary validation), `Program.cs` (console integration) | Dataset preparation, parser validation tests, missing/invalid input tests, duplicate handling tests. |
| Madina Kairbayeva | IT-2504 | `Stages.cs` (pure transformation stages), `PricingPolicies.cs` (higher-order rule configurations), `DeliveryWorkflow.cs` | Workflow purity tests, boundary values, two-policy comparison, configuration stability tests. |

## Business Rules & Workflow

The application processes raw text input into structured records, filters them, and applies a pricing function. 

**Pipeline Architecture:**  
`Raw CSV Lines` => **ParseDataset** (Boundary) => `DeliveryRequest` => **Stage 1 (Select)** => **Stage 2 (Price)** => **Stage 3 (Report)** => `QuoteReport`

1. **Input Validation:** Each line must contain exactly 3 comma-separated fields: `id`, `distanceKm`, `weightKg`. IDs cannot be blank or duplicated. Numeric values must be non-negative (0 is allowed for same-location drop-offs). Invalid lines are skipped and reported with their exact line number.
2. **Service Limits (Stage 1):** The delivery is strictly rejected if it exceeds `50 km` in distance or `30 kg` in weight. Limits are inclusive.
3. **Configurable Pricing (Stage 2):** Cost is calculated using a linear formula: `Fixed Fee + (Rate × Distance)`.
   * **Standard Policy:** 500 fixed + 100/km
   * **Express Policy:** 800 fixed + 150/km

## Dataset & Expected Results

We prepared a core dataset (`data/deliveries.csv`) covering normal cases, exact boundaries, and invalid inputs.

| ID | Distance | Weight | Status / Reason | Standard Cost | Express Cost |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **D1** | 2 | 3 | Accepted (Normal) | 700 | 1100 |
| **D2** | 10 | 5 | Accepted (Normal) | 1500 | 2300 |
| **D3** | 50 | 30 | Accepted (**Exact Boundary**) | 5500 | 8300 |
| **D4** | 60 | 5 | Excluded (Distance > 50) | - | - |
| **D5** | 0 | 1 | Accepted (Zero distance) | 500 | 800 |
| **D6** | -4 | 2 | Rejected (Negative value) | - | - |
| **D7** | abc | 2 | Rejected (Parse failure) | - | - |
| **D8** | 20 | 45 | Excluded (Weight > 30) | - | - |

**Expected Output Totals:** Standard = **8200**, Express = **12500**.  
Excluded IDs: `D4`, `D8`. Rejected lines: `6`, `7`.

## Pure Transformation Stages

All business logic resides in `src/DeliveryQuotes/Stages.cs` using pure functions. They do not mutate arguments, rely on global state, or perform I/O.

1. **`SelectDeliverable`**: Takes `IEnumerable<DeliveryRequest>` and `ServiceLimits`. Returns a `Selection` record separating requests into `Eligible` and `Excluded` immutable arrays.
2. **`Price`**: A higher-order function that takes the eligible requests and a delegate `Func<DeliveryRequest, decimal>`. It maps each request to a `PricedRequest` by invoking the injected rule.
3. **`BuildReport`**: Takes the priced requests and excluded IDs to build the final `QuoteReport`, sorting the valid quotes by cost (ascending) and calculating the total sum.

## Technical Requirements Map

| Task Requirement | Implementation Location |
| :--- | :--- |
| **Task 1: Operation & Dataset** | Described in this file, dataset located in `data/deliveries.csv`. |
| **Task 2: Immutable Models** | `Models.cs` (Uses `record` types and `ImmutableArray<T>`). |
| **Task 2: Boundary Validation** | `DeliveryParser.cs` (Explicit `TryParse` logic, no silent defaults). |
| **Task 3: Three Pure Stages** | `Stages.cs` (`SelectDeliverable`, `Price`, `BuildReport`). |
| **Task 4: Configurable Rule** | `Stages.Price` receives a `Func<DeliveryRequest, decimal>` delegate. |
| **Task 4: Two Policies** | `PricingPolicies.cs` defines `Standard` and `Express` configurations. |
| **Task 5: Functional Purity** | No side effects in `Stages.cs` or `DeliveryParser.cs`. I/O isolated in `Program.cs`. |

## Testing

The solution includes automated testing using xUnit (`tests/DeliveryQuotes.Tests/DeliveryTests.cs`). Run tests via `dotnet test`.

* **Normal Workflow:** Asserts documented quotes and totals for both Standard and Express policies.
* **Empty/Missing Input:** Asserts graceful handling of empty lists and `ArgumentNullException` for missing data.
* **Boundary Values:** Validates exact limits (50/30) are included, while 50.01/30.01 are excluded.
* **Purity & Isolation:** Checks that input arrays remain `SequenceEqual` before and after processing.
* **Higher-Order Function:** Asserts the policy delegate is correctly captured and invoked per record.

**Current Test Status:** `Passed: 24, Failed: 0`.

## Execution Instructions

Requires .NET 8.0 SDK. Run the following commands from the root directory (where `.sln` is located):

```bash
# Build the project
dotnet build

# Run unit tests
dotnet test

# Run with both policies on the default dataset
dotnet run --project src/DeliveryQuotes

# Run with a specific policy
dotnet run --project src/DeliveryQuotes -- data/deliveries.csv standard
