using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace SPNP
{
    public partial class ThreadingWindow : Window
    {
        private static Mutex? mutex;
        private static string mutexName = "TW_MUTEX";

        public ThreadingWindow()
        {
            CheckPreviousLunch();
            InitializeComponent();
        }

        #region Использование Mutex
        private void CheckPreviousLunch()
        {
            // перенести в MainWindow (потому что конструктор уже создаёт окно и процесс остаётся запущенным)
            try { mutex = Mutex.OpenExisting(mutexName); } catch { }  // пытаемся открыть

            if (mutex is null)  // первый запуск экземпляра окна
            {
                mutex = new Mutex(true, mutexName);  // создаём
            }
            else if (!mutex.WaitOne(1))  // если mutex закрыт
            {
                MessageBox.Show("Экземпляр окна уже запущен!");
                throw new ApplicationException();
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            mutex?.ReleaseMutex();  // освобождаем
        }
        #endregion



        #region BTN1 Без потоков
        private void BtnStart1_Click(object sender, RoutedEventArgs e)
        {
            // демонстрация проблемы - зависание интерфейса
            // в течении работы метода-обработчика события все другие события станут в очередь и не обработаются
            for (int i = 0; i <= 10; i++)
            {
                progressBar1.Value = i * 10;
                Thread.Sleep(300);
            }
            // обновление окна - это тоже одна с событий, поэтому бегунок отображается сразу заполнением, а не пошагово(в цикле)
        }

        private void BtnStop1_Click(object sender, RoutedEventArgs e)
        {
            // из-за зависания интерфейса, кнопка не нажимается в течении работы кнопки "Start"
            // никакое действие не сможет остановить её работу
        }
        #endregion

        #region BTN2 Запуск потока (ошибка, т.к. нельзя изменять элементы, которые принадлежат другому потоку)
        private void BtnStart2_Click(object sender, RoutedEventArgs e)
        {
            new Thread(IncrementProgress2).Start();
        }

        private void BtnStop2_Click(object sender, RoutedEventArgs e) { }

        private void IncrementProgress2()
        {
            // проблема - с данного потока нельзя изменять элементы, которые принадлежат другому потоку.
            // для доступа к элементам интерфейса(окна) следует делегировать выполнение изменений к UI потоку
            for (int i = 0; i <= 10; i++)
            {
                progressBar2.Value = i * 10;  // здесь будет ошибка
                Thread.Sleep(300);
            }
        }
        #endregion

        #region BTN3 Запуск потока (с багом, при клике на Start, запускается постоянно новый поток)
        private bool IsStopped3 { get; set; }
        private void BtnStart3_Click(object sender, RoutedEventArgs e)
        {
            new Thread(IncrementProgress3).Start();
            IsStopped3 = false;
        }

        private void BtnStop3_Click(object sender, RoutedEventArgs e)
        {
            IsStopped3 = true;
        }

        private void IncrementProgress3()
        {
            for (int i = 0; i <= 10 && !IsStopped3; i++)
            {
                // делегирование выполнения действия(лямбды) к оконному(UI) потоку
                Dispatcher.Invoke(() => progressBar3.Value = i * 10);
                Thread.Sleep(300);
            }
        }
        #endregion

        #region BTN4 Запуск потока (правильная версия)
        private bool IsStopped4 { get; set; }
        private Thread? thread4 = null;
        private void BtnStart4_Click(object sender, RoutedEventArgs e)
        {
            IsStopped4 = false;
            if (thread4 is null)
            {
                thread4 = new Thread(IncrementProgress4);
                thread4.Start();
                btnStart4.IsEnabled = false;
            }
        }

        private void BtnStop4_Click(object sender, RoutedEventArgs e)
        {
            StopHandle();
        }

        private void StopHandle()
        {
            MessageBox.Show("HERE");
            IsStopped4 = true;
            thread4 = null;
            btnStart4.IsEnabled = true;
            if (progressBar4.Value == 100)
            {
                progressBar4.Value = 0;
            }
        }

        private void IncrementProgress4()
        {
            for (int i = 0; i <= 10 && !IsStopped4; i++)
            {
                Dispatcher.Invoke(() => progressBar4.Value = i * 10);
                Thread.Sleep(300);
            }
            if (!IsStopped4)  // проверка, чтобы два раза не вызывать StopHandle() (если было нажато Stop)
                Dispatcher.Invoke(StopHandle);
        }
        #endregion

        #region BTN5 Передача данных в поток + современная остановка потоков с использованием токенов
        private Thread? thread5;
        CancellationTokenSource cts = null!;  // источник токенов отмены (современный подход)
        private void BtnStart5_Click(object sender, RoutedEventArgs e)
        {
            int workTime = Convert.ToInt32(workTimeTextBox.Text);
            thread5 = new Thread(IncrementProgress5);
            cts = new CancellationTokenSource();  // новый источник
            thread5.Start(new ThreadData5()  // объект для потока передаётся в параметр Start()
            {
                WorkTime = workTime,
                CancellToken = cts.Token  // передаём токен
            });
        }

        private void BtnStop5_Click(object sender, RoutedEventArgs e)
        {
            // отмена потоков выполняется через источник токенов
            cts?.Cancel();
            // после этой команды все токены этого источника переходят в отменённое состояние,
            // но на потоки это не влияет, проверку состояния токенов должно выполняться отдельно
            // в каждом потоке в тех местах, у которых возможная отмена потока
        }

        private void IncrementProgress5(object? parameter)
        {
            // аргументом может быть любой объект, но параметр принимает его как object.
            // для передачи нескольких аргументов, их объединяют в один класс/объект
            if (parameter is ThreadData5 data)
            {
                for (int i = 0; i <= 10; i++)
                {
                    Dispatcher.Invoke(() => progressBar5.Value = i * 10);
                    Thread.Sleep(100 * data.WorkTime);  // использование аргумента

                    // проверка токена на отмену - часть работы потока (отмена не влияет на поток, если мы это будем игнорировать)
                    // 1) способ
                    if (data.CancellToken.IsCancellationRequested) break;
                    // 2 способ, с помощью исключения
                    //data.CancellToken.ThrowIfCancellationRequested();
                }
            }
            else
            {
                MessageBox.Show("Thread5 начал работу с неверным аргументом");
            }
        }

        // пользовательский тип для передачи данных в параметр потоку
        class ThreadData5
        {
            public int WorkTime { get; set; }
            public CancellationToken CancellToken { get; set; }
            // токен, созданный источником (CTS), передаётся вместе с данными в поток
        }

        #endregion

        #region HW: Реализовать схему управления несколькими потоками
        /*
             Одна кнопка одновременно запускает несколько потоков, каждый из которых увеличивает свой индикатор прогресса.
         HW: Время работы каждого передается из основного потока(и желательно разный для разных потоков)
             Кнопка Стоп останавливает все потоки.
        */

        private Thread? hwThread1, hwThread2, hwThread3;
        private CancellationTokenSource? hwCts = null;
        private Random r = new Random();

        private void BtnHwStart_Click(object sender, RoutedEventArgs e)
        {
            if (hwCts is not null)
            {
                hwCts.Cancel();
            }

            int workTime1 = r.Next(1, 10);
            int workTime2 = r.Next(1, 10);
            int workTime3 = r.Next(1, 10);

            hwTextBox1.Text = workTime1.ToString();
            hwTextBox2.Text = workTime2.ToString();
            hwTextBox3.Text = workTime3.ToString();

            hwCts = new CancellationTokenSource();
            hwThread1 = new Thread(IncrementHWProgressBar1);
            hwThread2 = new Thread(IncrementHWProgressBar1);
            hwThread3 = new Thread(IncrementHWProgressBar1);

            hwThread1.Start(new ThreadProgress
            {
                WorkTime = workTime1,
                ProgressBar = hwProgressBar1,
                CancellToken = hwCts.Token
            });
            hwThread2.Start(new ThreadProgress
            {
                WorkTime = workTime2,
                ProgressBar = hwProgressBar2,
                CancellToken = hwCts.Token
            });
            hwThread3.Start(new ThreadProgress
            {
                WorkTime = workTime3,
                ProgressBar = hwProgressBar3,
                CancellToken = hwCts.Token
            });
        }

        private void BtnHwStop_Click(object sender, RoutedEventArgs e)
        {
            hwCts?.Cancel();
        }

        private void IncrementHWProgressBar1(object? parameter)
        {
            if (parameter is ThreadProgress threadProgress)
            {
                try
                {
                    for (int i = 0; i <= 10; i++)
                    {
                        Dispatcher.Invoke(() => threadProgress.ProgressBar.Value = i * 10);
                        Thread.Sleep(100 * threadProgress.WorkTime);
                        threadProgress.CancellToken.ThrowIfCancellationRequested();
                    }
                }
                catch (OperationCanceledException) {}  // работа потоков отменена
            }
        }

        class ThreadProgress
        {
            public int WorkTime { get; set; }
            public ProgressBar ProgressBar { get; set; } = null!;
            public CancellationToken CancellToken { get; set; }
        }
        #endregion
    }
}
