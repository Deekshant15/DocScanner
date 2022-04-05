using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Telerik.Windows.Controls;
using Telerik.Windows.MediaFoundation;

namespace Smbb.DocumentScanner.Control
{
    /// <summary>
    /// Logique d'interaction pour DocumentScanner.xaml
    /// </summary>
    public partial class DocumentScanner : UserControl
    {
        DocumentScannerViewModel dm;
        RadWebCam camera;
        DispatcherTimer dt;

        public DocumentScanner()
        {
            InitializeComponent();
            dm = new DocumentScannerViewModel();
            root.DataContext = dm;

            dm.AllCameras = RadWebCam.GetVideoCaptureDevices().Select(d => d.FriendlyName).ToList();
            if (dm.SelectedCamera == null) dm.SelectedCamera = (from r in dm.AllCameras where r.Contains(" Rear") select r).FirstOrDefault();
            if (dm.SelectedCamera == null) dm.SelectedCamera = dm.AllCameras.FirstOrDefault();


            this.Loaded += BarcodeScannerControl_Loaded;
        }


        private void BarcodeScannerControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (camera == null)
            {
                camera = new RadWebCam { ToolbarPanelVisibility = Visibility.Collapsed, PreviewSnapshots = false };
                cameraContainer.Children.Add(camera);
                camera.SnapshotTaken += Camera_SnapshotTaken;

                dt = new DispatcherTimer();
                dt.Interval = TimeSpan.FromMilliseconds(200);
                dt.Tick += Dt_Tick;
                dt.Start();
            }
        }

        private void Camera_SnapshotTaken(object sender, SnapshotTakenEventArgs e)
        {
            var bitmap = BitmapFromSource(e.Snapshot);



            #warning: TODO...
            if (/*  */ false)
            {
                //dt.Stop();
                //camera.Stop();
                dm.DocumentFound = true;
                dm.DocumentImage = null; //...

            }
        }



        static Bitmap BitmapFromSource(BitmapSource bitmapsource)
        {
            Bitmap bitmap;
            using (var outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapsource));
                enc.Save(outStream);
                bitmap = new Bitmap(outStream);
            }
            return bitmap;
        }

        private void Dt_Tick(object sender, EventArgs e)
        {
            camera.TakeSnapshot();
        }

        bool firstInit = true;

        public void InitCamera(RadWebCam webCam)
        {
            try
            {
                if (!firstInit)
                {
                    // fix a bug when changing camera
                    webCam.Dispose();
                }
                firstInit = false;
            }
            catch { }

            try
            {
                var videoDevices = RadWebCam.GetVideoCaptureDevices();
                MediaFoundationDeviceInfo cam = null;
                cam = videoDevices.SingleOrDefault(d => d.FriendlyName == dm.SelectedCamera);

                var videoFormats = RadWebCam.GetVideoFormats(cam);

                var maxResolution = (from r in videoFormats orderby r.FrameSizeWidth * r.FrameSizeHeight descending select r).FirstOrDefault();

                webCam.Initialize(cam, maxResolution);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
                //Toolkit.Log.Logger.Error(ex);
            }
        }

        public void InitAndStartCamera(RadWebCam webCam)
        {
            Dispatcher.BeginInvoke(new Action(delegate ()
            {
                InitCamera(webCam);
                webCam.Start();
                webCam.Visibility = Visibility.Visible;

            }));
        }

        private void RadComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (camera == null)
                return;

            if (camera != null)
            {
                camera.Stop();
                InitAndStartCamera(camera);
            }
        }
    }
}
