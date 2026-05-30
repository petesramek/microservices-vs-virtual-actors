# Scenario expected results

This document records the regression expectations for the scenario runner.

## Successful order

- total request submissions: 1
- unique successful orders: 1
- rejected submissions: 0
- idempotent duplicate responses: 0
- remaining inventory: initial stock minus quantity

## Insufficient inventory

- total request submissions: 1
- unique successful orders: 0
- rejected submissions: 1
- idempotent duplicate responses: 0
- reason: `InsufficientInventory`

## Payment failure compensation

- total request submissions: 1
- unique successful orders: 0
- rejected submissions: 1
- idempotent duplicate responses: 0
- remaining inventory: initial stock
- reason: `PaymentFailed`

## Payment timeout after reservation

- total request submissions: 1
- unique successful orders: 0
- rejected submissions: 1
- idempotent duplicate responses: 0
- remaining inventory: initial stock
- reason: `PaymentTimeout`

## Hot product contention

With initial stock 25, quantity 1, and 50 concurrent requests:

- total request submissions: 50
- unique successful orders: 25
- rejected submissions: 25
- idempotent duplicate responses: 0
- remaining inventory: 0
- reason: `SomeOrdersRejected`

## Duplicate request

With initial stock 10, quantity 2, and 20 duplicate request submissions:

- total request submissions: 20
- unique successful orders: 1
- rejected submissions: 0
- idempotent duplicate responses: 19
- remaining inventory: 8
- reason: `IdempotentResultReturned`

