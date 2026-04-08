using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System.Numerics;


namespace CadApp.Rendering.BackgroundGrid
{
    /// <summary>
    /// Handles rendering of background grid
    /// </summary>
    public class BackgroundGridRenderer
    {
        private readonly LineGeometryModel3D _bgGrid;
        private readonly LineGeometryModel3D _origin;

        public BackgroundGridRenderer(GroupModel3D sceneRoot)
        {
            var builder = new LineBuilder();

            int size = 50;
            int step = 1;

            for (int i = -size; i <= size; i += step)
            {
                // vertical lines
                builder.AddLine(
                    new Vector3(i, -size, 0),
                    new Vector3(i, size, 0));

                // horizontal lines
                builder.AddLine(
                    new Vector3(-size, i, 0),
                    new Vector3(size, i, 0));
            }

            _bgGrid = new LineGeometryModel3D
            {
                Geometry = builder.ToLineGeometry3D(),
                Color = System.Windows.Media.Color.FromRgb(150, 150, 150),
                Thickness = 0.5f
            };

            sceneRoot.Children.Add(_bgGrid);

            var originBuilder = new LineBuilder();

            originBuilder.AddLine(new Vector3(0, 0, 0), new Vector3(0.5f, 0, 0));
            originBuilder.AddLine(new Vector3(0, 0, 0), new Vector3(0, 0.5f, 0));
            originBuilder.AddLine(new Vector3(0, 0, 0), new Vector3(0, 0, 0.5f));

            _origin = new LineGeometryModel3D
            {
                Geometry = originBuilder.ToLineGeometry3D(),
                Color = System.Windows.Media.Color.FromRgb(150, 0, 0),
                Thickness = 1.00f
            };

            sceneRoot.Children.Add(_origin);

        }
    }
}
