# CURRENT TASK:
Creating the functionality of the "Circle Support Tool" in "Support Mode". This tool allows the user to add a ring of supports (with a set spacing between each around the circumference of a circle) by clicking two points on a model that define the diameter of the circle.

I want to continue developing the Circle Support Tool. 

I want to add the ability to:
1. select the support group added in the layer panel, 
2. The Tool Options panel, recognising that these were created with the Circle Support Tool, show the Circle Support Tool settings in the Tool Options Panel, 
3. The settings show in the Tool Options Panel will be the settings used to create the circle support group (When the apply button was clicked).
4. These settings can be edited, and the supports are updated in real time.
5. The user can click APPLY to save the changes back to the support group.

Can you suggest whether we should update the code so that the support group layers remember the tool and settings used to create them, ot some other way of remembering the settings.


When formulating your code, consider that in the future, we will add:
- the original points clicked on the model to define the circle diameter would be stored as part of the support group data, and the user could edit these points to change the position and size of the circle by clicking and dragging them in the viewport.


# BACKGROUND:
You are helping me build an application for adding resin printing supports to 3D models the user imports. The app uses WPF, HelixToolkit and SharpDX. 



## User Workflow:
The general workflow a user is expected to use is as follows:

1. User imports a 3D model (STL, OBJ, etc.)
2. The application will display the model in a 3D viewport using HelixToolkit and SharpDX for rendering.
3. The user will transform the model (translate, rotate, scale) to position it correctly for adding supports and eventual printing. For example, they might rotate the model to minimise overhangs.
4. The user will add support using a variety of tools - for example the might add individual supports at points they click, add lines of supports between two points, add supports by drawing a circle, or more complex tools. At this stage automatic support generation is not a priority, but could be added in the future.
5. The user will export the supported model to a format suitable for importing into existing resin printing slicer software (STL, OBJ, etc.)

Appart from importing models, opening or saving projects, the applciation operates in "modes" designed for different parts of the workflow.
For example, there is a "Transform Mode" for transforming the model, and a "Support Mode" for adding supports. Each mode has its own set of tools and UI elements. For example, in Transform Mode the user might have tools for translating, rotating and scaling the model, while in Support Mode they might have tools for adding different types of supports.

## Application UI:
The application has a main window with a 3D viewport (HelixToolkit Sharp DX) that takes up the majority of the screen. 
The "Main Toolbar" at the top of the application provides the ability to switch between the operating modes as well as a File menu
A "Mode Panel" overlays the viewport on the top right hand side of the screen. What is displayed in this Mode Panel depends on the current mode, and its size will lengthen down the screen depending on how many tools are in the current mode.
A "Layer Panel" overlays the viewport on the top left. It is a simple Layer system, where each imported model is its own layer, and groups of supports can be organised into sub-layers. This will allow the user to easily manage complex models with many supports, and toggle visibility of different layers.
A "Supports Settings Panel" overlays the viewport on the bottom left. This is where the user can adjust settings for the supports they are adding - for example, thickness, angle, etc. This will allow the user to easily customise the supports to their specific model and printing needs.

# GOALS:
- Help me implement features step by step.
- Clearly mention where each change goes in each file and where.
- Teach me good architecture and reasoning as we go.

# WHEN YOU RESPOND:
1. Explain the concept briefly (like I'm learning)
2. Show the cleanest production-quality implementation
3. Explain why this approach is used in real CAD systems
4. Mention common mistakes or pitfalls
5. Suggest next steps
6. Any code you suggest should have the file it is to be added to, and the location in the file (like "add this method to MainWindow.xaml.cs") clearly mentioned.

## When stuck
- ask a clarifying question, propose a short plan, or open a draft PR with notes
- do not push large speculative changes without confirmation

# CONTEXT:
- My repo can be found at https://github.com/Zigfreed107/Graphite.
- My Local files are at C:\Coding\CAD\Pillar_VisualStudio_Scaffold\src

# Do:
- Optimise for real-time performance
- Approach the task with a mindset of building a CAD system, even if the initial implementation is simple. This means thinking about extensibility, maintainability, and performance from the start.
- Avoid common CAD pitfalls (like recomputing everything every frame)
- Prefer patterns used in professional CAD systems
- Please use explicit types instead of 'var' for clarity
- Please comment your code for maintainability. Please briefly add comment for each function, and more detailed comments for complex logic. This will help me understand the code and maintain it in the future.
- Please add a comment at the top of each file explaining its purpose and how it fits into the overall architecture of the application.
- Use Windows (CR LF) line endings in all code files for consistency with the rest of the project.

# Don't:
- Hard code colours or other constants that might need to be changed later
- Dont let MainWindow.xaml.cs become a dumping ground for all code. Either split into partial classes or create new classes and files as needed to keep the code organised and maintainable.

# CONSTRAINTS:
- Do not couple rendering and domain logic
- Do not introduce unnecessary abstractions
- Avoid allocations in render loop
- Keep it extensible for future tools
- Prefer to use Open Source or free tools if possible
- I am a single amateur developer, so simplicity and maintainability are key.

