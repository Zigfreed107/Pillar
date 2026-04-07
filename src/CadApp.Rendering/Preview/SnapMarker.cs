using HelixToolkit;
using HelixToolkit.Geometry;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using System.Numerics;
using System.Windows;
using System.Windows.Media.Media3D;

/// <summary>
/// Handles rendering of the interactive snap markers.
/// </summary>
namespace CadApp.Rendering.Preview
{
    public class SnapMarkerRenderer
    {
        private readonly MeshGeometryModel3D _snapMarker;

        public SnapMarkerRenderer(GroupModel3D sceneRoot)
        {
            var builder = new MeshBuilder();
            builder.AddSphere(new Vector3(0, 0, 0), 0.1f);

            _snapMarker = new MeshGeometryModel3D
            {
                Geometry = builder.ToMeshGeometry3D(),
                Material = new PhongMaterial
                {
                    DiffuseColor = new Color4(1f, 0f, 0f, 1f), // red
                    AmbientColor = new Color4(1f, 0f, 0f, 1f)

                },
                Name = "SnapMarker",
                CullMode = SharpDX.Direct3D11.CullMode.None, // IMPORTANT
                Visibility = Visibility.Visible
            };

            sceneRoot.Children.Add(_snapMarker);

        }

        public void Show(Vector3 position)
        {
            _snapMarker.Transform = new TranslateTransform3D(position.X, position.Y, position.Z);

            _snapMarker.Visibility = Visibility.Visible;
        }

        public void HideSnappingPoint()
        {
            _snapMarker.Visibility = Visibility.Hidden;
        }
    }
}
