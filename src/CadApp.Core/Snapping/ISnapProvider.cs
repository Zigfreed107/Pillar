using System.Collections.Generic;

namespace CadApp.Core.Snapping
{
    public interface ISnapProvider
    {
        void GetSnapPoints(List<SnapPoint> snapPoints);
    }
}