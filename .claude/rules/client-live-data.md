---
paths:
  - "src/client/**/*.razor"
  - "src/client/ui/**/*.cs"
---

# Live data in client apps

- Data a client app displays must come from an observable update source: an `IObservable<T>` contract in `Contracts/`, subscribed to by the page.
- Subscribe in `OnInitializedAsync`, refresh through `InvokeAsync`, and dispose the subscription in `Dispose`.
- A change path that does not publish to that source is incomplete.
