using System.Windows.Media;

namespace ScreenBugs.Rendering.Painters;

public sealed class BlackGardenAntPainter : IBugPainter
{
    private readonly AntGeometry ant = new(PainterPens.Hex("#1c1c1c"), SpeciesCatalog.Get(SpeciesId.BlackGardenAnt).BodyLength);

    public Color BodyColor => ant.Color;

    public void Paint(DrawingContext dc, Bug bug) => ant.Paint(dc, bug);
}
