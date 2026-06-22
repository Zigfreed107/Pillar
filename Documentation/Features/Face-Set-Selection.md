# Face Set Selection

This document describes the Face Set Selection Tool as a reusable helper for tools that need a set of selected mesh faces.

## Purpose

The Face Set Selection Tool builds a temporary selection set of mesh faces and returns the accepted result to the calling tool. It is not itself a durable document feature.

## User Workflow

1. Launch the helper from a client tool or a temporary development entry point.
2. The floating tool panel opens.
3. The session starts with the caller's current face selection set.
4. The user adds to or removes from the set using Select, Line Select, or Angle Select.
5. Undo, redo, and clear operate within the temporary session.
6. Clicking OK returns the accepted face set to the caller.

## Selection Operations

- Select
  Click individual faces.
- Line Select
  Draw a screen-space polyline and collect front-most visible faces crossed by the line.
- Angle Select
  Pick a seed face and grow the selection through connected neighbors whose normal-angle difference is within the configured threshold.

## Domain Result

The output should be a collection of renderer-independent face identifiers such as `FaceSelectionKey`. The consuming tool is responsible for deciding what those faces mean and whether they should be saved in project data.

## Architecture

- geometry analysis code should own face traversal, adjacency, and angle-based growth
- rendering should own hit testing and visual overlays
- the face-set session tool should own temporary interaction state
- the client tool should own durable storage of accepted face selections, if needed

## CAD Rule

Treat face selection as lightweight input to another feature, not as a heavyweight document object by default.
