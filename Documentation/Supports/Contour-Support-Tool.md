# Contour Support Tool

This document records the intended role of the Contour Support tool in the support architecture.

## Purpose

Contour Support creates a support group from a contour extracted from a model face region at a chosen height or slicing rule. It is useful when supports need to follow an edge loop or planar slice of a selected region.

## Feature Definition

The saved feature should store enough information to regenerate the contour and redistribute supports later. Typical inputs include:

- seed point and seed triangle
- contour height or slicing rule
- coplanar or connectivity thresholds
- spacing
- start offset
- final offset

## Regeneration Rule

During regeneration, the tool should:

1. rebuild the selected connected face patch or contour source
2. extract the contour from the saved rule
3. apply offsets and trimming
4. redistribute support positions along the contour
5. regenerate concrete support entities

## CAD Rule

The contour definition is durable user intent. The resulting supports are derived output and can be replaced whenever the source feature changes.
