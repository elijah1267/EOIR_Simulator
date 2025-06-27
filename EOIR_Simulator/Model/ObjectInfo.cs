using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EOIR_Simulator.Model
{
    public class ObjectInfo : INotifyPropertyChanged
    {
        public byte Class
        {
            get => _class; set { _class = value; OnProp(); }
        }
        public byte TrackingId
        {
            get => _id; set { _id = value; OnProp(); }
        }
        public short X { get => _x; set { _x = value; OnProp(); } }
        public short Y { get => _y; set { _y = value; OnProp(); } }
        public short W { get => _w; set { _w = value; OnProp(); } }
        public short H { get => _h; set { _h = value; OnProp(); } }
        public float Confidence
        {
            get => _conf; set { _conf = value; OnProp(); }
        }

        /* ========= 내부 필드 ========= */
        private byte _class, _id;
        private short _x, _y, _w, _h;
        private float _conf;

        /* ---------- INotifyPropertyChanged ---------- */
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnProp([CallerMemberName] string p = null)
        { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p)); }

        /* 편의 메서드: 다른 ObjectInfo 로부터 값 복사 */
        public void CopyFrom(ObjectInfo src)
        {
            Class = src.Class;   // 각각 set { } 로 들어가므로 OnProp 자동 호출
            X = src.X; Y = src.Y; W = src.W; H = src.H; Confidence = src.Confidence;
        }

        /* FromBytes()·SizeInBytes 등 기존 정적 메서드는 그대로 */

        public static ObjectInfo FromBytes(byte[] data, int offset)
        {
            return new ObjectInfo
            {
                Class = data[offset + 0],
                TrackingId = data[offset + 1],
                X = BitConverter.ToInt16(data, offset + 2),
                Y = BitConverter.ToInt16(data, offset + 4),
                W = BitConverter.ToInt16(data, offset + 6),
                H = BitConverter.ToInt16(data, offset + 8),
                Confidence = BitConverter.ToSingle(data, offset + 10)
            };
        }
    }
}