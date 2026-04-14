// ICadCommand.cs
// Defines the reversible command contract used by CAD tools and the undo/redo history.
namespace CadApp.Commands;

public interface ICadCommand
{
    /// <summary>
    /// Gets the short user-facing name shown in undo and redo status messages.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Applies the command's document change.
    /// </summary>
    void Execute();

    /// <summary>
    /// Reverses the command's document change.
    /// </summary>
    void Undo();
}
