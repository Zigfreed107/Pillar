using CadApp.Core.Entities;

namespace CadApp.Core.Import;

/// <summary>
/// Imports a file into one or more document entities.
/// </summary>
public interface IModelImporter
{
    CadEntity Import(string filePath);
}
