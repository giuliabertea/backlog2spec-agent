# Spec: Mock Work Item

> Work Item: #1  
> Generated: 2026-05-15

---

## Goal

Add a mock feature to validate the pipeline end-to-end. The mock returns fixed data to allow testing without an LLM call.

## Behaviour

- Return a fixed spec when called with any enriched ticket
- Return an error result when input is flagged as invalid

## Edge Cases

- Null input
- Extremely large payload

## Out of Scope

Authentication, Authorization

## Files to Change

- **src/Backlog2SpecAgent.Cli/Agents/MockSpecGeneratorAgent.cs**: return mock GeneratedSpec
- **src/Backlog2SpecAgent.Cli/Agents/ISpecGeneratorAgent.cs**: interface contract
