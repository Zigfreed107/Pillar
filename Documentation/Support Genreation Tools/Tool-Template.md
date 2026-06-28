# Support Tool Template

Use this template when designing a new support tool so it fits the existing architecture.

## Purpose

State what kind of support pattern the tool creates and why a user would choose it.

## User Workflow

Describe:

1. how the user activates the tool
2. what they pick or draw
3. what the viewport previews
4. what parameters appear in the tool options panel
5. what Apply, Finish, Close, or Cancel do

## Stored Feature Definition

List the compact settings that become the durable source of truth for the support group.

## Regeneration Behaviour

Describe how the saved feature is rebuilt when:

- the user edits the generator settings
- the owning model transform changes
- the project is loaded

## Preview And Rendering Responsibilities

List which parts are transient preview state and which rendering components own the preview visuals.

## Undo Redo Behaviour

State which actions are preview-only and which actions create commands.

## Risks And Edge Cases

Note invalid picks, failure conditions, minimum-size rules, and how the tool should fail safely.
