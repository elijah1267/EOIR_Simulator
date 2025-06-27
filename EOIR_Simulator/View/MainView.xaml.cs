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
using EOIR_Simulator.Radar;
using System.ComponentModel;
using System.Windows.Input;

namespace EOIR_Simulator.View
{
    public partial class MainView : Window
    {
        private readonly MainViewModel _vm;
        private readonly RadarProcessor _radarProcessor;

        public MainView()
        {
            InitializeComponent();

            _radarProcessor = new RadarProcessor(RadarCanvas, Dispatcher);
            _radarProcessor.SetupSerial("COM4", 921600);
            _radarProcessor.SetupCli("COM3", 115200);
            _radarProcessor.StartMonitoringPorts();

            _vm = new MainViewModel(_radarProcessor);
            DataContext = _vm;
            _vm.StartServices();

            Closing += MainView_Closing;
        }

        private void MainView_Closing(object sender, CancelEventArgs e)
        {
            // 1) 레이더 센서 정지 (sensorStop + 점·FOV 제거)
            _radarProcessor.StopRadar();
            _radarProcessor.StopMonitoringPorts();
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

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleWindowState(); // 더블클릭 시 최대/복원
            }
            else if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaxRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindowState();
        }

        private void ToggleWindowState()
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
