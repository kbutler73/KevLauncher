using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace KevLauncher;

public enum DropPosition
{
    None,
    Before,
    Inside,
    After
}

public class DropAdorner : Adorner
{
    private readonly DropPosition _position;
    private readonly System.Windows.Media.Brush _penBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 115, 232));
    private readonly double _thickness = 2.0;

    public DropAdorner(UIElement adornedElement, DropPosition position)
        : base(adornedElement)
    {
        _position = position;
        IsHitTestVisible = false;
    }

    protected override void OnRender(System.Windows.Media.DrawingContext drawingContext)
    {
        var adornedElementRect = new Rect(this.AdornedElement.RenderSize);
        System.Windows.Media.Pen renderPen = new System.Windows.Media.Pen(_penBrush, _thickness);
        renderPen.Freeze();

        switch (_position)
        {
            case DropPosition.Before:
                drawingContext.DrawLine(renderPen, new System.Windows.Point(0, 0), new System.Windows.Point(adornedElementRect.Width, 0));
                break;
            case DropPosition.After:
                drawingContext.DrawLine(renderPen, new System.Windows.Point(0, adornedElementRect.Height), new System.Windows.Point(adornedElementRect.Width, adornedElementRect.Height));
                break;
            case DropPosition.Inside:
                // draw a border around to indicate inside
                drawingContext.DrawRectangle(null, renderPen, adornedElementRect);
                break;
        }
    }
}
