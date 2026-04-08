using CadApp.Core.Entities;
using System.Collections.ObjectModel;
using CadApp.Core.Spatial;

namespace CadApp.Core.Document;

public class CadDocument
{
    //TODO Spatial grid size is a const value. Make it specified in a config file somewhere?
    public ObservableCollection<CadEntity> Entities { get; } = new();
    public SpatialGrid SpatialGrid { get; }

    public CadDocument()
    {
        SpatialGrid = new SpatialGrid(1.0f);
    }

}