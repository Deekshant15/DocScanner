using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using System.Collections.Generic;
using System.Threading;
using System.Drawing.Imaging;
using AForge.Imaging.Filters;
using AForge.Imaging;
using AForge.Math.Geometry;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Smbb.DocumentScanner.Control
{
    /// <summary>
    /// Logique d'interaction pour DocumentScanner.xaml
    /// </summary>
    public partial class DocumentScanner : UserControl
    {

        DocumentScannerViewModel dm;
        VideoCaptureDevice camera;
        // DispatcherTimer dt;
        FilterInfoCollection videoDevices;
        WriteableBitmap camBitmap;
        System.Drawing.Bitmap capturedBitmap;
        PointCollection documentCorners = new PointCollection();
        bool capture = false;
        private bool mousePressed = false;
        private int pressedCorner = 0;
        private double circleSize = 20;
        private Point lastPoint;

        public DocumentScanner()
        {
            InitializeComponent();
            dm = new DocumentScannerViewModel();
            root.DataContext = dm;
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            List<string> cams = new List<string>();
            foreach (FilterInfo d in videoDevices)
            {
                cams.Add(d.Name);
            }

            dm.AllCameras = cams;
            dm.PreviewMode = false;

            if (dm.SelectedCamera == null) dm.SelectedCamera = (from r in dm.AllCameras where r.Contains(" Rear") select r).FirstOrDefault();
            if (dm.SelectedCamera == null) dm.SelectedCamera = dm.AllCameras.FirstOrDefault();


            this.Loaded += Control_Loaded;
            this.Unloaded += Control_UnLoaded;
        }


        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            InitAndStartCamera();
        }

        private void Control_UnLoaded(object sender, RoutedEventArgs e)
        {
            if(camera != null)
            {
                camera.SignalToStop();
                camera.WaitForStop();
            }
        }

        // Non-UI thread Method
        private void video_FrameHandler(object sender, NewFrameEventArgs eventArgs)
        {
            System.Drawing.Bitmap bitmap = (System.Drawing.Bitmap)eventArgs.Frame.Clone();

            // lock image
            BitmapData bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadWrite, bitmap.PixelFormat);

            /*
            // create filter
            YCbCrFiltering filter = new YCbCrFiltering();
            // set color ranges to keep
            filter.Y = new Range(0.6f, 1.0f);
            //filter.Cb = new Range(-0.2f, 0.2f);
            //filter.Cr = new Range(-0.2f, 0.2f);
            // apply the filter
            filter.ApplyInPlace(bitmapData);
            */

            UnmanagedImage grayImage = UnmanagedImage.Create(bitmap.Width, bitmap.Height, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
            
            Grayscale.CommonAlgorithms.BT709.Apply(UnmanagedImage.FromManagedImage(bitmapData), grayImage);
            bitmap.UnlockBits(bitmapData);

            // 1 - Equilize Image
            //HistogramEqualization histfilter = new HistogramEqualization();
            //histfilter.ApplyInPlace(grayImage);

            // 2 - Edge detection
            DifferenceEdgeDetector edgeDetector = new DifferenceEdgeDetector();
            UnmanagedImage edgesImage = edgeDetector.Apply(grayImage);

            // 3 - Threshold edges
            Threshold thresholdFilter = new Threshold(30);
            thresholdFilter.ApplyInPlace(edgesImage);

            BlobCounter blobCounter = new BlobCounter();
            blobCounter.FilterBlobs = true;
            blobCounter.MinHeight = bitmap.Width/20;
            blobCounter.MinWidth = bitmap.Height/20;
            blobCounter.MaxWidth = bitmap.Width - 5;
            blobCounter.MaxHeight = bitmap.Height - 5;
            blobCounter.ObjectsOrder = ObjectsOrder.Size;
            blobCounter.ProcessImage(edgesImage);
            
            Blob[] blobs = blobCounter.GetObjectsInformation();

            //GrayscaleToRGB filter = new GrayscaleToRGB();
            //bitmap = filter.Apply(edgesImage).ToManagedImage();

            if(blobs.Length > 0){
                List<AForge.IntPoint> edgePoints = blobCounter.GetBlobsEdgePoints(blobs[0]);
                SimpleShapeChecker shapeChecker = new SimpleShapeChecker();
                List<AForge.IntPoint> corners;
                if (shapeChecker.IsQuadrilateral(edgePoints, out corners))
                {
                    updateImage(bitmap, corners);
                    return;
                }
            }
            
            updateImage(bitmap, new List<AForge.IntPoint>());

        }

        private void updateBitmap(System.Drawing.Bitmap bitmap)
        {
            if(camBitmap == null || (camBitmap.PixelWidth != bitmap.Width || camBitmap.PixelHeight != bitmap.Height))
            {
                camBitmap = new WriteableBitmap(bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Bgr24, null);
                cameraImage.Source = camBitmap;
            }

            Int32Rect reg = new Int32Rect(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            camBitmap.Lock();
            camBitmap.WritePixels(reg, data.Scan0, data.Height * data.Stride, data.Stride, 0, 0);
            camBitmap.AddDirtyRect(reg);
            camBitmap.Unlock();
            bitmap.UnlockBits(data);
        }

        private void updatePolygon(PointCollection points)
        {
            cameraCanvas.Children.Clear();
            if (points.Count > 0)
            {
                Polygon polygon = new Polygon();
                polygon.Points = points;
                polygon.Width = cameraImage.ActualWidth;
                polygon.Height = cameraImage.ActualHeight;
                polygon.Stretch = Stretch.None;
                polygon.Stroke = Brushes.Green;
                polygon.StrokeThickness = 2;
                cameraCanvas.Children.Add(polygon);
            }
        }

        private void updateImage(System.Drawing.Bitmap bitmap, List<AForge.IntPoint> corners)
        {
            Dispatcher.BeginInvoke(new ThreadStart(delegate ()
            {
                if (dm.PreviewMode)
                    return;

                if (capture)
                {
                    documentCorners = Utils.ToPointCollection(corners);
                    capturedBitmap = (System.Drawing.Bitmap)bitmap.Clone();
                    dm.PreviewMode = true;
                    camera.SignalToStop();

                    if(documentCorners.Count > 0)
                    {
                        QuadrilateralTransformation filter = new QuadrilateralTransformation(corners);
                        bitmap = filter.Apply(bitmap);
                        camBitmap = null;
                        corners.Clear();

                        dm.DocumentFound = true;
                        dm.DocumentImage = bitmap;
                    }
                }

                updateBitmap(bitmap);
                updatePolygon(Utils.ToPointCollection(corners, cameraImage.ActualWidth / bitmap.Width));
                
            }));
        }

        public void InitAndStartCamera()
        {
            string device = getSelectedCamera();
            if (device == null)
                return;
            capture = false;
            dm.PreviewMode = false;
            camBitmap = null;
            Dispatcher.BeginInvoke(new Action(delegate ()
            {
                if (camera != null)
                {
                    camera.WaitForStop();
                }

                camera = new VideoCaptureDevice(device);
                //videoView.VideoSource = camera;
                camera.NewFrame += new NewFrameEventHandler(video_FrameHandler);
                camera.Start();
            }));
        }

        private void RadComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (camera == null)
                return;

            if (camera != null)
            {
                camera.SignalToStop();
                InitAndStartCamera();
            }
        }

        private void Capture_Clicked(object sender, RoutedEventArgs e)
        {
            if (camera == null)
                return;

            capture = !capture;
            captureButton.Content = capture ? "Re-Capture" : "Capture";
            if (!capture)
            {
                InitAndStartCamera();
            }
        }

        private void Adjust_Clicked(object sender, RoutedEventArgs e)
        {
            if (!dm.PreviewMode)
                return;

            if (dm.AdjustMode)
            {
                Polygon polygon = cameraCanvas.Children[0] as Polygon;
                if (!Utils.isPolygonValid(polygon.Points))
                    return;

                dm.DocumentFound = true;
                dm.AdjustMode = false;
                documentCorners = Utils.ScalePoints(polygon.Points, capturedBitmap.Width / cameraImage.ActualWidth);
                QuadrilateralTransformation filter = new QuadrilateralTransformation(Utils.ToIntPointList(documentCorners));
                dm.DocumentImage = filter.Apply(capturedBitmap);
                camBitmap = null;
                cameraCanvas.Children.Clear();
                updateBitmap(dm.DocumentImage);

            }
            else
            {
                dm.DocumentFound = false;
                dm.AdjustMode = true;
                camBitmap = null;
                updateBitmap(capturedBitmap);
                cameraImage.UpdateLayout();

                if (documentCorners.Count == 0)
                {   // Add Half Rectangle Size Selection
                    double x = capturedBitmap.Width / 4;
                    double y = capturedBitmap.Height / 4;
                    documentCorners.Add(new Point(x, y));
                    documentCorners.Add(new Point(3 * x, y));
                    documentCorners.Add(new Point(3 * x, 3 * y));
                    documentCorners.Add(new Point(x, 3 * y));
                }

                PointCollection points = Utils.ScalePoints(documentCorners, cameraImage.ActualWidth / capturedBitmap.Width);
                updatePolygon(points);

                foreach (Point point in points)
                {
                    Ellipse circle = new Ellipse();
                    circle.Fill = Brushes.White;
                    circle.Stroke = Brushes.Black;
                    circle.StrokeThickness = 1;
                    circle.Width = circleSize;
                    circle.Height = circleSize;
                    Canvas.SetLeft(circle, point.X - circleSize / 2);
                    Canvas.SetTop(circle, point.Y - circleSize / 2);
                    circle.MouseDown += cameraCanvas_MouseDown;
                    cameraCanvas.Children.Add(circle);
                }

            }
        }


        private string getSelectedCamera()
        {
            foreach (FilterInfo d in videoDevices)
            {
                if (d.Name == dm.SelectedCamera)
                {
                    return d.MonikerString;
                }
            }

            return null;
        }

        private void cameraCanvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!dm.PreviewMode)
                return;

            mousePressed = true;
            pressedCorner = cameraCanvas.Children.IndexOf(sender as Ellipse);
            lastPoint = e.GetPosition(cameraCanvas);
            
        }

        private void cameraCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!mousePressed)
                return;

            Point cp = e.GetPosition(cameraCanvas);
            Point diff = new Point(cp.X - lastPoint.X, cp.Y - lastPoint.Y);
            moveCorner(diff, pressedCorner);
            lastPoint = cp;
        }

        private void cameraCanvas_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            mousePressed = false;
        }

        private void cameraCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            mousePressed = false;
        }

        private void moveCorner(Point diff, int corner)
        {
            if (corner < 1 || cameraCanvas.Children.Count != 5)
                return;

            Polygon polygon = (Polygon)cameraCanvas.Children[0];
            PointCollection points = polygon.Points;
            int pi = corner - 1;
            Point np = new Point(points[pi].X + diff.X, points[pi].Y + diff.Y);

            double maxX = cameraImage.ActualWidth;
            double maxY = cameraImage.ActualHeight;

            if (np.X < 0)
                np.X = 0;

            if (np.X > maxX)
                np.X = maxX;

            if (np.Y < 0)
                np.Y = 0;

            if (np.Y > maxY)
                np.Y = maxY;

            points[pi] = np;
            polygon.Points = points;
            polygon.Stroke = Utils.isPolygonValid(points) ? Brushes.Green : Brushes.Red;
            Ellipse circle = (Ellipse)cameraCanvas.Children[corner];
            Canvas.SetLeft(circle, np.X - circleSize / 2);
            Canvas.SetTop(circle, np.Y - circleSize / 2);
        }
    }
}
