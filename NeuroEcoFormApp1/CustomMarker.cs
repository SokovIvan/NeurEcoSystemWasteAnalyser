using GMap.NET.WindowsForms;
using GMap.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace NeuroEcoFormApp1
{
    public class CustomMarker : GMapMarker
    {
        private Color _color;
        private int _radious;
        public CustomMarker(PointLatLng pos, Color colorC,int radiusC) : base(pos)
        {
            ToolTip = new GMap.NET.WindowsForms.ToolTips.GMapRoundedToolTip(this);//всплывающее окно с инфой к маркеру
            ToolTipText = pos.Lng.ToString(); // текст внутри всплывающего окна
            ToolTipMode = MarkerTooltipMode.OnMouseOver;
            _color= colorC;
            _radious = radiusC;
        }

        public override void OnRender(Graphics g)
        {
            // Рисуем круг с заданным цветом
            int radius = _radious; // Радиус круга
            Color color = Color.FromArgb(255, 255, 0, 0); // Красный цвет
            using (Brush brush = new SolidBrush(_color))
            {
                g.FillEllipse(brush, new RectangleF(LocalPosition.X - radius, LocalPosition.Y - radius, radius * 2, radius * 2));
            }
        }
    }
}
