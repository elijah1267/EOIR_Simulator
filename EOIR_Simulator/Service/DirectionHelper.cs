using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EOIR_Simulator.Model;
using System.Windows.Media.Media3D;

namespace EOIR_Simulator.Service
{
    static class DirectionHelper
    {
        // angleX = Yaw , angleY = Pitch   (byte 0~180)
        public static (Point3D p, Vector3D v, Transform3D cube)
               Calc(byte angleX, byte angleY)
        {
            const double RAD = System.Math.PI / 180.0;

            double yaw = (angleX - 90) * RAD;  // X축(빨강) 기준
            double pitch = (angleY - 90) * RAD;

            double cosP = System.Math.Cos(pitch);
            double dx = cosP * System.Math.Cos(yaw);
            double dy = cosP * System.Math.Sin(yaw);
            double dz = System.Math.Sin(pitch);

            var dirP = new Point3D(dx, dy, dz);
            var dirV = new Vector3D(dx, dy, dz);

            /* 큐브 Transform 계산 (기존 코드 그대로) */
            var zAxis = new Vector3D(0, 0, 1);
            var axis = Vector3D.CrossProduct(zAxis, dirV);
            double ang = Vector3D.AngleBetween(zAxis, dirV);

            var grp = new Transform3DGroup();
            if (axis.Length > 1e-6)
                grp.Children.Add(new RotateTransform3D(
                    new AxisAngleRotation3D(axis, ang)));
            grp.Children.Add(new TranslateTransform3D(
                    dx * 0.35, dy * 0.35, dz * 0.35));

            grp.Freeze();     // 다른 스레드 사용용

            return (dirP, dirV, grp);
        }
    }
}
