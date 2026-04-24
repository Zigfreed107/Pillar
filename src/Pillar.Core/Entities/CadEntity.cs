// CadEntity.cs
// Defines the shared domain data every CAD entity carries, independent of rendering and UI concerns.
using Pillar.Core.Snapping;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Pillar.Core.Entities;

/// <summary>
/// Provides the shared identity, naming, bounds, and snap-point contract for all CAD entities.
/// </summary>
public abstract class CadEntity : ISelectable, INotifyPropertyChanged
{
    private string _name;

    public Guid Id { get; protected set; }

    /// <summary>
    /// Gets or sets the user-visible entity name and raises change notifications for shell observers.
    /// </summary>
    public string Name
    {
        get { return _name; }
        set
        {
            string normalizedName = string.IsNullOrWhiteSpace(value) ? "Entity" : value;

            if (string.Equals(_name, normalizedName, StringComparison.Ordinal))
            {
                return;
            }

            _name = normalizedName;
            OnPropertyChanged();
        }
    }

    public abstract (Vector3 Min, Vector3 Max) GetBounds();

    /// <summary>
    /// Creates a CAD entity with a stable identifier and user-visible name.
    /// </summary>
    protected CadEntity(string name)
    {
        Id = Guid.NewGuid();
        _name = string.IsNullOrWhiteSpace(name) ? "Entity" : name;
    }

    /// <summary>
    /// Returns entity snap points for tools that support snapping.
    /// </summary>
    public virtual IEnumerable<SnapPoint> GetSnapPoints()
    {
        yield break;
    }

    /// <summary>
    /// Raised when entity state changes and dependent UI or rendering layers need to refresh.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Publishes one property change notification for derived entities.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
