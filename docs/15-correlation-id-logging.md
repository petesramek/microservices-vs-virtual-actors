# Correlation ID logging

The sample uses lightweight header-based correlation for scenario diagnostics.

## Header

The UI sends a correlation ID on scenario execution using:

`X-Correlation-ID`

The gateway stores the value in an asynchronous correlation context and forwards it to backend HTTP calls. Backend APIs add the value to structured logging scopes when the header is present.

## Why this is not part of the scenario contract

Correlation IDs are diagnostic metadata rather than business data. Keeping correlation in headers avoids mixing observability concerns into scenario request and result contracts.

## Production note

This sample uses a pragmatic custom header because it is easy to demonstrate. A production system would commonly use W3C Trace Context, `Activity`, and OpenTelemetry for distributed tracing.
