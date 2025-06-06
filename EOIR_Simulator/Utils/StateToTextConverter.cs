using System;
using System.Globalization;
using System.Windows.Data;
using EOIR_Simulator.Model;          // SimState enum

namespace EOIR_Simulator.Utils
{
    public sealed class StateToTextConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            var s = (SimState)value;
            return s == SimState.Idle ? "[State : IDLE]" : "[State : 운용 상태]";
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c)
                => Binding.DoNothing;
    }
}
