﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace norsu.ass.Server
{
    static class ImageProcessor
    {
        public const string ACCEPTED_EXTENSIONS = @".BMP.JPG.JPEG.GIF.PNG.BMP.DIB.RLE.JPE.JFIF";

        public static bool IsAccepted(string file)
        {
            if (file == null) return false;
            var ext = System.IO.Path.GetExtension(file)?.ToUpper();
            return File.Exists(file) && (ACCEPTED_EXTENSIONS.Contains(ext));
            
        }

        public static Image Resize(Image imgPhoto, int size)
        {
            return Resize(imgPhoto, size, Color.White);
        }

        public static Image Resize(Image imgPhoto, int size, Color background)
        {
            var sourceWidth = imgPhoto.Width;
            var sourceHeight = imgPhoto.Height;
            var sourceX = 0;
            var sourceY = 0;
            var destX = 0;
            var destY = 0;
            var nPercent = 0.0f;

            if (sourceWidth > sourceHeight)
                nPercent = (size / (float) sourceWidth);

            else
                nPercent = (size / (float) sourceHeight);



            var destWidth = (int) (sourceWidth * nPercent);
            var destHeight = (int) (sourceHeight * nPercent);

            var bmPhoto = new Bitmap(destWidth, destHeight, PixelFormat.Format32bppArgb);
            bmPhoto.SetResolution(imgPhoto.HorizontalResolution, imgPhoto.VerticalResolution);

            var grPhoto = Graphics.FromImage(bmPhoto);
            grPhoto.InterpolationMode = InterpolationMode.HighQualityBicubic;
            grPhoto.Clear(background);
            grPhoto.DrawImage(imgPhoto,
                new Rectangle(destX, destY, destWidth, destHeight),
                new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                GraphicsUnit.Pixel);

            grPhoto.Dispose();
            return bmPhoto;
        }
    }
}