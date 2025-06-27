using System;
using System.Globalization;
using System.Windows.Data;

namespace EOIR_Simulator.Util
{
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
            => v != null;
        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => throw new NotSupportedException();
    }
}
