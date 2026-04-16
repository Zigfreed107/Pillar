using System.Collections.Generic;

namespace Pillar.Core.Snapping
{
    public interface ISnapProvider
    {
        void GetSnapPoints(List<SnapPoint> snapPoints);
    }
}