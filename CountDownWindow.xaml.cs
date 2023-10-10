using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SPNP
{
    public partial class CountDownWindow : Window
    {
        private Mutex mutex;

        public CountDownWindow(Mutex mutex)
        {
            InitializeComponent();
            this.mutex = mutex;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Run(Tick);
        }

        private void Tick()
        {
            if (!mutex.WaitOne(100))
            {
                Dispatcher.Invoke(() =>
                {
                    --progressBar1.Value;
                    if (progressBar1.Value == 0)
                    {
                        DialogResult = false;
                    }
                    else
                    {
                        Task.Run(Tick);
                    }
                });
            }
            else
            {
                mutex.ReleaseMutex();
                DialogResult = true;
            }
        }
    }
}
