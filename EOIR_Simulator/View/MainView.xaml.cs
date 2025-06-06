using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

using EOIR_Simulator.Model;
using EOIR_Simulator.Service;
using EOIR_Simulator.ViewModel;

namespace EOIR_Simulator.View
{
    public partial class MainView : Window
    {
        private readonly MainViewModel _vm;

        public MainView()
        {
            InitializeComponent();

            _vm = new MainViewModel();
            DataContext = _vm;
            _vm.StartServices();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm.TcpLogs is INotifyCollectionChanged notifyCollection)
            {
                notifyCollection.CollectionChanged += (s, args) =>
                {
                    if (args.Action == NotifyCollectionChangedAction.Add && _vm.TcpLogs.Count > 0)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogListBox.ScrollIntoView(_vm.TcpLogs[0]);
                        });
                    }
                };
            }
        }
    }
}
