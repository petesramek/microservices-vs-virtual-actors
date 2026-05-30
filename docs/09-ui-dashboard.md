# UI dashboard

The Blazor Server dashboard is intentionally small. It is not a product UI or ecommerce frontend.

The UI has three jobs:

1. Run the same scenario against one or both implementations.
2. Show side-by-side results.
3. Make topology and trade-offs visible without reading every source file.

## Pages

- `/` — scenario runner
- `/topology` — deployment and communication shape
- `/tradeoffs` — practical pros and cons

## Why Blazor Server

Blazor Server keeps the dashboard implementation inside the .NET solution. It allows form binding and validation without introducing a separate JavaScript build toolchain.

The dashboard is developer-facing, so Blazor Server's server-side circuit model is acceptable for this repository.
