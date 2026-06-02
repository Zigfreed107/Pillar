# Current Task
When supports are added, they should avoid passing through the model they are attached to. 

## User Interface
In the Support Presets Window:
- Add a section called "Branch" between the Stem and the Head sections.
- Add a numeric input with label "Maximum branch length".
	- This input must be greater or equal to zero.
- Add a numeric input with label "Model Clearance".
	- This input must be greater or equal to zero. 
	- The default is 3.

# Logic
- Between the Head and the Stem, a new cylinder shape called the "Branch" needs to be added. 
	- The Branch has the same diameter as the top of the Stem.
	- The Branch should have the same number of polar segmants as the Stem and head meshes.
	- A ball(sphere) mesh drawn between the Head and the Stem should now be created between the Branch and Stem.
	- A ball(sphere) mesh should be created between the Head and the Branch. It should have the same diameter as the Branch.
	- The Branch mesh should be closed at the top and bottom since we can't guarentee it will merge nicely with the balls, and it is important the mesh is closed for slicing when printing.
	- The Branch length (length of the cylinder) should extend until the Stem no longer intersects the model the support is attached to by the Model Clearance distance specified in the Support Presets Window.
	- There is a maximum length the Branch can extend. This is set in the Support Presets Window with the "Maximum branch length" parameter. If the support cannot be created at this maximum height, then the support should not be created.
	- Try to keep the code that calculates the Branch length as efficient and fast as possible. This is important because it will be called many times during the support generation process.
	- If without the Branch the support does not intersect the model (within the Model Clearance distance), then the Branch should not be created. This is to avoid creating unnecessary geometry when it is not needed. Only one ball should be created between the Head and the Stem in this case.

# Notes
- Review "SupportFunctionality.md" in the "Documentation" folder for more information on how supports are currently implemented in Graphite. This will help you understand where to add the new Branch logic and how it fits into the existing support generation process.