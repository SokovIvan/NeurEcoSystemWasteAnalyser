using GMap.NET;
using GMap.NET.Projections;
using GMap.NET.WindowsForms;
using AForge.Imaging.Filters;
using System.Net.Sockets;
using System.Text.Json;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
namespace NeuroEcoFormApp1
{
    public partial class Form1 : Form
    {
        private GMapOverlay heatmapOverlay;
        private GMapOverlay photoOverlay;
        private List<GMapOverlay> heatOverlay;
        private List<GMapOverlay> zagrOverlay;
        private List<GMapOverlay> gasOverlay;
        private List<HeatPoint> heatPoints;
        private List<HeatPoint> neuroPoints;
        private List<HeatPoint> gasPoints;
        private bool isNeuroMap;
        private bool isHeatMap;
        private bool isMetanMap;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            isNeuroMap = false;
            heatPoints = new List<HeatPoint>();
            neuroPoints = new List<HeatPoint>();
            gasPoints = new List<HeatPoint>();
            zagrOverlay = new List<GMapOverlay>();
            heatOverlay = new List<GMapOverlay>();
            gasOverlay = new List<GMapOverlay>();
            pictureBox1.Visible = false;
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            InitializeMap();
        }

        public static float[,] LoadAndProcessImage(string imagePath, Size targetSize)
        {
            using (Bitmap img = new Bitmap(imagePath))
            {   
                using (Bitmap resizedImg = new Bitmap(img, targetSize))
                {                    
                    float[,] pixelArray = new float[targetSize.Width, targetSize.Height];
                    for (int x = 0; x < targetSize.Width; x++)
                    {
                        for (int y = 0; y < targetSize.Height; y++)
                        {
                            Color pixelColor = resizedImg.GetPixel(x, y);
                            pixelArray[x, y] = (pixelColor.R + pixelColor.G + pixelColor.B) / 3.0f / 255.0f;
                        }
                    }
                    return pixelArray;
                }
            }
        }

        public static double useModel(string imagePath1, string imagePath2, float num1, float num2, float num3, float num4)
        {
            using (var client = new TcpClient("localhost", 12345))
            using (var stream = client.GetStream())
            {
                var numbers = new float[4] { num1, num2, num3, num4 };
                Size targetSize = new Size(512, 512);
                float[,] img1 = LoadAndProcessImage(imagePath1, targetSize);
                float[,] img2 = LoadAndProcessImage(imagePath2, targetSize);
                float[] img1Flat = img1.Cast<float>().ToArray();
                float[] img2Flat = img2.Cast<float>().ToArray();
                var dataToSend = new
                {
                    Images = new[] { img1Flat, img2Flat },
                    Numbers = numbers
                };

                var jsonData = JsonSerializer.Serialize(dataToSend);
                var jsonBytes = Encoding.UTF8.GetBytes(jsonData);
                stream.Write(jsonBytes, 0, jsonBytes.Length);
                stream.Flush();  
                try
                {
                    var buffer = new byte[4096];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    var jsonObject = JsonSerializer.Deserialize<Dictionary<string, double>>(receivedData);

                    double prediction = jsonObject["prediction"];
                    return prediction;
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"JSON parse error: {ex.Message}");
                    return -1;
                }

            }
        }


        private void CheckHeatMap(string name)
        {

            if (name != null)
            {
                foreach (GMapOverlay mapOverlay in gMapControl.Overlays) Debug.WriteLine(mapOverlay.Id);
                name += gMapControl.Zoom + 3;
                GMapOverlay gMapOverlaySearch = gMapControl.Overlays.Where(r => r.Id == name).FirstOrDefault();
                if(gMapOverlaySearch!=null) gMapControl.Overlays.Where(r => r.Id == name).FirstOrDefault().IsVisibile = true;//видимость слоя с заданным именем
            }
        }
        public void GetHeatMap(List<HeatPoint> heatPoints, string name,List<GMapOverlay> mainL ,bool AllCoord = true)
        {
            MercatorProjection mercatorProjection = new MercatorProjection();
            mainL.Clear();
            for (int myZoom = gMapControl.MinZoom + 4; myZoom <= gMapControl.MaxZoom + 3; myZoom++)
            {
                GMapOverlay gMapOverlay = new GMapOverlay(name + myZoom);
                mainL.Add(gMapOverlay);
                List<PointLatLng> grayBackgroundPoints = new List<PointLatLng>
                {
                    new PointLatLng(90, -180), // Северо-Западный угол
                    new PointLatLng(90, 180),  // Северо-Восточный угол
                    new PointLatLng(-90, 180), // Юго-Восточный угол
                    new PointLatLng(-90, -180) // Юго-Западный угол
                };
                GMapPolygon grayPolygon = new GMapPolygon(grayBackgroundPoints, "GrayBackground");
                grayPolygon.Fill = new SolidBrush(Color.FromArgb(25, 128, 255, 128)); 
                grayPolygon.Stroke = new Pen(Color.Transparent, 0);                 
                gMapOverlay.Polygons.Add(grayPolygon);
                int tileSizePx = (int)mercatorProjection.TileSize.Height;
                Dictionary<GPoint, double> pointIntensities = new Dictionary<GPoint, double>();
                foreach (HeatPoint heatPoint in heatPoints)
                {
                    GPoint pixelCoord = mercatorProjection.FromLatLngToPixel(heatPoint.Latitude, heatPoint.Longitude, myZoom);
                    if (pointIntensities.ContainsKey(pixelCoord))
                    {
                        pointIntensities[pixelCoord] += heatPoint.Intensity;
                    }
                    else
                    {
                        pointIntensities[pixelCoord] = heatPoint.Intensity;
                    }
                }
                foreach (var point in pointIntensities)
                {
                    double intensity = point.Value;
                    int intensityColor = (int)(intensity * 255);

                    for (int coefMash = 5; coefMash <= 50; coefMash += 5)
                    {
                        Color centerColor = Color.FromArgb(255 - coefMash * 5, Math.Clamp(intensityColor - coefMash * 2, 0, 255), coefMash * 2, 0);

                        double radius = Math.Sqrt(point.Value) * coefMash/3; 
                        List<PointLatLng> circlePoints = new List<PointLatLng>();
                        for (int i = 0; i <= 360; i += 2)
                        {
                            double angle = i * Math.PI / 180;
                            double x = point.Key.X + radius * Math.Cos(angle);
                            double y = point.Key.Y + radius * Math.Sin(angle);
                            circlePoints.Add(mercatorProjection.FromPixelToLatLng(new GPoint((long)x, (long)y), myZoom));
                        }

                        SolidBrush solBrush = new SolidBrush(centerColor);
                        GMapPolygon circlePolygon = new GMapPolygon(circlePoints, point.Key.ToString());  
                        circlePolygon.Fill = solBrush;
                        circlePolygon.Stroke = new Pen(centerColor, -1);
                        circlePolygon.IsVisible = true;
                        gMapOverlay.Polygons.Add(circlePolygon);
                    }
                }                
                Bitmap heatMapImage = new Bitmap(gMapControl.Width, gMapControl.Height);
                gMapControl.DrawToBitmap(heatMapImage, new Rectangle(0, 0, gMapControl.Width, gMapControl.Height));
                GaussianBlur filter = new GaussianBlur(20); 
                filter.ApplyInPlace(heatMapImage);
                gMapOverlay.Markers.Clear();
                gMapOverlay.Markers.Add(new GMapMarkerImage(new PointLatLng(0, 0), heatMapImage));
                gMapControl.Overlays.Add(gMapOverlay);
                GC.Collect();
            }
        }

        private void InitializeMap()
        {
            
            GMap.NET.GMaps.Instance.Mode = GMap.NET.AccessMode.ServerAndCache;
            gMapControl.Dock = DockStyle.None;
            gMapControl.MapProvider = GMap.NET.MapProviders.OpenStreetMapProvider.Instance;
            gMapControl.Position = new PointLatLng(43.0, 132.0);
            gMapControl.MinZoom = 10;
            gMapControl.MaxZoom = 16;
            gMapControl.Zoom = 11;

            photoOverlay = new GMapOverlay("photo");
            heatmapOverlay = new GMapOverlay("heatmap");
            gMapControl.Overlays.Add(heatmapOverlay);
            gMapControl.Overlays.Add(photoOverlay);
            gMapControl.OnMapZoomChanged += GMapControl_OnMapZoomChanged;
            photoOverlay.IsVisibile = false;

            AddHeatmapPoint(43.140504, 132.051316, 100);
            AddHeatmapPoint(43.010451, 131.910935, 100);
            AddHeatmapPoint(43.266873, 132.015364, 50);

            Image[] images1 = { Image.FromFile("Руч1.jpg"), Image.FromFile("Руч17.jpg"), Image.FromFile("Руч2.jpg") };

            Image[] images2 = { Image.FromFile("Рус1.jpg"), Image.FromFile("Рус7.jpg"), Image.FromFile("Рус37.jpg") };

            Image[] images3 = { Image.FromFile("Сад1.jpg"), Image.FromFile("Сад27.jpg"), Image.FromFile("Сад15.jpg") };

            AddPhotoPoint(43.140504, 132.051316, images1, 10, 100, 100);
            AddPhotoPoint(43.010451, 131.910935, images2, 10, 100, 100);
            AddPhotoPoint(43.266873, 132.015364, images3, 10, 100, 100);

            intitialiseNeuroPoints();
            inialiseHeatMapPoints();
            initialiseGasPont();
        }

        private void GMapControl_OnMapZoomChanged()
        {

            if (gMapControl.Zoom >=13)

                photoOverlay.IsVisibile = !(isHeatMap || isMetanMap || isNeuroMap);
            else
            {
                photoOverlay.IsVisibile = false;
            }
            gMapControl.Update();
        }

        private void inialiseHeatMapPoints() {
            HeatPoint hp1 = new()
            {
                Latitude = 43.140504,
                Longitude = 132.051316,
                Intensity = 8.4 / 20f
            };
            heatPoints.Add(hp1);

            HeatPoint hp2 = new()
            {
                Latitude = 43.2672005,
                Longitude = 132.0155020,
                Intensity = 8.4 / 20f
            };
            heatPoints.Add(hp2);

            HeatPoint hp3 = new()
            {
                Latitude = 43.2671702,
                Longitude = 132.0155020,
                Intensity = 9 / 20f
            };
            heatPoints.Add(hp3);

            HeatPoint hp4 = new()
            {
                Latitude = 43.2671663,
                Longitude = 132.0154940,
                Intensity = 8 / 20f
            };
            heatPoints.Add(hp4);

            HeatPoint hp5 = new()
            {
                Latitude = 43.266974,
                Longitude = 132.015372,
                Intensity = 7.8 / 20f
            };
            heatPoints.Add(hp5);

            HeatPoint hp6 = new()
            {
                Latitude = 43.266976,
                Longitude = 132.015375,
                Intensity = 8.4 / 20f
            };
            heatPoints.Add(hp6);

            HeatPoint hp7 = new()
            {
                Latitude = 43.266972,
                Longitude = 132.015372,
                Intensity = 8.5 / 20f
            };
            heatPoints.Add(hp7);

            HeatPoint hp8 = new()
            {
                Latitude = 43.266962,
                Longitude = 132.015369,
                Intensity = 8.5 / 20f
            };
            heatPoints.Add(hp8);

            HeatPoint hp9 = new()
            {
                Latitude = 43.266896,
                Longitude = 132.015316,
                Intensity = 8.3 / 20f
            };
            heatPoints.Add(hp9);

            HeatPoint hp10 = new()
            {
                Latitude = 43.266892,
                Longitude = 132.015316,
                Intensity = 8.5 / 20f
            };
            heatPoints.Add(hp10);

            HeatPoint hp11 = new()
            {
                Latitude = 43.266874,
                Longitude = 132.015318,
                Intensity = 6.2 / 20f
            };
            heatPoints.Add(hp11);

            HeatPoint hp12 = new()
            {
                Latitude = 43.266845,
                Longitude = 132.015308,
                Intensity = 6.3 / 20f
            };
            heatPoints.Add(hp12);

            HeatPoint hp13 = new()
            {
                Latitude = 43.266851,
                Longitude = 132.015299,
                Intensity = 6.9 / 20f
            };
            heatPoints.Add(hp13);

            HeatPoint hp14 = new()
            {
                Latitude = 43.266827,
                Longitude = 132.015299,
                Intensity = 6.8 / 20f
            };
            heatPoints.Add(hp14);

            HeatPoint hp15 = new()
            {
                Latitude = 43.266810,
                Longitude = 132.015291,
                Intensity = 6.7 / 20f
            };
            heatPoints.Add(hp15);

            HeatPoint hp16 = new()
            {
                Latitude = 43.266816,
                Longitude = 132.015299,
                Intensity = 6.8 / 20f
            };
            heatPoints.Add(hp16);

            HeatPoint hp17 = new()
            {
                Latitude = 43.266833,
                Longitude = 132.015332,
                Intensity = 6.8 / 20f
            };
            heatPoints.Add(hp17);

            HeatPoint hp18 = new()
            {
                Latitude = 43.266820,
                Longitude = 132.015342,
                Intensity = 6.6 / 20f
            };
            heatPoints.Add(hp18);

            HeatPoint hp19 = new()
            {
                Latitude = 43.266781,
                Longitude = 132.015257,
                Intensity = 6.6 / 20f
            };
            heatPoints.Add(hp19);

            HeatPoint hp20 = new()
            {
                Latitude = 43.266773,
                Longitude = 132.015259,
                Intensity = 6.2 / 20f
            };
            heatPoints.Add(hp20);

            HeatPoint hp21 = new()
            {
                Latitude = 43.266749,
                Longitude = 132.015262,
                Intensity = 6.4 / 20f
            };
            heatPoints.Add(hp21);

            HeatPoint hp22 = new()
            {
                Latitude = 43.266745,
                Longitude = 132.015251,
                Intensity = 6.3 / 20f
            };
            heatPoints.Add(hp22);

            HeatPoint hp23 = new()
            {
                Latitude = 43.266753,
                Longitude = 132.015251,
                Intensity = 6.5 / 20f
            };
            heatPoints.Add(hp23);

            HeatPoint hp24 = new()
            {
                Latitude = 43.266724,
                Longitude = 132.015238,
                Intensity = 6.6 / 20f
            };
            heatPoints.Add(hp24);

            HeatPoint hp25 = new()
            {
                Latitude = 43.266710,
                Longitude = 132.015232,
                Intensity = 6.5 / 20f
            };
            heatPoints.Add(hp25);

            HeatPoint hp26 = new()
            {
                Latitude = 43.266697,
                Longitude = 132.015224,
                Intensity = 6.7 / 20f
            };
            heatPoints.Add(hp26);

            HeatPoint hp27 = new()
            {
                Latitude = 43.266675,
                Longitude = 132.015224,
                Intensity = 6.8 / 20f
            };
            heatPoints.Add(hp27);

            HeatPoint hp28 = new()
            {
                Latitude = 43.266650,
                Longitude = 132.015254,
                Intensity = 6.8 / 20f
            };
            heatPoints.Add(hp28);

            HeatPoint hp29 = new()
            {
                Latitude = 43.266628,
                Longitude = 132.015251,
                Intensity = 6.8 / 20f
            };
            heatPoints.Add(hp29);

            HeatPoint hp30 = new()
            {
                Latitude = 43.266560,
                Longitude = 132.015208,
                Intensity = 11.6 / 20f
            };
            heatPoints.Add(hp30);

            HeatPoint hp31 = new()
            {
                Latitude = 43.266531,
                Longitude = 132.015240,
                Intensity = 11.8 / 20f
            };
            heatPoints.Add(hp31);

            HeatPoint hp32 = new()
            {
                Latitude = 43.266480,
                Longitude = 132.015230,
                Intensity = 11.5 / 20f
            };
            heatPoints.Add(hp32);

            HeatPoint hp33 = new()
            {
                Latitude = 43.266462,
                Longitude = 132.015224,
                Intensity = 11.6 / 20f
            };
            heatPoints.Add(hp33);

            HeatPoint hp34 = new()
            {
                Latitude = 43.266505,
                Longitude = 132.015187,
                Intensity = 9.3 / 20f
            };
            heatPoints.Add(hp34);

            HeatPoint hp35 = new()
            {
                Latitude = 43.266384,
                Longitude = 132.015208,
                Intensity = 7.8 / 20f
            };
            heatPoints.Add(hp35);

            HeatPoint hp36 = new()
            {
                Latitude = 43.266347,
                Longitude = 132.015195,
                Intensity = 8.1 / 20f
            };
            heatPoints.Add(hp36);

            HeatPoint hp37 = new()
            {
                Latitude = 43.266318,
                Longitude = 132.015173,
                Intensity = 7.2 / 20f
            };
            heatPoints.Add(hp37);

            HeatPoint hp38 = new()
            {
                Latitude = 43.266316,
                Longitude = 132.015181,
                Intensity = 7.2 / 20f
            };
            heatPoints.Add(hp38);

            HeatPoint hp39 = new()
            {
                Latitude = 43.266304,
                Longitude = 132.015173,
                Intensity = 6 / 20f
            };
            heatPoints.Add(hp39);

            HeatPoint hp40 = new()
            {
                Latitude = 43.266267,
                Longitude = 132.015168,
                Intensity = 6.5 / 20f
            };
            heatPoints.Add(hp40);

            HeatPoint hp41 = new()
            {
                Latitude = 43.266249,
                Longitude = 132.015176,
                Intensity = 6.5 / 20f
            };
            heatPoints.Add(hp41);

            HeatPoint hp42 = new()
            {
                Latitude = 43.266357,
                Longitude = 132.015104,
                Intensity = 5.4 / 20f
            };
            heatPoints.Add(hp42);

            HeatPoint hp43 = new()
            {
                Latitude = 43.266394,
                Longitude = 132.015074,
                Intensity = 5.6 / 20f
            };
            heatPoints.Add(hp43);

            HeatPoint hp44 = new()
            {
                Latitude = 43.266511,
                Longitude = 132.015098,
                Intensity = 4.3 / 20f
            };
            heatPoints.Add(hp44);

            HeatPoint hp45 = new()
            {
                Latitude = 43.266697,
                Longitude = 132.015181,
                Intensity = 3.3 / 20f
            };
            heatPoints.Add(hp45);

            HeatPoint hp46 = new()
            {
                Latitude = 43.266769,
                Longitude = 132.015192,
                Intensity = 2.6/20f
            };
            heatPoints.Add(hp46);
            HeatPoint hp47 = new()
            {
                Latitude = 43.140150,
                Longitude = 132.050785,
                Intensity = 14.4f / 20
            };
            heatPoints.Add(hp47);

            HeatPoint hp48 = new()
            {
                Latitude = 43.140160,
                Longitude = 132.050753,
                Intensity = 11.6f / 20
            };
            heatPoints.Add(hp48);

            HeatPoint hp49 = new()
            {
                Latitude = 43.140258,
                Longitude = 132.050817,
                Intensity = 9.9f / 20
            };
            heatPoints.Add(hp49);

            HeatPoint hp50 = new()
            {
                Latitude = 43.140299,
                Longitude = 132.050793,
                Intensity = 9.1f / 20
            };
            heatPoints.Add(hp50);

            HeatPoint hp51 = new()
            {
                Latitude = 43.140317,
                Longitude = 132.050774,
                Intensity = 9.1f / 20
            };
            heatPoints.Add(hp51);

            HeatPoint hp52 = new()
            {
                Latitude = 43.140336,
                Longitude = 132.050817,
                Intensity = 7.5f / 20
            };
            heatPoints.Add(hp52);

            HeatPoint hp53 = new()
            {
                Latitude = 43.140334,
                Longitude = 132.050831,
                Intensity = 7.3f / 20
            };
            heatPoints.Add(hp53);

            HeatPoint hp54 = new()
            {
                Latitude = 43.140360,
                Longitude = 132.050898,
                Intensity = 7.3f / 20
            };
            heatPoints.Add(hp54);

            HeatPoint hp55 = new()
            {
                Latitude = 43.140299,
                Longitude = 132.050957,
                Intensity = 6.4f / 20
            };
            heatPoints.Add(hp55);

            HeatPoint hp56 = new()
            {
                Latitude = 43.140364,
                Longitude = 132.050989,
                Intensity = 5.8f / 20
            };
            heatPoints.Add(hp56);

            HeatPoint hp57 = new()
            {
                Latitude = 43.140379,
                Longitude = 132.051005,
                Intensity = 5.3f / 20
            };
            heatPoints.Add(hp57);

            HeatPoint hp58 = new()
            {
                Latitude = 43.140405,
                Longitude = 132.051069,
                Intensity = 5.1f / 20
            };
            heatPoints.Add(hp58);

            HeatPoint hp59 = new()
            {
                Latitude = 43.140401,
                Longitude = 132.051069,
                Intensity = 4.7f / 20
            };
            heatPoints.Add(hp59);

            HeatPoint hp60 = new()
            {
                Latitude = 43.140399,
                Longitude = 132.051096,
                Intensity = 5f / 20
            };
            heatPoints.Add(hp60);

            HeatPoint hp61 = new()
            {
                Latitude = 43.140415,
                Longitude = 132.051112,
                Intensity = 4.5f / 20
            };
            heatPoints.Add(hp61);

            HeatPoint hp62 = new()
            {
                Latitude = 43.140417,
                Longitude = 132.051123,
                Intensity = 4.5f / 20
            };
            heatPoints.Add(hp62);

            HeatPoint hp63 = new()
            {
                Latitude = 43.140419,
                Longitude = 132.051153,
                Intensity = 4.4f / 20
            };
            heatPoints.Add(hp63);

            HeatPoint hp64 = new()
            {
                Latitude = 43.140426,
                Longitude = 132.051166,
                Intensity = 4.3f / 20
            };
            heatPoints.Add(hp64);

            HeatPoint hp65 = new()
            {
                Latitude = 43.140448,
                Longitude = 132.051166,
                Intensity = 5f / 20
            };
            heatPoints.Add(hp65);

            HeatPoint hp66 = new()
            {
                Latitude = 43.140479,
                Longitude = 132.051185,
                Intensity = 4.5f / 20
            };
            heatPoints.Add(hp66);

            HeatPoint hp67 = new()
            {
                Latitude = 43.140473,
                Longitude = 132.051252,
                Intensity = 3.8f / 20
            };
            heatPoints.Add(hp67);

            HeatPoint hp68 = new()
            {
                Latitude = 43.140485,
                Longitude = 132.051222,
                Intensity = 4.1f / 20
            };
            heatPoints.Add(hp68);

            HeatPoint hp69 = new()
            {
                Latitude = 43.140520,
                Longitude = 132.051287,
                Intensity = 4.3f / 20
            };
            heatPoints.Add(hp69);

            HeatPoint hp70 = new()
            {
                Latitude = 43.140540,
                Longitude = 132.051319,
                Intensity = 3.6f / 20
            };
            heatPoints.Add(hp70);

            HeatPoint hp71 = new()
            {
                Latitude = 43.140565,
                Longitude = 132.051378,
                Intensity = 3.6f / 20
            };
            heatPoints.Add(hp71);

            HeatPoint hp72 = new()
            {
                Latitude = 43.140612,
                Longitude = 132.051466,
                Intensity = 3.5f / 20
            };
            heatPoints.Add(hp72);

            HeatPoint hp73 = new()
            {
                Latitude = 43.140638,
                Longitude = 132.051493,
                Intensity = 3.5f / 20
            };
            heatPoints.Add(hp73);

            HeatPoint hp74 = new()
            {
                Latitude = 43.140634,
                Longitude = 132.051544,
                Intensity = 3.4f / 20
            };
            heatPoints.Add(hp74);

            HeatPoint hp75 = new()
            {
                Latitude = 43.140642,
                Longitude = 132.051617,
                Intensity = 3.8f / 20
            };
            heatPoints.Add(hp75);

            HeatPoint hp76 = new()
            {
                Latitude = 43.140579,
                Longitude = 132.051520,
                Intensity = 3.3f / 20
            };
            heatPoints.Add(hp76);

            HeatPoint hp77 = new()
            {
                Latitude = 43.140587,
                Longitude = 132.051638,
                Intensity = 3.4f / 20
            };
            heatPoints.Add(hp77);

            HeatPoint hp78 = new()
            {
                Latitude = 43.140667,
                Longitude = 132.051590,
                Intensity = 3.5f / 20
            };
            heatPoints.Add(hp78);

            HeatPoint hp79 = new()
            {
                Latitude = 43.140687,
                Longitude = 132.051614,
                Intensity = 3.4f / 20
            };
            heatPoints.Add(hp79);

            HeatPoint hp80 = new()
            {
                Latitude = 43.140663,
                Longitude = 132.051550,
                Intensity = 5f / 20
            };
            heatPoints.Add(hp80);

            HeatPoint hp81 = new()
            {
                Latitude = 43.140673,
                Longitude = 132.051574,
                Intensity = 5.1f / 20
            };
            heatPoints.Add(hp81);

            HeatPoint hp82 = new()
            {
                Latitude = 43.140532,
                Longitude = 132.051606,
                Intensity = 4.3f / 20
            };
            heatPoints.Add(hp82);

            HeatPoint hp83 = new()
            {
                Latitude = 43.140563,
                Longitude = 132.051751,
                Intensity = 4.5f / 20
            };
            heatPoints.Add(hp83);

            HeatPoint hp84 = new()
            {
                Latitude = 43.140462,
                Longitude = 132.051426,
                Intensity = 4.5f / 20
            };
            heatPoints.Add(hp84);

            HeatPoint hp85 = new()
            {
                Latitude = 43.140663,
                Longitude = 132.051802,
                Intensity = 3.9f / 20
            };
            heatPoints.Add(hp85);

            HeatPoint hp86 = new()
            {
                Latitude = 43.140426,
                Longitude = 132.051311,
                Intensity = 3.6f / 20
            };
            heatPoints.Add(hp86);

            // Добавление остальных точек
            heatPoints.Add(new HeatPoint { Latitude = 43.009747, Longitude = 131.910825, Intensity = 14.2f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009798, Longitude = 131.910809, Intensity = 13.9f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009849, Longitude = 131.910825, Intensity = 11.7f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009802, Longitude = 131.910825, Intensity = 6.8f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009813, Longitude = 131.910825, Intensity = 8.4f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009915, Longitude = 131.910728, Intensity = 8.3f / 500 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010080, Longitude = 131.910734, Intensity = 8.2f / 200 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010053, Longitude = 131.910750, Intensity = 8.3f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010112, Longitude = 131.910744, Intensity = 8.4f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010241, Longitude = 131.910766, Intensity = 8.1f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010272, Longitude = 131.910782, Intensity = 8.2f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010359, Longitude = 131.910776, Intensity = 7.1f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010402, Longitude = 131.910760, Intensity = 7.6f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010429, Longitude = 131.910766, Intensity = 7f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010406, Longitude = 131.910841, Intensity = 7.4f / 450 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010414, Longitude = 131.910771, Intensity = 7.3f / 200 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010488, Longitude = 131.910766, Intensity = 7.3f / 450 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010492, Longitude = 131.910760, Intensity = 7f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010465, Longitude = 131.910814, Intensity = 6.1f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010500, Longitude = 131.910862, Intensity = 6.4f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010516, Longitude = 131.910734, Intensity = 6.1f / 400 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010237, Longitude = 131.910948, Intensity = 6.5f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010280, Longitude = 131.910921, Intensity = 6.5f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010221, Longitude = 131.910927, Intensity = 6.8f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010174, Longitude = 131.910916, Intensity = 6.5f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010041, Longitude = 131.910857, Intensity = 7.6f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009966, Longitude = 131.910830, Intensity = 6.8f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010088, Longitude = 131.910948, Intensity = 6.1f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009837, Longitude = 131.910809, Intensity = 7f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009708, Longitude = 131.910776, Intensity = 6.4f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009731, Longitude = 131.910798, Intensity = 6.8f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009692, Longitude = 131.910782, Intensity = 7.5f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009664, Longitude = 131.910803, Intensity = 7.4f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009629, Longitude = 131.910771, Intensity = 7.4f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009519, Longitude = 131.910787, Intensity = 6.9f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009472, Longitude = 131.910776, Intensity = 6.3f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009504, Longitude = 131.910750, Intensity = 6.4f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009460, Longitude = 131.910766, Intensity = 6.1f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009445, Longitude = 131.910787, Intensity = 6.2f / 550 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009523, Longitude = 131.910664, Intensity = 6f / 400 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009559, Longitude = 131.910658, Intensity = 6f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009586, Longitude = 131.910696, Intensity = 5.6f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009594, Longitude = 131.910675, Intensity = 5.6f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009935, Longitude = 131.910927, Intensity = 3.6f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009939, Longitude = 131.910937, Intensity = 3.8f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.009955, Longitude = 131.910830, Intensity = 4.2f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010167, Longitude = 131.910943, Intensity = 4f / 25 });
            heatPoints.Add(new HeatPoint { Latitude = 43.010418, Longitude = 131.910986, Intensity = 3.4f / 25 });
        }
        private void intitialiseNeuroPoints()
        {            
            HeatPoint hp1 = new()
            {
                Latitude = 43.2672005,
                Longitude = 132.0155020,
                Intensity = 1f
            };
            neuroPoints.Add(hp1);
            HeatPoint hp2 = new()
            {
                Latitude = 43.2671663,
                Longitude = 132.0154940,
                Intensity = 1f
            };
            neuroPoints.Add(hp2);
            HeatPoint hp3 = new()
            {
                Latitude = 43.266974,
                Longitude = 132.015372,
                Intensity = 0.99993074f
            };
            neuroPoints.Add(hp3);
            HeatPoint hp4 = new()
            {
                Latitude = 43.266976,
                Longitude = 132.015375,
                Intensity = 1f
            };
            neuroPoints.Add(hp4);
            HeatPoint hp5 = new()
            {
                Latitude = 43.266976,
                Longitude = 132.015375,
                Intensity = 1f
            };
            neuroPoints.Add(hp5);
            HeatPoint hp6 = new()
            {
                Latitude = 43.266972,
                Longitude = 132.015372,
                Intensity = 1f
            };
            neuroPoints.Add(hp6);
            HeatPoint hp7 = new()
            {
                Latitude = 43.266962,
                Longitude = 132.015369,
                Intensity = 1f
            };
            neuroPoints.Add(hp7);
            HeatPoint hp8 = new()
            {
                Latitude = 43.266896,
                Longitude = 132.015316,
                Intensity = 1f
            };
            neuroPoints.Add(hp8);
            HeatPoint hp9 = new()
            {
                Latitude = 43.266874,
                Longitude = 132.015318,
                Intensity = 1f
            };
            neuroPoints.Add(hp9);
            HeatPoint hp10 = new()
            {
                Latitude = 43.266845,
                Longitude = 132.015308,
                Intensity = 1f
            };
            neuroPoints.Add(hp10);
            HeatPoint hp11 = new()
            {
                Latitude = 43.266851,
                Longitude = 132.015299,
                Intensity = 1f
            };
            neuroPoints.Add(hp11);
            HeatPoint hp12 = new()
            {
                Latitude = 43.266827,
                Longitude = 132.015299,
                Intensity = 1f
            };
            neuroPoints.Add(hp12);
            HeatPoint hp13 = new()
            {
                Latitude = 43.266810,
                Longitude = 132.015291,
                Intensity = 1f
            };
            neuroPoints.Add(hp13);
            HeatPoint hp14 = new()
            {
                Latitude = 43.266816,
                Longitude = 132.015299,
                Intensity = 1f
            };
            neuroPoints.Add(hp14);
            HeatPoint hp15 = new()
            {
                Latitude = 43.266833,
                Longitude = 132.015332,
                Intensity = 1f
            };
            neuroPoints.Add(hp15);
            HeatPoint hp16 = new()
            {
                Latitude = 43.266820,
                Longitude = 132.015342,
                Intensity = 1f
            };
            neuroPoints.Add(hp16);
            HeatPoint hp17 = new()
            {
                Latitude = 43.266781,
                Longitude = 132.015257,
                Intensity = 1f
            };
            neuroPoints.Add(hp17);
            HeatPoint hp18 = new()
            {
                Latitude = 43.266749,
                Longitude = 132.015262,
                Intensity = 1f
            };
            neuroPoints.Add(hp18);
            HeatPoint hp19 = new()
            {
                Latitude = 43.266745,
                Longitude = 132.015251,
                Intensity = 1f
            };
            neuroPoints.Add(hp19);
            HeatPoint hp20 = new()
            {
                Latitude = 43.266753,
                Longitude = 132.015251,
                Intensity = 1f
            };
            neuroPoints.Add(hp20);
            HeatPoint hp21 = new()
            {
                Latitude = 43.266724,
                Longitude = 132.015238,
                Intensity = 1f
            };
            neuroPoints.Add(hp21);
            HeatPoint hp22 = new()
            {
                Latitude = 43.266710,
                Longitude = 132.015232,
                Intensity = 1f
            };
            neuroPoints.Add(hp22);
            HeatPoint hp23 = new()
            {
                Latitude = 43.266697,
                Longitude = 132.015224,
                Intensity = 0.9999999f
            };
            neuroPoints.Add(hp23);
            HeatPoint hp24 = new()
            {
                Latitude = 43.266675,
                Longitude = 132.015224,
                Intensity = 1f
            };
            neuroPoints.Add(hp24);
            HeatPoint hp25 = new()
            {
                Latitude = 43.266650,
                Longitude = 132.015254,
                Intensity = 1f
            };
            neuroPoints.Add(hp25);
            HeatPoint hp26 = new()
            {
                Latitude = 43.266628,
                Longitude = 132.015251,
                Intensity = 7.863597e-31f
            };
            neuroPoints.Add(hp26);
            HeatPoint hp27 = new()
            {
                Latitude = 43.266560,
                Longitude = 132.015208,
                Intensity = 0.00140683f
            };
            neuroPoints.Add(hp27);
            HeatPoint hp28 = new()
            {
                Latitude = 43.266531,
                Longitude = 132.015240,
                Intensity = 1f
            };
            neuroPoints.Add(hp28);
            HeatPoint hp29 = new()
            {
                Latitude = 43.266480,
                Longitude = 132.015230,
                Intensity = 1f
            };
            neuroPoints.Add(hp29);
            HeatPoint hp30 = new()
            {
                Latitude = 43.266462,
                Longitude = 132.015224,
                Intensity = 1f
            };
            neuroPoints.Add(hp30);
            HeatPoint hp31 = new()
            {
                Latitude = 43.266505,
                Longitude = 132.015187,
                Intensity = 1f
            };
            neuroPoints.Add(hp31);
            HeatPoint hp32 = new()
            {
                Latitude = 43.266384,
                Longitude = 132.015208,
                Intensity = 5.6973755e-30f
            };
            neuroPoints.Add(hp32);
            HeatPoint hp33 = new()
            {
                Latitude = 43.266347,
                Longitude = 132.015195,
                Intensity = 4.364023e-27f
            };
            neuroPoints.Add(hp33);
            HeatPoint hp34 = new()
            {
                Latitude = 43.266318,
                Longitude = 132.015173,
                Intensity = 5.327224e-27f
            };
            neuroPoints.Add(hp34);
            HeatPoint hp35 = new()
            {
                Latitude = 43.266316,
                Longitude = 132.015181,
                Intensity = 7.2e-27f // Intensity из Image 36: Sensor Data
            };
            neuroPoints.Add(hp35);

            HeatPoint hp36 = new()
            {
                Latitude = 43.266304,
                Longitude = 132.015173,
                Intensity = 6.0e-27f // Intensity из Image 37: Sensor Data
            };
            neuroPoints.Add(hp36);

            HeatPoint hp37 = new()
            {
                Latitude = 43.266267,
                Longitude = 132.015168,
                Intensity = 6.5e-27f // Intensity из Image 38: Sensor Data
            };
            neuroPoints.Add(hp37);

            HeatPoint hp38 = new()
            {
                Latitude = 43.266249,
                Longitude = 132.015176,
                Intensity = 6.5e-27f // Intensity из Image 39: Sensor Data
            };
            neuroPoints.Add(hp38);

            HeatPoint hp39 = new()
            {
                Latitude = 43.266357,
                Longitude = 132.015104,
                Intensity = 5.4e-27f // Intensity из Image 40: Sensor Data
            };
            neuroPoints.Add(hp39);

            HeatPoint hp40 = new()
            {
                Latitude = 43.266394,
                Longitude = 132.015074,
                Intensity = 5.6e-27f // Intensity из Image 41: Sensor Data
            };
            neuroPoints.Add(hp40);

            HeatPoint hp41 = new()
            {
                Latitude = 43.266511,
                Longitude = 132.015098,
                Intensity = 4.3e-27f // Intensity из Image 42: Sensor Data
            };
            neuroPoints.Add(hp41);

            HeatPoint hp42 = new()
            {
                Latitude = 43.266697,
                Longitude = 132.015181,
                Intensity = 3.3e-27f // Intensity из Image 43: Sensor Data
            };
            neuroPoints.Add(hp42);

            HeatPoint hp43 = new()
            {
                Latitude = 43.266769,
                Longitude = 132.015192,
                Intensity = 2.6e-27f // Intensity из Image 44: Sensor Data
            };
            neuroPoints.Add(hp43);
            HeatPoint hp44 = new()
            {
                Latitude = 43.140540,
                Longitude = 132.051319,
                Intensity = 1f // Intensity из Image 45: Sensor Data
            };
            neuroPoints.Add(hp44);

            HeatPoint hp45 = new()
            {
                Latitude = 43.140565,
                Longitude = 132.051378,
                Intensity = 1f // Intensity из Image 46: Sensor Data
            };
            neuroPoints.Add(hp45);

            HeatPoint hp46 = new()
            {
                Latitude = 43.140612,
                Longitude = 132.051466,
                Intensity = 1f // Intensity из Image 47: Sensor Data
            };
            neuroPoints.Add(hp46);

            HeatPoint hp47 = new()
            {
                Latitude = 43.140638,
                Longitude = 132.051493,
                Intensity = 1f // Intensity из Image 48: Sensor Data
            };
            neuroPoints.Add(hp47);

            HeatPoint hp48 = new()
            {
                Latitude = 43.140634,
                Longitude = 132.051544,
                Intensity = 1f // Intensity из Image 49: Sensor Data
            };
            neuroPoints.Add(hp48);

            HeatPoint hp49 = new()
            {
                Latitude = 43.140642,
                Longitude = 132.051617,
                Intensity = 7.3e-27f // Intensity из Image 50: Sensor Data
            };
            neuroPoints.Add(hp49);

            HeatPoint hp50 = new()
            {
                Latitude = 43.140579,
                Longitude = 132.051520,
                Intensity = 7.3e-27f // Intensity из Image 51: Sensor Data
            };
            neuroPoints.Add(hp50);

            HeatPoint hp51 = new()
            {
                Latitude = 43.140587,
                Longitude = 132.051638,
                Intensity = 6.4e-27f // Intensity из Image 52: Sensor Data
            };
            neuroPoints.Add(hp51);

            HeatPoint hp52 = new()
            {
                Latitude = 43.140667,
                Longitude = 132.051590,
                Intensity = 5.8e-27f // Intensity из Image 53: Sensor Data
            };
            neuroPoints.Add(hp52);
            HeatPoint hp53 = new()
            {
                Latitude = 43.140540,
                Longitude = 132.051319,
                Intensity = 1f // Intensity из Image 54: Sensor Data
            };
            neuroPoints.Add(hp53);

            HeatPoint hp54 = new()
            {
                Latitude = 43.140565,
                Longitude = 132.051378,
                Intensity = 5.1e-27f // Intensity из Image 55: Sensor Data
            };
            neuroPoints.Add(hp54);

            HeatPoint hp55 = new()
            {
                Latitude = 43.140612,
                Longitude = 132.051466,
                Intensity = 4.7e-27f // Intensity из Image 56: Sensor Data
            };
            neuroPoints.Add(hp55);

            HeatPoint hp56 = new()
            {
                Latitude = 43.140638,
                Longitude = 132.051493,
                Intensity = 1f // Intensity из Image 57: Sensor Data
            };
            neuroPoints.Add(hp56);

            HeatPoint hp57 = new()
            {
                Latitude = 43.140634,
                Longitude = 132.051544,
                Intensity = 1f // Intensity из Image 58: Sensor Data
            };
            neuroPoints.Add(hp57);

            HeatPoint hp58 = new()
            {
                Latitude = 43.140642,
                Longitude = 132.051617,
                Intensity = 1f // Intensity из Image 59: Sensor Data
            };
            neuroPoints.Add(hp58);

            HeatPoint hp59 = new()
            {
                Latitude = 43.140579,
                Longitude = 132.051520,
                Intensity = 1f // Intensity из Image 60: Sensor Data
            };
            neuroPoints.Add(hp59);

            HeatPoint hp60 = new()
            {
                Latitude = 43.140587,
                Longitude = 132.051638,
                Intensity = 1f // Intensity из Image 61: Sensor Data
            };
            neuroPoints.Add(hp60);

            HeatPoint hp61 = new()
            {
                Latitude = 43.140667,
                Longitude = 132.051590,
                Intensity = 1f // Intensity из Image 62: Sensor Data
            };
            neuroPoints.Add(hp61);

            HeatPoint hp62 = new()
            {
                Latitude = 43.140540,
                Longitude = 132.051319,
                Intensity = 1f // Intensity из Image 63: Sensor Data
            };
            neuroPoints.Add(hp62);

            HeatPoint hp63 = new()
            {
                Latitude = 43.140565,
                Longitude = 132.051378,
                Intensity = 1f // Intensity из Image 64: Sensor Data
            };
            neuroPoints.Add(hp63);

            HeatPoint hp64 = new()
            {
                Latitude = 43.140612,
                Longitude = 132.051466,
                Intensity = 1f // Intensity из Image 65: Sensor Data
            };
            neuroPoints.Add(hp64);

            HeatPoint hp65 = new()
            {
                Latitude = 43.140638,
                Longitude = 132.051493,
                Intensity = 1f // Intensity из Image 66: Sensor Data
            };
            neuroPoints.Add(hp65);

            HeatPoint hp66 = new()
            {
                Latitude = 43.140634,
                Longitude = 132.051544,
                Intensity = 1f // Intensity из Image 67: Sensor Data
            };
            neuroPoints.Add(hp66);

            HeatPoint hp67 = new()
            {
                Latitude = 43.140642,
                Longitude = 132.051617,
                Intensity = 0f // Intensity из Image 68: Sensor Data
            };
            neuroPoints.Add(hp67);

            HeatPoint hp68 = new()
            {
                Latitude = 43.140579,
                Longitude = 132.051520,
                Intensity = 1f // Intensity из Image 69: Sensor Data
            };
            neuroPoints.Add(hp68);

            HeatPoint hp69 = new()
            {
                Latitude = 43.140587,
                Longitude = 132.051638,
                Intensity = 0f // Intensity из Image 70: Sensor Data
            };
            neuroPoints.Add(hp69);

            HeatPoint hp70 = new()
            {
                Latitude = 43.140667,
                Longitude = 132.051590,
                Intensity = 0f // Intensity из Image 71: Sensor Data
            };
            neuroPoints.Add(hp70);

            HeatPoint hp71 = new()
            {
                Latitude = 43.140540,
                Longitude = 132.051319,
                Intensity = 0.99f // Intensity из Image 72: Sensor Data
            };
            neuroPoints.Add(hp71);

            HeatPoint hp72 = new()
            {
                Latitude = 43.140565,
                Longitude = 132.051378,
                Intensity = 1f // Intensity из Image 73: Sensor Data
            };
            neuroPoints.Add(hp72);

            HeatPoint hp73 = new()
            {
                Latitude = 43.140612,
                Longitude = 132.051466,
                Intensity = 1f // Intensity из Image 74: Sensor Data
            };
            neuroPoints.Add(hp73);

            HeatPoint hp74 = new()
            {
                Latitude = 43.140638,
                Longitude = 132.051493,
                Intensity = 1f // Intensity из Image 75: Sensor Data
            };
            neuroPoints.Add(hp74);

            HeatPoint hp75 = new()
            {
                Latitude = 43.140634,
                Longitude = 132.051544,
                Intensity = 1f // Intensity из Image 76: Sensor Data
            };
            neuroPoints.Add(hp75);

            HeatPoint hp76 = new()
            {
                Latitude = 43.140642,
                Longitude = 132.051617,
                Intensity = 5.0e-27f // Intensity из Image 77: Sensor Data
            };
            neuroPoints.Add(hp76);

            HeatPoint hp77 = new()
            {
                Latitude = 43.140579,
                Longitude = 132.051520,
                Intensity = 5.1e-27f // Intensity из Image 78: Sensor Data
            };
            neuroPoints.Add(hp77);

            HeatPoint hp78 = new()
            {
                Latitude = 43.140587,
                Longitude = 132.051638,
                Intensity = 4.3e-27f // Intensity из Image 79: Sensor Data
            };
            neuroPoints.Add(hp78);

            HeatPoint hp79 = new()
            {
                Latitude = 43.140667,
                Longitude = 132.051590,
                Intensity = 4.5e-27f // Intensity из Image 80: Sensor Data
            };
            neuroPoints.Add(hp79);

            HeatPoint hp80 = new()
            {
                Latitude = 43.140687,
                Longitude = 132.051614,
                Intensity = 4.5e-27f // Intensity из Image 81: Sensor Data
            };
            neuroPoints.Add(hp80);

            HeatPoint hp81 = new()
            {
                Latitude = 43.140663,
                Longitude = 132.051550,
                Intensity = 3.9e-27f // Intensity из Image 82: Sensor Data
            };
            neuroPoints.Add(hp81);

            HeatPoint hp82 = new()
            {
                Latitude = 43.140673,
                Longitude = 132.051574,
                Intensity = 0f // Intensity из Image 83: Sensor Data
            };
            neuroPoints.Add(hp82);

            HeatPoint hp83 = new()
            {
                Latitude = 43.140532,
                Longitude = 132.051606,
                Intensity = 0f // Intensity из Image 84: Sensor Data
            };
            neuroPoints.Add(hp83);

            HeatPoint hp84 = new()
            {
                Latitude = 43.140563,
                Longitude = 132.051751,
                Intensity = 5.0e-27f // Intensity из Image 85: Sensor Data
            };
            neuroPoints.Add(hp84);

            HeatPoint hp85 = new()
            {
                Latitude = 43.140462,
                Longitude = 132.051426,
                Intensity = 1f // Intensity из Image 86: Sensor Data
            };
            neuroPoints.Add(hp85);

            HeatPoint hp86 = new()
            {
                Latitude = 43.140663,
                Longitude = 132.051802,
                Intensity = 3.5689704e-24f // Intensity из Image 87: Sensor Data
            };
            neuroPoints.Add(hp86);

            HeatPoint hp87 = new()
            {
                Latitude = 43.140426,
                Longitude = 132.051311,
                Intensity = 0f // Intensity из Image 88: Sensor Data
            };
            neuroPoints.Add(hp87);

            HeatPoint hp88 = new()
            {
                Latitude = 43.009798,
                Longitude = 131.910809,
                Intensity = 1f // Prediction из Image 84
            };
            neuroPoints.Add(hp88);

            HeatPoint hp89 = new()
            {
                Latitude = 43.009849,
                Longitude = 131.910825,
                Intensity = 1f // Prediction из Image 85
            };
            neuroPoints.Add(hp89);

            HeatPoint hp90 = new()
            {
                Latitude = 43.009802,
                Longitude = 131.910825,
                Intensity = 1f // Prediction из Image 86
            };
            neuroPoints.Add(hp90);

            HeatPoint hp91 = new()
            {
                Latitude = 43.009813,
                Longitude = 131.910825,
                Intensity = 1f // Prediction из Image 87
            };
            neuroPoints.Add(hp91);

            HeatPoint hp92 = new()
            {
                Latitude = 43.009915,
                Longitude = 131.910728,
                Intensity = 1f // Prediction из Image 88
            };
            neuroPoints.Add(hp92);

            HeatPoint hp93 = new()
            {
                Latitude = 43.010080,
                Longitude = 131.910734,
                Intensity = 1f // Prediction из Image 89
            };
            neuroPoints.Add(hp93);

            HeatPoint hp94 = new()
            {
                Latitude = 43.010053,
                Longitude = 131.910750,
                Intensity = 1f // Prediction из Image 90
            };
            neuroPoints.Add(hp94);

            HeatPoint hp95 = new()
            {
                Latitude = 43.010112,
                Longitude = 131.910744,
                Intensity = 1f // Prediction из Image 91
            };
            neuroPoints.Add(hp95);

            HeatPoint hp96 = new()
            {
                Latitude = 43.010241,
                Longitude = 131.910766,
                Intensity = 1f // Prediction из Image 92
            };
            neuroPoints.Add(hp96);

            HeatPoint hp97 = new()
            {
                Latitude = 43.010272,
                Longitude = 131.910782,
                Intensity = 1f // Prediction из Image 93
            };
            neuroPoints.Add(hp97);

            HeatPoint hp98 = new()
            {
                Latitude = 43.010359,
                Longitude = 131.910776,
                Intensity = 0f // Prediction из Image 94
            };
            neuroPoints.Add(hp98);

            HeatPoint hp99 = new()
            {
                Latitude = 43.010402,
                Longitude = 131.910760,
                Intensity = 4.3304763e-11f // Prediction из Image 95
            };
            neuroPoints.Add(hp99);
            HeatPoint hp101 = new()
            {
                Latitude = 43.010406,
                Longitude = 131.910841,
                Intensity = 1f // Prediction из Image 97
            };
            neuroPoints.Add(hp101);

            HeatPoint hp102 = new()
            {
                Latitude = 43.010414,
                Longitude = 131.910771,
                Intensity = 1f // Prediction из Image 98
            };
            neuroPoints.Add(hp102);

            HeatPoint hp103 = new()
            {
                Latitude = 43.010488,
                Longitude = 131.910766,
                Intensity = 1f // Prediction из Image 99
            };
            neuroPoints.Add(hp103);

            HeatPoint hp104 = new()
            {
                Latitude = 43.010492,
                Longitude = 131.910760,
                Intensity = 1f // Prediction из Image 100
            };
            neuroPoints.Add(hp104);

            HeatPoint hp105 = new()
            {
                Latitude = 43.010465,
                Longitude = 131.910814,
                Intensity = 1f // Prediction из Image 101
            };
            neuroPoints.Add(hp105);

            HeatPoint hp106 = new()
            {
                Latitude = 43.010500,
                Longitude = 131.910862,
                Intensity = 1f // Prediction из Image 102
            };
            neuroPoints.Add(hp106);

            HeatPoint hp107 = new()
            {
                Latitude = 43.010516,
                Longitude = 131.910734,
                Intensity = 1f // Prediction из Image 103
            };
            neuroPoints.Add(hp107);

            HeatPoint hp108 = new()
            {
                Latitude = 43.010237,
                Longitude = 131.910948,
                Intensity = 1f // Prediction из Image 104
            };
            neuroPoints.Add(hp108);

            HeatPoint hp109 = new()
            {
                Latitude = 43.010280,
                Longitude = 131.910921,
                Intensity = 1f // Prediction из Image 105
            };
            neuroPoints.Add(hp109);

            HeatPoint hp110 = new()
            {
                Latitude = 43.010221,
                Longitude = 131.910927,
                Intensity = 1f // Prediction из Image 106
            };
            neuroPoints.Add(hp110);

            HeatPoint hp111 = new()
            {
                Latitude = 43.010174,
                Longitude = 131.910916,
                Intensity = 1f // Prediction из Image 107
            };
            neuroPoints.Add(hp111);

            HeatPoint hp112 = new()
            {
                Latitude = 43.010041,
                Longitude = 131.910857,
                Intensity = 1f // Prediction из Image 108
            };
            neuroPoints.Add(hp112);

            HeatPoint hp113 = new()
            {
                Latitude = 43.009966,
                Longitude = 131.910830,
                Intensity = 1f // Prediction из Image 109
            };
            neuroPoints.Add(hp113);

            HeatPoint hp114 = new()
            {
                Latitude = 43.010088,
                Longitude = 131.910948,
                Intensity = 1f // Prediction из Image 110
            };
            neuroPoints.Add(hp114);

            HeatPoint hp115 = new()
            {
                Latitude = 43.009837,
                Longitude = 131.910809,
                Intensity = 1f // Prediction из Image 111
            };
            neuroPoints.Add(hp115);

            HeatPoint hp116 = new()
            {
                Latitude = 43.009708,
                Longitude = 131.910776,
                Intensity = 1f // Prediction из Image 112
            };
            neuroPoints.Add(hp116);

            HeatPoint hp117 = new()
            {
                Latitude = 43.009731,
                Longitude = 131.910798,
                Intensity = 1f // Prediction из Image 113
            };
            neuroPoints.Add(hp117);

            HeatPoint hp118 = new()
            {
                Latitude = 43.009692,
                Longitude = 131.910782,
                Intensity = 1f // Prediction из Image 114
            };
            neuroPoints.Add(hp118);

            HeatPoint hp119 = new()
            {
                Latitude = 43.009664,
                Longitude = 131.910803,
                Intensity = 1f // Prediction из Image 115
            };
            neuroPoints.Add(hp119);

            HeatPoint hp120 = new()
            {
                Latitude = 43.009629,
                Longitude = 131.910771,
                Intensity = 1f // Prediction из Image 116
            };
            neuroPoints.Add(hp120);

            HeatPoint hp121 = new()
            {
                Latitude = 43.009519,
                Longitude = 131.910787,
                Intensity = 1f // Prediction из Image 117
            };
            neuroPoints.Add(hp121);

            HeatPoint hp122 = new()
            {
                Latitude = 43.009472,
                Longitude = 131.910776,
                Intensity = 1f // Prediction из Image 118
            };
            neuroPoints.Add(hp122);

            HeatPoint hp123 = new()
            {
                Latitude = 43.009504,
                Longitude = 131.910750,
                Intensity = 1f // Prediction из Image 119
            };
            neuroPoints.Add(hp123);

            HeatPoint hp124 = new()
            {
                Latitude = 43.009460,
                Longitude = 131.910766,
                Intensity = 1f // Prediction из Image 120
            };
            neuroPoints.Add(hp124);

            HeatPoint hp125 = new()
            {
                Latitude = 43.009445,
                Longitude = 131.910787,
                Intensity = 1f // Prediction из Image 121
            };
            neuroPoints.Add(hp125);

            HeatPoint hp126 = new()
            {
                Latitude = 43.009523,
                Longitude = 131.910664,
                Intensity = 1f // Prediction из Image 122
            };
            neuroPoints.Add(hp126);

            HeatPoint hp127 = new()
            {
                Latitude = 43.009559,
                Longitude = 131.910658,
                Intensity = 1f // Prediction из Image 123
            };
            neuroPoints.Add(hp127);

            HeatPoint hp128 = new()
            {
                Latitude = 43.009586,
                Longitude = 131.910696,
                Intensity = 1f // Prediction из Image 124
            };
            neuroPoints.Add(hp128);

            HeatPoint hp129 = new()
            {
                Latitude = 43.009594,
                Longitude = 131.910675,
                Intensity = 1.5410747e-11f // Prediction из Image 125
            };
            neuroPoints.Add(hp129);
        }
        private void initialiseGasPont() {
            gasPoints.Add(new HeatPoint { Latitude = 43.009747, Longitude = 131.910825, Intensity = 500f / 600 });
            gasPoints.Add(new HeatPoint { Latitude = 43.009915, Longitude = 131.910728, Intensity = 500f / 600 });
            gasPoints.Add(new HeatPoint { Latitude = 43.010080, Longitude = 131.910734, Intensity = 200f / 600 });
            gasPoints.Add(new HeatPoint { Latitude = 43.010406, Longitude = 131.910841, Intensity = 450f / 600 });
            gasPoints.Add(new HeatPoint { Latitude = 43.010414, Longitude = 131.910771, Intensity = 200f / 600 });
            gasPoints.Add(new HeatPoint { Latitude = 43.010488, Longitude = 131.910766, Intensity = 450f / 600 });
            gasPoints.Add(new HeatPoint { Latitude = 43.010516, Longitude = 131.910734, Intensity = 400f / 600 });
            gasPoints.Add(new HeatPoint { Latitude = 43.009445, Longitude = 131.910787, Intensity = 550f / 600 });
            gasPoints.Add(new HeatPoint { Latitude = 43.009523, Longitude = 131.910664, Intensity = 400f / 600 });

        }
        private void AddHeatmapPoint(double lat, double lng, int intensity)
        {
            // Создаем круг для отображения интенсивности
            int radius = 20; // Радиус круга
            Color color = Color.FromArgb(255, 255, 0, 0); // Красный цвет
            var circle = new CustomMarker(new PointLatLng(lat, lng), color, radius);
            heatmapOverlay.Markers.Add(circle);

            gMapControl.Refresh(); // Обновляем карту
        }
        private void AddPhotoPoint(double lat, double lng, Image[] images, int spacing, int imageWidth, int imageHeight)
        {
            // Создаем маркер с фотографиями
            var customMarker = new PhotoMarker(new PointLatLng(lat, lng), images, spacing, imageWidth, imageHeight);
            photoOverlay.Markers.Add(customMarker);

            gMapControl.Refresh(); // Обновляем карту
        }
        private void button5_Click(object sender, EventArgs e)
        {
            gMapControl.Zoom += 1;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            isMetanMap = false;
            isHeatMap = false;
            isNeuroMap = false;
            refreshMap();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            isMetanMap = false;
            isHeatMap = true;
            isNeuroMap = false;
            refreshMap();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            isMetanMap = true; 
            isHeatMap = false;
            isNeuroMap = false;
            refreshMap();
        }

        private void button4_Click(object sender, EventArgs e)
        {

            isMetanMap = false;
            isHeatMap = false;
            isNeuroMap = true;
            refreshMap();
        }
        private void refreshMap() {

            heatmapOverlay.IsVisibile = !(isHeatMap || isMetanMap || isNeuroMap);
            pictureBox1.Visible = (isHeatMap || isMetanMap || isNeuroMap);
            
            if (isHeatMap) pictureBox1.Image = Image.FromFile("scalaTemp.png");
            if (isMetanMap) pictureBox1.Image = Image.FromFile("scalaMet.png");
            if (isNeuroMap) pictureBox1.Image = Image.FromFile("scalaNeuro.png");
            if (gMapControl.Zoom >= 13)
                photoOverlay.IsVisibile = !(isHeatMap || isMetanMap || isNeuroMap);
            else { 
                photoOverlay.IsVisibile = false;
            }
            if (isHeatMap)
            {
                GetHeatMap(heatPoints, "temper", heatOverlay);
                
                CheckHeatMap("temper");
            }
            if (isMetanMap)
            {
                GetHeatMap(gasPoints, "gas", gasOverlay);
                
                CheckHeatMap("gas");
            }
            if (isNeuroMap)
            {
                GetHeatMap(neuroPoints, "neuro", zagrOverlay);
                
                CheckHeatMap("neuro");
            }
            foreach (GMapOverlay gMapOverlay in heatOverlay) gMapOverlay.IsVisibile = isHeatMap;
            foreach (GMapOverlay gMapOverlay in gasOverlay) gMapOverlay.IsVisibile = isMetanMap;
            foreach (GMapOverlay gMapOverlay in zagrOverlay) gMapOverlay.IsVisibile = isNeuroMap;
            gMapControl.Refresh(); // Обновляем карту
            gMapControl.Update();
        }
        private void button6_Click(object sender, EventArgs e)
        {
            gMapControl.Zoom -= 1;
        }

        private void button7_Click(object sender, EventArgs e)
        {

            gMapControl.Update();
        }

        private void button8_Click(object sender, EventArgs e)
        {
            gMapControl.Position = new PointLatLng(gMapControl.Position.Lat + 0.01, gMapControl.Position.Lng);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            gMapControl.Position = new PointLatLng(gMapControl.Position.Lat - 0.01, gMapControl.Position.Lng);
        }

        private void button10_Click(object sender, EventArgs e)
        {
            gMapControl.Position = new PointLatLng(gMapControl.Position.Lat, gMapControl.Position.Lng + 0.01);
        }

        private void button11_Click(object sender, EventArgs e)
        {
            gMapControl.Position = new PointLatLng(gMapControl.Position.Lat, gMapControl.Position.Lng - 0.01);
        }

        private void gMapControl_Load(object sender, EventArgs e)
        {

        }

        private void button12_Click(object sender, EventArgs e)
        {
            Form2 dataEntryForm = new();
            if (dataEntryForm.ShowDialog() == DialogResult.OK) // Проверяем, был ли результат OK
            {
                float[] data = dataEntryForm.GetData(); // Получаем данные
                                                        // Здесь вы можете использовать полученные данные
                MessageBox.Show("Добавлена точка");
                HeatPoint hp1 = new()
                {
                    Latitude = data[0],
                    Longitude = data[1],
                    Intensity = data[2]
                };
                heatPoints.Add(hp1);
            }
        }
    }
}
