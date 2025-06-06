using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using EOIR_Simulator.Model;
using EOIR_Simulator.Service;
using EOIR_Simulator.Utils;

namespace EOIR_Simulator.ViewModel
{
    public sealed class VideoVM : ObservableObject
    {
        private readonly PacketReceiver _rx;
        private readonly System.Windows.Threading.Dispatcher _ui =
            Application.Current.Dispatcher;

        public bool AcceptFrames { get; set; } = true;

        private BitmapSource _currentFrame;
        public BitmapSource CurrentFrame
        {
            get => _currentFrame;
            private set { _currentFrame = value; RaisePropertyChanged(); }
        }

        public ObservableCollection<ObjectInfo> Objects { get; }
            = new ObservableCollection<ObjectInfo>();

        public VideoVM(PacketReceiver rx)
        {
            _rx = rx;
            _rx.FrameArrived += OnFrame;
        }

        private void OnFrame(FramePacket fp)
        {
            if (!AcceptFrames) return;  //IDLE일 때 무시

            BitmapImage bmp;
            using (var ms = new MemoryStream(fp.JpegBytes))
            {
                bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
            }

            _ui.BeginInvoke(new Action(() =>
            {
                CurrentFrame = bmp;

                Objects.Clear();
                foreach (var o in fp.Objects)
                    Objects.Add(o);
            }));
        }

        public void Clear()
        {
            CurrentFrame = null;
            Objects.Clear();
        }
    }
}
