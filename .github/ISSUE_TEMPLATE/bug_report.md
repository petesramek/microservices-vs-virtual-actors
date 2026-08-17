---
name: Bug report
about: Create a report to help us improve
title: "[Bug]: "
description: Report a problem with the architecture workbench.
labels:
  - bug
assignees: 'petesramek'
body:
  - type: textarea
    id: problem
    attributes:
      label: What happened?
      description: Describe the problem and what you expected to happen.
      placeholder: Tell us what went wrong.
    validations:
      required: true

  - type: textarea
    id: reproduction
    attributes:
      label: How can we reproduce it?
      description: List the smallest set of steps needed to reproduce the problem.
      placeholder: |
        1. Start the repository using the Aspire AppHost.
        2. Open ...
        3. Select ...
        4. Observe ...
    validations:
      required: true

  - type: dropdown
    id: area
    attributes:
      label: Affected area
      options:
        - Aspire hosting
        - Microservices
        - Virtual actors
        - Workbench UI
        - Workbench Gateway
        - Health or topology
        - Build or tests
        - Documentation
        - Other
    validations:
      required: true

  - type: textarea
    id: context
    attributes:
      label: Relevant output
      description: Add a short error message, log excerpt, screenshot, or other useful context. Remove sensitive information.
    validations:
      required: false

  - type: checkboxes
    id: checks
    attributes:
      label: Before submitting
      options:
        - label: I searched the existing issues before submitting this report.
          required: true
---
