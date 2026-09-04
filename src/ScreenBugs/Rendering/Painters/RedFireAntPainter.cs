using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class RedFireAntPainter : IBugPainter
{
    private readonly AntGeometry ant = new(PainterPens.Hex("#a8462a"), SpeciesCatalog.Get(SpeciesId.RedFireAnt).BodyLength);

    public Color BodyColor => ant.Color;

    public void Paint(DrawingContext dc, Bug bug) => ant.Paint(dc, bug);
}
