# Problem

This repository compares two ways of implementing the same stateful distributed workflow.

The workflow is intentionally small: placing an order, reserving inventory, authorizing payment, and completing or rejecting the order.

The interesting part is not ecommerce. The interesting part is how state, concurrency, failure handling, deployment, and scaling change between service-oriented and virtual-actor-oriented designs.
