using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Smbb.DocumentScanner.Control
{
    static class Utils{

        public static PointCollection ToPointCollection(List<AForge.IntPoint> corners)
        {
            PointCollection points = new PointCollection();
            foreach (AForge.IntPoint point in corners)
            {
                points.Add(new Point(point.X, point.Y));
            }

            return points;
        }

        public static PointCollection ToPointCollection(List<AForge.IntPoint> corners, double scale)
        {
            PointCollection points = new PointCollection();
            foreach (AForge.IntPoint point in corners)
            {
                points.Add(new Point(point.X * scale, point.Y * scale));
            }

            return points;
        }

        public static PointCollection ScalePoints(PointCollection points, double scale)
        {
            PointCollection scaledpoints = new PointCollection();
            foreach (Point point in points)
            {
                scaledpoints.Add(new Point(point.X * scale, point.Y * scale));
            }

            return scaledpoints;
        }

        public static List<AForge.IntPoint> ToIntPointList(PointCollection corners)
        {
            List<AForge.IntPoint> points = new List<AForge.IntPoint>();
            foreach (Point point in corners)
            {
                points.Add(new AForge.IntPoint((int)point.X, (int)point.Y));
            }

            return points;
        }

        public static List<AForge.IntPoint> ToIntPointList(PointCollection corners, double scale)
        {
            List<AForge.IntPoint> points = new List<AForge.IntPoint>();
            foreach (Point point in corners)
            {
                points.Add(new AForge.IntPoint((int)(point.X * scale), (int)(point.Y * scale)));
            }

            return points;
        }


        public static bool isPolygonValid(PointCollection points, double minAngle = Math.PI / 3)
        {
            int n = points.Count;

            for (int i = 0; i < n; i++)
            {
                var p1 = points[(n + i - 1) % n];
                var p2 = points[i];
                var p3 = points[(i + 1) % n];
                Vector v1 = p1 - p2;
                Vector v2 = p3 - p2;
                double dot = v1.X * v2.X + v1.Y * v2.Y;
                double det = v1.X * v2.Y - v1.Y * v2.X;

                if (Math.Abs(Math.Atan2(det, dot)) < minAngle)
                {
                    return false;
                }
            }

            return true;
        }

    }
    
}
