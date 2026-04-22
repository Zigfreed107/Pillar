// DocumentFileService.cs
// Coordinates New, Open, and Save project commands for the WPF shell without putting file workflow in MainWindow.
using Pillar.Core.Document;
using Pillar.Core.Entities;
using Pillar.Core.Layers;
using Pillar.Core.Persistence;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace Pillar.UI.Services;

/// <summary>
/// Runs document file workflows that combine dialogs, persistence, and workspace reset callbacks.
/// </summary>
public sealed class DocumentFileService
{
    private const string GraphiteProjectFileFilter = "Graphite Project (*.gph)|*.gph";
    private const string GraphiteProjectDefaultExtension = ".gph";

    private readonly Window _owner;
    private readonly CadDocument _document;
    private readonly SelectionManager _selectionManager;
    private readonly GphDocumentSerializer _serializer;
    private readonly Action _cancelTransientToolState;
    private readonly Action _clearCommandHistory;
    private readonly Action _activateSelectionTool;

    /// <summary>
    /// Creates a service for New, Open, and Save document commands.
    /// </summary>
    public DocumentFileService(
        Window owner,
        CadDocument document,
        SelectionManager selectionManager,
        GphDocumentSerializer serializer,
        Action cancelTransientToolState,
        Action clearCommandHistory,
        Action activateSelectionTool)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _selectionManager = selectionManager ?? throw new ArgumentNullException(nameof(selectionManager));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _cancelTransientToolState = cancelTransientToolState ?? throw new ArgumentNullException(nameof(cancelTransientToolState));
        _clearCommandHistory = clearCommandHistory ?? throw new ArgumentNullException(nameof(clearCommandHistory));
        _activateSelectionTool = activateSelectionTool ?? throw new ArgumentNullException(nameof(activateSelectionTool));
    }

    /// <summary>
    /// Creates a new blank document after optionally saving the current document.
    /// </summary>
    public DocumentFileOperationResult New()
    {
        if (!ConfirmNewDocument(out DocumentFileOperationResult cancelledOrSaveResult))
        {
            return cancelledOrSaveResult;
        }

        ReplaceCurrentDocument(Array.Empty<CadEntity>(), Array.Empty<SupportLayerGroup>());
        return new DocumentFileOperationResult("New document", "New document");
    }

    /// <summary>
    /// Opens a saved Graphite project file and replaces the current document after confirmation.
    /// </summary>
    public DocumentFileOperationResult Open()
    {
        OpenFileDialog dialog = new OpenFileDialog
        {
            Title = "Open Graphite Project",
            Filter = GraphiteProjectFileFilter,
            DefaultExt = GraphiteProjectDefaultExtension,
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(_owner) != true)
        {
            return new DocumentFileOperationResult("Open cancelled", null);
        }

        if (!ConfirmReplaceCurrentDocument())
        {
            return new DocumentFileOperationResult("Open cancelled", null);
        }

        try
        {
            GphDocumentData documentData = _serializer.LoadDocument(dialog.FileName);
            ReplaceCurrentDocument(documentData.Entities, documentData.SupportLayerGroups);

            string fileName = Path.GetFileName(dialog.FileName);
            return new DocumentFileOperationResult($"Opened {fileName}", $"Opened {fileName}");
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is NotSupportedException || ex is ArgumentException || ex is UnauthorizedAccessException)
        {
            MessageBox.Show(_owner, ex.Message, "Open Graphite Project Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return new DocumentFileOperationResult("Open failed", null);
        }
    }

    /// <summary>
    /// Saves the current document to a Graphite project file selected by the user.
    /// </summary>
    public DocumentFileOperationResult Save()
    {
        _ = TrySaveCurrentDocument(out DocumentFileOperationResult result);
        return result;
    }

    /// <summary>
    /// Confirms replacement when opening a project would remove current document entities.
    /// </summary>
    private bool ConfirmReplaceCurrentDocument()
    {
        if (_document.Entities.Count == 0)
        {
            return true;
        }

        MessageBoxResult result = MessageBox.Show(
            _owner,
            "Opening a project will replace the current document. Continue?",
            "Open Graphite Project",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.Yes;
    }

    /// <summary>
    /// Confirms whether current entities should be saved before creating a new blank document.
    /// </summary>
    private bool ConfirmNewDocument(out DocumentFileOperationResult result)
    {
        if (_document.Entities.Count == 0)
        {
            result = new DocumentFileOperationResult(string.Empty, null);
            return true;
        }

        MessageBoxResult answer = MessageBox.Show(
            _owner,
            "Save the current document before creating a new document?",
            "New Graphite Project",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Cancel)
        {
            result = new DocumentFileOperationResult("New document cancelled", null);
            return false;
        }

        if (answer == MessageBoxResult.No)
        {
            result = new DocumentFileOperationResult(string.Empty, null);
            return true;
        }

        bool didSave = TrySaveCurrentDocument(out result);

        if (didSave && result.DidSaveDocument)
        {
            MessageBox.Show(
                _owner,
                "Your document has been saved.",
                "New Graphite Project",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        return didSave;
    }

    /// <summary>
    /// Prompts for a Graphite project path and saves the current document.
    /// </summary>
    private bool TrySaveCurrentDocument(out DocumentFileOperationResult result)
    {
        SaveFileDialog dialog = new SaveFileDialog
        {
            Title = "Save Graphite Project",
            Filter = GraphiteProjectFileFilter,
            DefaultExt = GraphiteProjectDefaultExtension,
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(_owner) != true)
        {
            result = new DocumentFileOperationResult("Save cancelled", null);
            return false;
        }

        try
        {
            _serializer.Save(_document, dialog.FileName);

            string fileName = Path.GetFileName(dialog.FileName);
            result = new DocumentFileOperationResult($"Saved {fileName}", $"Saved {fileName}", true);
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is NotSupportedException || ex is ArgumentException || ex is UnauthorizedAccessException)
        {
            MessageBox.Show(_owner, ex.Message, "Save Graphite Project Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            result = new DocumentFileOperationResult("Save failed", null);
            return false;
        }
    }

    /// <summary>
    /// Replaces the document contents after resetting transient interaction and selection state.
    /// </summary>
    private void ReplaceCurrentDocument(IEnumerable<CadEntity> loadedEntities, IEnumerable<SupportLayerGroup> supportLayerGroups)
    {
        _cancelTransientToolState();
        _selectionManager.ClearSelection();
        _document.ReplaceDocumentData(loadedEntities, supportLayerGroups);
        _clearCommandHistory();
        _activateSelectionTool();
    }
}
