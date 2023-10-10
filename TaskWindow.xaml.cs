using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace SPNP
{
    public partial class TaskWindow : Window
    {
        public TaskWindow()
        {
            InitializeComponent();
        }

        #region Старый стиль, использование класса Task
        private void DemoBtn1_Click(object sender, RoutedEventArgs e)
        {
            textBlockLog.Text = "";

            // Task запускается в отдельном потоке
            Task task = new Task(demo1);  // объект и запуск
            task.Start();

            Task task2 = Task.Run(demo1);  // запуск и возвращение уже запущенного объекта Task
        }

        private void demo1()
        {
            Dispatcher.Invoke(() => textBlockLog.Text += "demo1 start\n");
            Thread.Sleep(1000);
            Dispatcher.Invoke(() => textBlockLog.Text += "demo1 finish\n");
        }
        #endregion


        #region Новый стиль, использование async/await
        // WPF позволяет создавать и вызывать async обработчики событий
        private async void DemoBtn2_Click(object sender, RoutedEventArgs e)
        {
            textBlockLog.Text = "";

            Task<string> task = demo2();  // метод возвращает Task в "рабочем" состоянии
            textBlockLog.Text += $"demo2 work1\n";  // здесь то, что может выполняться паралельно (пока выполняется код в async методе demo2())
            textBlockLog.Text += $"demo2 work2\n";
            string str = await task;  // ждёт перехода Task в "завершенное" состояние и делает распаковку (из Task<string> в string)

            //string str = await demo2();  // demo2() возвращает Task, await ждёт выполнение и распаковывает string
            textBlockLog.Text += $"demo2 result: {str}\n";
        }

        private async Task<string> demo2()
        {
            // в async методах можно обращаться к UI без Dispatcher
            textBlockLog.Text += "demo2 start\n";
            await Task.Delay(2000);  // альтернатива Thread.Sleep(2000)
            return "Done";  // автоматически преобразовывается в тип Task<string>
        }
        #endregion


        #region ДЗ (последовательность и паралельность)
        /*
            С помощью многозадачности реализовать след. схему:
            - Есть 2-3 ProgressBar и 2 кнопки
            - Первая стартует последовательную работу PB (один заполняется, за ним другой)
            - Вторая стартует паралельную работу (все заполняются одновременно)
            - Заложить разное время заполнения РB для наглядности
        */
        private bool IsWork { get; set; } = false;
        private static Random r = new Random();
        private async void StartSequentially_Click(object sender, RoutedEventArgs e)
        {
            if (!IsWork)  // если работа с PB не идёт
            {
                IsWork = true;
                await AddProgressBar(new ProgressWork { ProgressBar = progressBar1, Delay = r.Next(100, 300) });
                await AddProgressBar(new ProgressWork { ProgressBar = progressBar2, Delay = r.Next(100, 300) });
                await AddProgressBar(new ProgressWork { ProgressBar = progressBar3, Delay = r.Next(100, 300) });
                ClearProgressBar();  // обнуляем PB
                IsWork = false;
            }
            else MessageBox.Show("PB заняты!");
        }

        private async void StartParallel_Click(object sender, RoutedEventArgs e)
        {
            if (!IsWork)  // если работа с PB не идёт
            {
                IsWork = true;
                Task[] tasks = new[]  // делаем массив Tasks
                {
                    AddProgressBar(new ProgressWork { ProgressBar = progressBar1, Delay = r.Next(100, 500) }),
                    AddProgressBar(new ProgressWork { ProgressBar = progressBar2, Delay = r.Next(100, 500) }),
                    AddProgressBar(new ProgressWork { ProgressBar = progressBar3, Delay = r.Next(100, 500) })
                };
                await Task.WhenAll(tasks);  // дожидаемся завершения всех задач параллельно
                ClearProgressBar();  // обнуляем PB
                IsWork = false;
            }
            else MessageBox.Show("PB заняты!");
        }

        private async Task AddProgressBar(ProgressWork progressWork)
        {
            for (int i = 0; i <= 10; i++)
            {
                progressWork.ProgressBar.Value = i * 10;  // добавляем прогресс
                await Task.Delay(progressWork.Delay);  // ожидание
            }
        }

        private void ClearProgressBar()
        {
            progressBar1.Value = 0;
            progressBar2.Value = 0;
            progressBar3.Value = 0;
        }

        // Класс для передачи в параметр async метода
        private class ProgressWork
        {
            public ProgressBar ProgressBar { get; set; } = null!;
            public int Delay { get; set; }
        }

        #endregion
    }
}
