# Project
Pillar is a WPF CAD-style application for preparing resin-printing supports on imported 3D models.
Core stack: WPF, HelixToolkit, SharpDX.

# Priorities
- Optimize for real-time interaction performance.
- Keep rendering concerns separate from domain logic.
- Design for extensibility, but avoid unnecessary abstraction.
- Favor maintainability for a solo developer.

# Architecture Rules
- Do not couple rendering code to support or domain data structures.
- Avoid allocations in render loops and other hot paths.
- Do not let MainWindow.xaml.cs become a catch-all; split code into partials or focused classes when needed.
- Prefer patterns that scale to more tools, modes, and editing operations.

# Code Style
- Use explicit types instead of `var` unless the type is extremely obvious and surrounding code already uses implicit typing.
- Add a short file header comment explaining the file's role in the application.
- Add brief comments for each function, plus extra comments only where logic is non-obvious.
- Use Windows CRLF line endings in code files.

# Product Direction
- Imported models should behave like layers.
- Supports should remain editable and organized into support-related structures and layers.
- Undo and redo are desirable where they fit naturally into the workflow.

# When Working In Specific Areas
- For support behavior and tool rules, read `Documentation/Supports/Overview.md`.
- For UI workflow and panel responsibilities, read `Documentation/UI/Workflow.md` and `Documentation/UI/Panels.md`.
- For mode and tool conventions, read `Documentation/Architecture/Modes-And-Tools.md`.
- For rendering and domain boundaries, read `Documentation/Architecture/Rendering-Boundaries.md`.

# Collaboration
- Implement features step by step.
- Explain where each important change goes and why.
- If a change is speculative or high-risk, pause and confirm before making it.
