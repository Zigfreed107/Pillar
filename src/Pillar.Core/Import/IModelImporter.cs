using Pillar.Core.Entities;

namespace Pillar.Core.Import;

/// <summary>
/// Imports a file into one or more document entities.
/// </summary>
public interface IModelImporter
{
    CadEntity Import(string filePath);
}
