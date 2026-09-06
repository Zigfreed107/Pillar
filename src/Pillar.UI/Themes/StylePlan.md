# Styles
This document outlines a consistent approach to GUI styles and how and what it communicates to the user.

## Main Styles
Generally use the following style concepts:

### Framing
Used for panels, windows, borders - UI elements whose job it is to organise and frame the other controls. Needs to be subtle, and help to emphasise the controls that need to be interacted with.

### Standard
Used for most controls and UI elements such as panels, text, buttons, spinners, text boxes, etc.

### Accent
Used sparingly to draw attention to controls that apply, exit, or progress a tool or workflow. Lets the user identify a quick 'exit' back to the normal mode of the app.

### Accent 2
Used for text only, used as a subtitle accent.

### Subtle
Used on UI elements like text labels or drawings that are 'hints' for the user and don't require any interaction. Examples include in window help text, contextual information, drawings or paths that help visually link several controls.

### Title
Used on text labels that title a given panel or window.

### Floating
Use on individual controls like buttons that sit on top of the 3D Viewer but don't sit on or within a panel or border that would otherwise help visually isolate them from the detail drawn in the viewer. Generally this is a simple boost to their prominence (eg less transparency) while attempting to keep the look and feel the same to the user.

## Intra-style variations
A user can interact with a UI Control via:
- default (no interaction)
- focus (previously selected but mouse is not over the control)
- hover
- enabled (e.g. toggle button, check box)
- selected (e.g. press a button)

Each **Main Style** has variations depending on the user interaction, but each follows the same escalation in visual prominence:

- **default:** default colours
- **focused:** increase in border prominence
- **hover:** highest increase in border prominence
- **enabled:** hover border with increase in BG prominence
- **selected:** hover border with highest increase in BG prominence.


### Colours needed
BG_Transparent - standard BG colour. Transparent, naturally layers so panels on panels naturally stand out more.
BG_TransparentAccent
BG2_Opaque - used where transparency would be inappropriate (main toolbar).

Accent 1
- standard
- subtle

Accent 2
- standard
- subtle

Accent 2
- standard
- subtle