using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EOIR_Simulator.Util
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string prop = null)
        {
            var h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(prop));
        }
    }
}
