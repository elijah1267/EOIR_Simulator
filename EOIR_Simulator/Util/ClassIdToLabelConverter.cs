using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EOIR_Simulator.Model;
using System.Windows.Data;

namespace EOIR_Simulator.Util
{
    public class ClassIdToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
                              object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            // ★ 모든 IConvertible 숫자를 int로
            int cls;
            try
            {
                cls = System.Convert.ToInt32(value); // byte/ushort/long 전부 OK
            }
            catch (Exception)
            {
                return value.ToString();
            }

            string label;
            return CocoLabelMap.TryGet(cls, out label) ? label : "#" + cls;
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
