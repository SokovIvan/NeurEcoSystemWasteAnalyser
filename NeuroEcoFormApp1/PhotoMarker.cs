using GMap.NET.WindowsForms.ToolTips;
using GMap.NET.WindowsForms;
using GMap.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuroEcoFormApp1
{
    public class PhotoMarker : GMapMarker
    {
        private Image[] _images;
        private int _spacing;
        private int _imageWidth;
        private int _imageHeight;

        public PhotoMarker(PointLatLng pos, Image[] images, int spacing, int imageWidth, int imageHeight) : base(pos)
        {
            ToolTip = new GMapRoundedToolTip(this); // Всплывающее окно с информацией к маркеру
            ToolTipText = pos.Lng.ToString(); // Текст внутри всплывающего окна
            ToolTipMode = MarkerTooltipMode.OnMouseOver;
            _images = images;
            _spacing = spacing;
            _imageWidth = imageWidth;
            _imageHeight = imageHeight;
        }

        public override void OnRender(Graphics g)
        {
            // Вычисляем позицию для первой фотографии
            int startX = LocalPosition.X - (_images.Length * _imageWidth + (_images.Length - 1) * _spacing) / 2;
            int startY = LocalPosition.Y - _imageHeight / 2;

            // Отрисовываем фотографии в ряд
            for (int i = 0; i < _images.Length; i++)
            {
                g.DrawImage(_images[i], new Rectangle(startX + i * (_imageWidth + _spacing), startY, _imageWidth, _imageHeight));
            }
        }
    }
}
