using System;
using System.Windows;
using System.Windows.Media.Media3D;
using EOIR_Simulator.Service;
using EOIR_Simulator.Utils;

namespace EOIR_Simulator.ViewModel
{
    public sealed class AngleVM : ObservableObject
    {
        private readonly System.Windows.Threading.Dispatcher _ui =
            Application.Current.Dispatcher;

        private byte _angleX, _angleY;
        public byte AngleX { get => _angleX; private set { _angleX = value; RaisePropertyChanged(); } }
        public byte AngleY { get => _angleY; private set { _angleY = value; RaisePropertyChanged(); } }

        private Point3D _dirPoint = new Point3D(0, 0, 1);
        public Point3D DirPoint { get => _dirPoint; private set { _dirPoint = value; RaisePropertyChanged(); } }

        private Vector3D _dirVector = new Vector3D(0, 0, 1);
        public Vector3D DirVector { get => _dirVector; private set { _dirVector = value; RaisePropertyChanged(); } }

        private Transform3D _cubeTransform = Transform3D.Identity;
        public Transform3D CubeTransform { get => _cubeTransform; private set { _cubeTransform = value; RaisePropertyChanged(); } }

        public AngleVM(AngleReceiver rx)
        {
            rx.AngleReceived += (x, y) =>
            {
                if (_ui.HasShutdownStarted) return;

                _ui.BeginInvoke(new Action(() =>
                {
                    Update(x, y);
                }));
            };
        }

        private void Update(byte x, byte y)
        {
            AngleX = x;
            AngleY = y;

            var res = DirectionHelper.Calc(x, y);
            DirPoint = res.p;
            DirVector = res.v;
            CubeTransform = res.cube;
        }
    }
}
