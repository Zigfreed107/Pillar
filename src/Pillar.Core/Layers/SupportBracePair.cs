// SupportBracePair.cs
// Stores one durable unordered pair of support identities excluded from a Brace modifier.
using System;

namespace Pillar.Core.Layers;

/// <summary>
/// Identifies one unordered support pair using stable endpoint ordering.
/// </summary>
public sealed class SupportBracePair : IEquatable<SupportBracePair>
{
    /// <summary>
    /// Creates one validated unordered support pair.
    /// </summary>
    public SupportBracePair(Guid firstSupportId, Guid secondSupportId)
    {
        if (firstSupportId == Guid.Empty || secondSupportId == Guid.Empty || firstSupportId == secondSupportId)
        {
            throw new ArgumentException("Brace pairs require two distinct non-empty support identities.");
        }

        if (firstSupportId.CompareTo(secondSupportId) <= 0)
        {
            FirstSupportId = firstSupportId;
            SecondSupportId = secondSupportId;
        }
        else
        {
            FirstSupportId = secondSupportId;
            SecondSupportId = firstSupportId;
        }
    }

    public Guid FirstSupportId { get; }

    public Guid SecondSupportId { get; }

    /// <summary>
    /// Creates a defensive copy.
    /// </summary>
    public SupportBracePair Clone()
    {
        return new SupportBracePair(FirstSupportId, SecondSupportId);
    }

    /// <summary>
    /// Compares one pair by its normalized endpoint identities.
    /// </summary>
    public bool Equals(SupportBracePair? other)
    {
        return other != null
            && FirstSupportId == other.FirstSupportId
            && SecondSupportId == other.SecondSupportId;
    }

    /// <summary>
    /// Compares an object with this normalized pair.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is SupportBracePair other && Equals(other);
    }

    /// <summary>
    /// Creates a stable hash from both normalized endpoint identities.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(FirstSupportId, SecondSupportId);
    }
}
