﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Smbb.DocumentScanner.Control
{
    internal class DocumentScannerViewModel : BaseViewModel
    {
        List<string> allCameras;
        public List<string> AllCameras
        {
            get { return this.allCameras; }
            set
            {
                this.allCameras = value;
                OnPropertyChanged(nameof(AllCameras));
            }
        }

        string selectedCamera;
        public string SelectedCamera
        {
            get { return this.selectedCamera; }
            set
            {
                this.selectedCamera = value;
                OnPropertyChanged(nameof(SelectedCamera));
            }
        }



        bool documentFound;
        public bool DocumentFound
        {
            get { return this.documentFound; }
            set
            {
                this.documentFound = value;
                OnPropertyChanged(nameof(DocumentFound));
            }
        }




        Bitmap documentImage;
        public Bitmap DocumentImage
        {
            get { return this.documentImage; }
            set
            {
                this.documentImage = value;
                OnPropertyChanged(nameof(DocumentImage));
            }
        }



    }
}
