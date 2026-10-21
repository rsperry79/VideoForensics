---
paths:
  - "src/data/**"
  - "src/providers/**"
---

# Data requirements

- Every provider API field is parsed and stored in typed columns or tables. Design the schema first; never store JSON to dodge a schema gap.
- Raw JSON only for audit trail or fields that cannot be normalized, in an optional `MetadataJson` column. No unstructured JSON in core domain tables (events, devices, locations).
- Do not scope globally unique provider resources to an account (e.g. `ProviderLocationId`). Use junction tables for multi-account sharing.
- Run filtering, sorting, paging, counting, and aggregation on the server (EF query → SQL). Do not `ToListAsync()` and then filter, sort, or page in memory. Pull to the client only when the logic cannot translate to SQL; say why in a comment.
