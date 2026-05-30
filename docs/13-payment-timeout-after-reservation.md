# Payment timeout after reservation

The payment timeout scenario models an ambiguous downstream payment failure after inventory has already been reserved.

## Expected behavior

- one request submission is received
- inventory is reserved
- payment authorization times out
- inventory reservation is released
- the order is rejected with reason `PaymentTimeout`
- remaining inventory returns to the initial stock

## Demo strategy

For this sample, timeout is treated as a failed payment and the reservation is released immediately. A production system might instead move the order to a pending payment confirmation state because a timeout does not always prove that the downstream payment operation failed.
