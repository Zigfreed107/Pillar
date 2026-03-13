using CadApp.Core.Entities;
using HelixToolkit.SharpDX.Core;
using HelixToolkit.Wpf.SharpDX;
using SharpDX;

namespace CadApp.Rendering.EntityRenderers;

public static class LineRenderer
{
    public static LineGeometryModel3D Create(LineEntity line)
    {
        var builder = new LineBuilder();
        builder.AddLine(
            new Vector3(line.Start.X, line.Start.Y, line.Start.Z),
            new Vector3(line.End.X, line.End.Y, line.End.Z));

        return new LineGeometryModel3D
        {
            Geometry = builder.ToLineGeometry3D(),
            Color = Color.Blue,
            Thickness = 2
        };
    }
}
