// CadCommandRunner.cs
// Executes CAD commands and keeps undo/redo history separate from tools and rendering.
using System;
using System.Collections.Generic;

namespace CadApp.Commands;

/// <summary>
/// Runs document commands and stores the command history needed for undo and redo.
/// </summary>
public sealed class CadCommandRunner
{
    private readonly List<ICadCommand> _undoHistory = new List<ICadCommand>();
    private readonly List<ICadCommand> _redoHistory = new List<ICadCommand>();
    private readonly int _maxUndoSteps;

    /// <summary>
    /// Creates a command runner with a bounded undo history.
    /// </summary>
    public CadCommandRunner(int maxUndoSteps)
    {
        _maxUndoSteps = Math.Max(1, maxUndoSteps);
    }

    /// <summary>
    /// Raised whenever undo or redo availability changes.
    /// </summary>
    public event Action? HistoryChanged;

    /// <summary>
    /// Gets whether there is a command that can be undone.
    /// </summary>
    public bool CanUndo
    {
        get { return _undoHistory.Count > 0; }
    }

    /// <summary>
    /// Gets whether there is a command that can be redone.
    /// </summary>
    public bool CanRedo
    {
        get { return _redoHistory.Count > 0; }
    }

    /// <summary>
    /// Executes a new command and records it for future undo.
    /// </summary>
    public void Execute(ICadCommand command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        command.Execute();
        _undoHistory.Add(command);
        TrimUndoHistoryToLimit();
        _redoHistory.Clear();
        RaiseHistoryChanged();
    }

    /// <summary>
    /// Undoes the most recently executed command when one is available.
    /// </summary>
    public ICadCommand? Undo()
    {
        if (_undoHistory.Count == 0)
        {
            return null;
        }

        int commandIndex = _undoHistory.Count - 1;
        ICadCommand command = _undoHistory[commandIndex];
        _undoHistory.RemoveAt(commandIndex);

        command.Undo();
        _redoHistory.Add(command);
        RaiseHistoryChanged();

        return command;
    }

    /// <summary>
    /// Re-executes the most recently undone command when one is available.
    /// </summary>
    public ICadCommand? Redo()
    {
        if (_redoHistory.Count == 0)
        {
            return null;
        }

        int commandIndex = _redoHistory.Count - 1;
        ICadCommand command = _redoHistory[commandIndex];
        _redoHistory.RemoveAt(commandIndex);

        command.Execute();
        _undoHistory.Add(command);
        TrimUndoHistoryToLimit();
        RaiseHistoryChanged();

        return command;
    }

    /// <summary>
    /// Clears command history when the document is replaced outside the undoable edit stream.
    /// </summary>
    public void ClearHistory()
    {
        _undoHistory.Clear();
        _redoHistory.Clear();
        RaiseHistoryChanged();
    }

    /// <summary>
    /// Removes the oldest undo commands when the history grows beyond the configured limit.
    /// </summary>
    private void TrimUndoHistoryToLimit()
    {
        while (_undoHistory.Count > _maxUndoSteps)
        {
            _undoHistory.RemoveAt(0);
        }
    }

    /// <summary>
    /// Notifies UI code that command availability may have changed.
    /// </summary>
    private void RaiseHistoryChanged()
    {
        HistoryChanged?.Invoke();
    }
}
