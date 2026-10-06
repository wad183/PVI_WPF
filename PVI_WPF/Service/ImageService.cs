using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Security.Cryptography.X509Certificates;

namespace PVI_WPF
{
    internal class ImageService
    {
        
        public List<ImageItem> ImageItems = new List<ImageItem>();
        public int CurrentIndex = 0;
        public ImageItem CurrentImage => ImageItems.Count > 0 ? ImageItems[CurrentIndex] : null;
        public int? ImageCount => ImageItems.Count;

        public string FolderPath = "";

        HashSet<string> supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp"};
        public bool LoadImagesFolder(string folderPath)
        {

            if (!Directory.Exists(folderPath))
            {
                return false;
            }

            FolderPath = folderPath;
            ImageItems.Clear();

            var imageFiles = Directory.EnumerateFiles(folderPath)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file)))
                .Where(f => (File.GetAttributes(f) & FileAttributes.Hidden) == 0)
                .OrderBy(f => f)
                .ToList();

                CurrentIndex = 0;
            foreach (var imageFile in imageFiles)
                {
                    ImageItems?.Add(new ImageItem
                    {
                        ImagePath = imageFile,
                        ImageName =  Path.GetFileName(imageFile),
                        ImageIndex = ImageItems.Count
                    });
                }
                return true;
        }
        public void Next()
        {
            if (ImageCount > 0 && CurrentIndex < ImageCount - 1)
            {
                CurrentIndex++;
            }
        }
        public void Previous()
        {
            if (ImageCount > 0 && CurrentIndex > 0)
            {
                CurrentIndex--;
            }
        }
    }
}
