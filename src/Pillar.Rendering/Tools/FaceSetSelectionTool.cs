// FaceSetSelectionTool.cs
// Runs a temporary face-selection editing session for client tools without storing helper state in the CAD document.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Selection;
using Pillar.Core.Tools;
using Pillar.Geometry.Analysis;
using Pillar.Rendering.Scene;
using HelixToolkit.Maths;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Windows.Input;

namespace Pillar.Rendering.Tools;

/// <summary>
/// Describes the face picking operation currently used by the face-set selection helper.
/// </summary>
public enum FaceSetSelectionToolKind
{
    Select,
    LineSelect,
    AngleSelect
}

/// <summary>
/// Describes whether newly picked faces add to or remove from the temporary selection set.
/// </summary>
public enum FaceSetSelectionModifier
{
    Add,
    Remove
}

/// <summary>
/// Describes the screen-space Line Select preview segment drawn by the WPF shell.
/// </summary>
public readonly struct FaceSetLineSelectionPreviewState
{
    /// <summary>
    /// Creates immutable preview state for the active Line Select segment.
    /// </summary>
    public FaceSetLineSelectionPreviewState(bool isVisible, Vector2 start, Vector2 end)
    {
        IsVisible = isVisible;
        Start = start;
        End = end;
    }

    /// <summary>
    /// Gets whether the preview line should be visible.
    /// </summary>
    public bool IsVisible { get; }

    /// <summary>
    /// Gets the screen-space segment start in viewport coordinates.
    /// </summary>
    public Vector2 Start { get; }

    /// <summary>
    /// Gets the screen-space segment end in viewport coordinates.
    /// </summary>
    public Vector2 End { get; }
}

/// <summary>
/// Edits a temporary set of mesh faces and returns that set to the launcher only when accepted.
/// </summary>
public sealed class FaceSetSelectionTool : ITool
{
    private readonly SceneManager _scene;
    private readonly HashSet<FaceSelectionKey> _selectedFaces = new HashSet<FaceSelectionKey>();
    private readonly List<FaceSelectionKey> _candidateFaces = new List<FaceSelectionKey>(256);
    private readonly List<int> _candidateTriangleIndices = new List<int>(256);
    private readonly Stack<HashSet<FaceSelectionKey>> _undoHistory = new Stack<HashSet<FaceSelectionKey>>();
    private readonly Stack<HashSet<FaceSelectionKey>> _redoHistory = new Stack<HashSet<FaceSelectionKey>>();
    private readonly Color4 _selectionColor;
    private Vector2? _lineSelectPreviousPoint;

    /// <summary>
    /// Creates a face-set selection session with an optional initial face set from the client tool.
    /// </summary>
    public FaceSetSelectionTool(
        CadDocument document,
        SceneManager scene,
        IReadOnlyCollection<FaceSelectionKey> initialSelection,
        Color4 selectionColor)
    {
        _ = document ?? throw new ArgumentNullException(nameof(document));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _selectionColor = selectionColor;

        if (initialSelection != null)
        {
            foreach (FaceSelectionKey face in initialSelection)
            {
                _selectedFaces.Add(face);
            }
        }

        ApplySelectionOverlay();
    }

    /// <summary>
    /// Raised when the selection count or undo state changes.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    /// Raised when Line Select needs the shell to show or hide its screen-space segment preview.
    /// </summary>
    public event Action<FaceSetLineSelectionPreviewState>? LineSelectionPreviewChanged;

    /// <summary>
    /// Gets the active face selection operation.
    /// </summary>
    public FaceSetSelectionToolKind ToolKind { get; private set; } = FaceSetSelectionToolKind.Select;

    /// <summary>
    /// Gets the active add/remove modifier.
    /// </summary>
    public FaceSetSelectionModifier Modifier { get; private set; } = FaceSetSelectionModifier.Add;

    /// <summary>
    /// Gets the number of faces currently in the temporary selection set.
    /// </summary>
    public int SelectedFaceCount
    {
        get { return _selectedFaces.Count; }
    }

    /// <summary>
    /// Gets whether the session-local history has an undoable selection edit.
    /// </summary>
    public bool CanUndo
    {
        get { return _undoHistory.Count > 0; }
    }

    /// <summary>
    /// Gets whether the session-local history has a redoable selection edit.
    /// </summary>
    public bool CanRedo
    {
        get { return _redoHistory.Count > 0; }
    }

    /// <summary>
    /// Gets or sets the angle threshold used by angle-grow selection.
    /// </summary>
    public double CoplanarThresholdDegrees { get; set; } = 15.0;

    /// <summary>
    /// Changes the active face selection operation and resets transient line-select state.
    /// </summary>
    public void SetToolKind(FaceSetSelectionToolKind toolKind)
    {
        if (ToolKind == toolKind)
        {
            if (toolKind == FaceSetSelectionToolKind.LineSelect)
            {
                ClearLineSelectionPreview();
                RaiseStateChanged();
            }

            return;
        }

        ClearLineSelectionPreview();
        ToolKind = toolKind;
        RaiseStateChanged();
    }

    /// <summary>
    /// Changes whether candidate faces are added to or removed from the temporary selection.
    /// </summary>
    public void SetModifier(FaceSetSelectionModifier modifier)
    {
        Modifier = modifier;
        RaiseStateChanged();
    }

    /// <summary>
    /// Handles face-set clicks according to the active selection operation.
    /// </summary>
    public void OnMouseDown(Vector2 screenPosition)
    {
        FaceSetSelectionModifier effectiveModifier = GetEffectiveModifier();

        if (ToolKind == FaceSetSelectionToolKind.Select)
        {
            ApplySingleFaceSelection(screenPosition, effectiveModifier);
            return;
        }

        if (ToolKind == FaceSetSelectionToolKind.AngleSelect)
        {
            ApplyAngleSelection(screenPosition, effectiveModifier);
            return;
        }

        ApplyLineSelectionPoint(screenPosition, effectiveModifier);
    }

    /// <summary>
    /// Updates the screen-space preview segment when Line Select has an anchor point.
    /// </summary>
    public void OnMouseMove(Vector2 screenPosition)
    {
        if (ToolKind != FaceSetSelectionToolKind.LineSelect || !_lineSelectPreviousPoint.HasValue)
        {
            return;
        }

        LineSelectionPreviewChanged?.Invoke(new FaceSetLineSelectionPreviewState(
            true,
            _lineSelectPreviousPoint.Value,
            screenPosition));
    }

    /// <summary>
    /// Completes click-only gestures; line selection commits on each new anchor click.
    /// </summary>
    public void OnMouseUp(Vector2 screenPosition)
    {
        _ = screenPosition;
    }

    /// <summary>
    /// Cancels transient previews and removes temporary face highlighting.
    /// </summary>
    public void Cancel()
    {
        ClearLineSelectionPreview();
        _scene.ClearFaceSelection();
    }

    /// <summary>
    /// Clears all selected faces as one undoable session edit.
    /// </summary>
    public void ClearSelection()
    {
        if (_selectedFaces.Count == 0)
        {
            return;
        }

        PushUndoSnapshot();
        _selectedFaces.Clear();
        _redoHistory.Clear();
        ApplySelectionOverlay();
        RaiseStateChanged();
    }

    /// <summary>
    /// Restores the previous face-selection snapshot.
    /// </summary>
    public void Undo()
    {
        if (_undoHistory.Count == 0)
        {
            return;
        }

        _redoHistory.Push(new HashSet<FaceSelectionKey>(_selectedFaces));
        ReplaceSelection(_undoHistory.Pop());
    }

    /// <summary>
    /// Reapplies the most recently undone face-selection snapshot.
    /// </summary>
    public void Redo()
    {
        if (_redoHistory.Count == 0)
        {
            return;
        }

        _undoHistory.Push(new HashSet<FaceSelectionKey>(_selectedFaces));
        ReplaceSelection(_redoHistory.Pop());
    }

    /// <summary>
    /// Returns an immutable copy of the temporary selection for the launcher to hand to its client tool.
    /// </summary>
    public IReadOnlyCollection<FaceSelectionKey> CreateResult()
    {
        return new ReadOnlyCollection<FaceSelectionKey>(new List<FaceSelectionKey>(_selectedFaces));
    }

    /// <summary>
    /// Selects or removes one face under the click point.
    /// </summary>
    private void ApplySingleFaceSelection(Vector2 screenPosition, FaceSetSelectionModifier modifier)
    {
        if (!_scene.TryHitMeshFace(screenPosition, out MeshEntity mesh, out int triangleIndex))
        {
            return;
        }

        _candidateFaces.Clear();
        _candidateFaces.Add(new FaceSelectionKey(mesh.Id, triangleIndex));
        ApplyCandidateFaces(_candidateFaces, modifier);
    }

    /// <summary>
    /// Grows selection through neighbouring faces whose normal difference is inside the configured threshold.
    /// </summary>
    private void ApplyAngleSelection(Vector2 screenPosition, FaceSetSelectionModifier modifier)
    {
        if (!_scene.TryHitMeshFace(screenPosition, out MeshEntity mesh, out int triangleIndex))
        {
            return;
        }

        _candidateTriangleIndices.Clear();
        FaceSetSelectionAnalyzer.FillConnectedCoplanarTriangles(mesh, triangleIndex, CoplanarThresholdDegrees, _candidateTriangleIndices);
        _candidateFaces.Clear();

        for (int i = 0; i < _candidateTriangleIndices.Count; i++)
        {
            _candidateFaces.Add(new FaceSelectionKey(mesh.Id, _candidateTriangleIndices[i]));
        }

        ApplyCandidateFaces(_candidateFaces, modifier);
    }

    /// <summary>
    /// Adds one polyline point and selects all faces crossed by the newest segment.
    /// </summary>
    private void ApplyLineSelectionPoint(Vector2 screenPosition, FaceSetSelectionModifier modifier)
    {
        if (!_lineSelectPreviousPoint.HasValue)
        {
            _lineSelectPreviousPoint = screenPosition;
            return;
        }

        _candidateFaces.Clear();

        _scene.FillVisibleMeshFacesCrossedByScreenLine(
            _lineSelectPreviousPoint.Value,
            screenPosition,
            _candidateFaces);

        ApplyCandidateFaces(_candidateFaces, modifier);
        _lineSelectPreviousPoint = screenPosition;
        HideLineSelectionPreview();
    }

    /// <summary>
    /// Applies candidate faces to the temporary selection using add or remove semantics.
    /// </summary>
    private void ApplyCandidateFaces(IReadOnlyList<FaceSelectionKey> candidateFaces, FaceSetSelectionModifier modifier)
    {
        if (candidateFaces.Count == 0)
        {
            return;
        }

        PushUndoSnapshot();
        bool didChangeSelection = false;

        for (int i = 0; i < candidateFaces.Count; i++)
        {
            if (modifier == FaceSetSelectionModifier.Remove)
            {
                didChangeSelection |= _selectedFaces.Remove(candidateFaces[i]);
                continue;
            }

            didChangeSelection |= _selectedFaces.Add(candidateFaces[i]);
        }

        if (!didChangeSelection)
        {
            _undoHistory.Pop();
            return;
        }

        _redoHistory.Clear();
        ApplySelectionOverlay();
        RaiseStateChanged();
    }

    /// <summary>
    /// Replaces the temporary selection with a stored history snapshot.
    /// </summary>
    private void ReplaceSelection(HashSet<FaceSelectionKey> snapshot)
    {
        _selectedFaces.Clear();

        foreach (FaceSelectionKey face in snapshot)
        {
            _selectedFaces.Add(face);
        }

        ApplySelectionOverlay();
        RaiseStateChanged();
    }

    /// <summary>
    /// Stores the current selection before an undoable edit.
    /// </summary>
    private void PushUndoSnapshot()
    {
        _undoHistory.Push(new HashSet<FaceSelectionKey>(_selectedFaces));
    }

    /// <summary>
    /// Pushes the current temporary selection into the scene overlay.
    /// </summary>
    private void ApplySelectionOverlay()
    {
        _scene.ConfigureFaceSelection(_selectedFaces, _selectionColor);
    }

    /// <summary>
    /// Reads keyboard overrides so Shift temporarily adds and Alt temporarily removes faces.
    /// </summary>
    private FaceSetSelectionModifier GetEffectiveModifier()
    {
        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            return FaceSetSelectionModifier.Remove;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            return FaceSetSelectionModifier.Add;
        }

        return Modifier;
    }

    /// <summary>
    /// Clears the current line anchor and hides the screen-space preview.
    /// </summary>
    private void ClearLineSelectionPreview()
    {
        _lineSelectPreviousPoint = null;
        HideLineSelectionPreview();
    }

    /// <summary>
    /// Requests that the shell hide the current line-select preview segment.
    /// </summary>
    private void HideLineSelectionPreview()
    {
        LineSelectionPreviewChanged?.Invoke(new FaceSetLineSelectionPreviewState(false, Vector2.Zero, Vector2.Zero));
    }

    /// <summary>
    /// Notifies the panel that count and history labels should refresh.
    /// </summary>
    private void RaiseStateChanged()
    {
        StateChanged?.Invoke();
    }
}
