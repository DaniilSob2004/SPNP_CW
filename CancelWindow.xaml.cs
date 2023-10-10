using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SPNP
{
    public partial class CancelWindow : Window
    {
        private CancellationTokenSource cancellationTokenSource = null!;
        private int taskCountActive;
        private readonly object countLocker = new();

        public CancelWindow()
        {
            InitializeComponent();
        }


        #region void метод, параллельная работа, отмена, и rollback, использование без await
        private void StartBtn1_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource = new CancellationTokenSource();
            taskCountActive = 0;
            RunProgressCancellable(progressBar10, cancellationTokenSource.Token);  // fire-and-forget -> запустили и забыли
            RunProgressCancellable(progressBar11, cancellationTokenSource.Token, 4);
            RunProgressCancellable(progressBar12, cancellationTokenSource.Token, 2);
        }

        private void StopBtn1_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource?.Cancel();
        }

        private async void RunProgressCancellable(ProgressBar progressBar, CancellationToken token, int time = 3)
        {
            progressBar.Value = 0;
            lock (countLocker) { taskCountActive++; }
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    progressBar.Value += 10;
                    await Task.Delay(1000 * time / 10);
                    token.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException)
            {
                // rollback
                if (progressBar.Value < 100)
                {
                    progressBar.Foreground = Brushes.Tomato;
                    for (int i = (int)progressBar.Value / 10; i > 0; i--)
                    {
                        progressBar.Value -= 10;
                        await Task.Delay(1000 * time / 10);
                    }
                }
            }
            finally
            {
                progressBar.Foreground = Brushes.Green;
                bool isLast;
                lock (countLocker)
                {
                    isLast = (--taskCountActive) == 0;
                }
                if (isLast)
                {
                    MessageBox.Show("Конец");
                }
            }
        }
        #endregion


        #region Task метод, последовательная работа, отмена, использование await
        private async void StartBtn2_Click(object sender, RoutedEventArgs e)
        {
            ClearProgressBar(progressBar20, progressBar21, progressBar22);
            cancellationTokenSource = new CancellationTokenSource();
            await RunProgressWaitable(progressBar20, cancellationTokenSource.Token);
            await RunProgressWaitable(progressBar21, cancellationTokenSource.Token, 4);
            await RunProgressWaitable(progressBar22, cancellationTokenSource.Token, 2);
        }

        private void StopBtn2_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource?.Cancel();
        }

        private async Task RunProgressWaitable(ProgressBar progressBar, CancellationToken token, int time = 3)
        {
            progressBar.Value = 0;
            for (int i = 0; i < 10; i++)
            {
                progressBar.Value += 10;
                await Task.Delay(1000 * time / 10);
                if (token.IsCancellationRequested) break;
            }
        }
        #endregion


        #region void метод, последовательная работа, отмена, использование await Task.Run()
        private async void StartBtn3_Click(object sender, RoutedEventArgs e)
        {
            ClearProgressBar(progressBar30, progressBar31, progressBar32);
            cancellationTokenSource = new CancellationTokenSource();

            // !!! Всё равно не работает
            await Task.Run(() => RunProgress(progressBar30, cancellationTokenSource.Token));
            await Task.Run(() => RunProgress(progressBar31, cancellationTokenSource.Token, 4));
            await Task.Run(() => RunProgress(progressBar32, cancellationTokenSource.Token, 2));
        }

        private void StopBtn3_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource?.Cancel();
        }

        private async void RunProgress(ProgressBar progressBar, CancellationToken token, int time = 3)
        {
            for (int i = 0; i < 10; i++)
            {
                Dispatcher.Invoke(() => progressBar.Value += 10);
                await Task.Delay(1000 * time / 10);
                if (token.IsCancellationRequested) break;
            }
        }
        #endregion


        private void ClearProgressBar(params ProgressBar[] progressBars)
        {
            foreach (var pb in progressBars) pb.Value = 0;
        }


        #region Иллюстрация того, что await не блокирует выполнение других обработчиков
        private async void StartBtnTest1_Click(object sender, RoutedEventArgs e)
        {
            await Task.Delay(5000);
            MessageBox.Show("Button 1");
        }

        private async void StartBtnTest2_Click(object sender, RoutedEventArgs e)
        {
            await Task.Delay(3000);
            MessageBox.Show("Button 2");
        }
        #endregion
    }
}
