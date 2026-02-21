using GMap.NET;
using GMap.NET.WindowsForms;

namespace NeuroEcoFormApp1
{
    internal class GMapMarkerImage : GMapMarker
    {
        private PointLatLng pointLatLng;
        private Bitmap heatMapImage;

        public GMapMarkerImage(PointLatLng pointLatLng, Bitmap heatMapImage) : base(pointLatLng)
        {
            this.pointLatLng = pointLatLng;
            this.heatMapImage = heatMapImage;
        }
    }
}