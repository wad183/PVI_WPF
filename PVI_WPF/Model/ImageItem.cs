using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace PVI_WPF
{
    internal class ImageItem
    {
        public String ImagePath { get; set; } = "";

        public string ImageName { get; set; } = "";

        public int ImageIndex { get; set; }
        public string FileName {
            get { return Path.GetFileName(ImagePath); }
        }
    }
}
