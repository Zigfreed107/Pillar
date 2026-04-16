// DocumentFileOperationResult.cs
// Carries user-facing status text from document file commands back to the WPF shell.
namespace Pillar.UI.Services;

/// <summary>
/// Describes the visible shell feedback produced by a document file command.
/// </summary>
public readonly struct DocumentFileOperationResult
{
    /// <summary>
    /// Creates a result with status text and optional tool-panel text.
    /// </summary>
    public DocumentFileOperationResult(string statusText, string? toolPanelText)
        : this(statusText, toolPanelText, false)
    {
    }

    /// <summary>
    /// Creates a result with status text, optional tool-panel text, and save-success metadata.
    /// </summary>
    public DocumentFileOperationResult(string statusText, string? toolPanelText, bool didSaveDocument)
    {
        StatusText = statusText;
        ToolPanelText = toolPanelText;
        DidSaveDocument = didSaveDocument;
    }

    public string StatusText { get; }
    public string? ToolPanelText { get; }
    public bool DidSaveDocument { get; }
}
