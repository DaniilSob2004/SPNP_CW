using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SPNP
{
    public partial class ChainingWindow : Window
    {
        public ChainingWindow()
        {
            InitializeComponent();
        }


        private CancellationTokenSource cts = null!;
        private void StartBtn1_Click(object sender, RoutedEventArgs e)
        {
            if (cts is null || cts.Token.IsCancellationRequested)  // если другие задачи не выполняются
                cts = new CancellationTokenSource();
            else return;

            var task10 =
                ShowProgress(progressBar10, cts.Token)  // ShowProgress - возвращает Task
                .ContinueWith(task10 =>     // к нему задаём продолжение
                    ShowProgress(progressBar11, cts.Token)
                   .ContinueWith(task11 =>
                        ShowProgress(progressBar12, cts.Token)));  // task - результат Task (ShowProgress)

            var task20 =
                ShowProgress(progressBar20, cts.Token)  // ShowProgress - возвращает Task
                .ContinueWith(task20 =>     // к нему задаём продолжение
                    ShowProgress(progressBar21, cts.Token)
                   .ContinueWith(task21 =>
                        ShowProgress(progressBar22, cts.Token)));  // task - результат Task (ShowProgress)
        }

        private void StopBtn1_Click(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
        }

        private async void StartBtn2_Click(object sender, RoutedEventArgs e)
        {
            // 10-11-12
            // 20-21-22

            if (cts is null || cts.Token.IsCancellationRequested)  // если другие задачи не выполняются
                cts = new CancellationTokenSource();
            else return;

            var task10 = ShowProgress(progressBar10, cts.Token);
            var task20 = ShowProgress(progressBar20, cts.Token);
            await task10; var task11 = ShowProgress(progressBar11, cts.Token);
            await task20; var task21 = ShowProgress(progressBar21, cts.Token);
            await task11; var task12 = ShowProgress(progressBar12, cts.Token);
            await task21; var task22 = ShowProgress(progressBar22, cts.Token);
        }

        private void StopBtn2_Click(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
        }

        private async Task ShowProgress(ProgressBar progressBar, CancellationToken token)
        {
            int delay = 100;
            if (progressBar == progressBar10) delay = 300;
            if (progressBar == progressBar11) delay = 200;
            if (progressBar == progressBar12) delay = 100;

            if (progressBar == progressBar20) delay = 100;
            if (progressBar == progressBar21) delay = 200;
            if (progressBar == progressBar22) delay = 300;

            for (int i = 0; i <= 10; i++)
            {
                await Task.Delay(delay);
                Dispatcher.Invoke(() => progressBar.Value = i * 10);
                //progressBar.Value = i * 10;  // ошибка, внутрение задачи не видят диспетчер

                if (token.IsCancellationRequested)
                {
                    return;
                }
            }
        }



        private async void StartBtn3_Click(object sender, RoutedEventArgs e)
        {
            string str = "";

            // Первый способ (лесенкой)
            //await AddHello(str)
            //    .ContinueWith(task =>
            //    {
            //        string res = task.Result;
            //        Dispatcher.Invoke(() => logTextBlock.Text = res);
            //        AddWorld(res)
            //            .ContinueWith(task2 =>
            //            {
            //                string res = task2.Result;
            //                Dispatcher.Invoke(() => logTextBlock.Text = res);
            //                AddExclamantion(res)
            //                    .ContinueWith(task3 =>
            //                    {
            //                        string res = task3.Result;
            //                        Dispatcher.Invoke(() => logTextBlock.Text = res);
            //                    });
            //            });
            //    });

            // Второй способ (использование Unwrap())
            string text = await AddHello(str)
                .ContinueWith(task =>
                {
                    string res = task.Result;
                    Dispatcher.Invoke(() => logTextBlock.Text = res);
                    return AddWorld(res);
                })
                .Unwrap()  // снять одну "обёртку" Task<>, без неё task2 - Task<taskW> = Task<Task<string>>
                           // а с ней - task2 - Task<string> (без одной обёртки)
                .ContinueWith(task2 =>
                {
                    string res = task2.Result;  // без Unwrap task2.Result.Result
                    Dispatcher.Invoke(() => logTextBlock.Text = res);
                    return AddExclamantion(res);
                })
                .Unwrap()
                .ContinueWith(task3 => Dispatcher.Invoke(() => logTextBlock.Text = task3.Result));

            MessageBox.Show(text);  // ожидание text действует на все продолжения, т.е. в результате имеем полную фразу
        }

        private async Task<string> AddHello(string str)
        {
            await Task.Delay(1000);
            return str += " Hello ";
        }

        private async Task<string> AddWorld(string str)
        {
            await Task.Delay(1000);
            return str += " World ";
        }

        private async Task<string> AddExclamantion(string str)
        {
            await Task.Delay(1000);
            return str += " !!! ";
        }
    }
}
/*
  Task chaining. Continuations.
  Нитки кода. Продолжение.
  
  Задачи, как объекты, позволяют указать другие задачи, которые будут выполняться после того,
  как будет завершена данная.
  Другая задача наз. Продолжением первой задачи.
  Создание цепей из нескольких продолжений составляют "нитку"(chain) -
  это есть отдельный стиль в ассинхронном программировании.
*/
