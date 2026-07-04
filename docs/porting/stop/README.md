# STOP queue

One file per SET+COLOR batch: `<SET>.<COLOR>.md` (e.g. `BT1.Red.md`).

The `porter` agent appends a line for every card (or branch) it cannot port 1:1
with a catalog primitive:

```
<ID> | reason | original symbol
```

Reasons are the escalation conditions: factory not in the catalog, timing missing
in headless, nested custom coroutine/class logic, or a special-play recipe not
expressible via a catalog factory.

The `reviewer` agent (강모델) consumes these during `port-review <SET>`: it
develops the missing primitive, adds it to `docs/porting/PRIMITIVE-CATALOG.md`,
and marks the card for recovery by a re-run of `port-set <SET> <COLOR>`.
