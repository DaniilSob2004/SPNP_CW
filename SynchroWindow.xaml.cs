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
    public partial class SynchroWindow : Window
    {
        private const int Months = 12;
        private static Random r = new Random();
        public double sum;
        private int threadCount;  // кол-во активных потоков

        public SynchroWindow()
        {
            InitializeComponent();
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            sum = 100;
            logTextBlock.Text = String.Empty;
            threadCount = Months;
            float randPercent, avgPercent = 0;
            for (int i = 0; i < threadCount; i++)
            {
                randPercent = (float)Math.Round(r.NextDouble() * 20, 1);  // генерация процента от 0 до 20
                avgPercent += randPercent;
                new Thread(AddPercentHW).Start(new MonthData { Month = i + 1, Percent = randPercent });
            }
            logTextBlock.Text += $"Avg percent: {avgPercent / Months}\n";  // выводим средний процент за 12 месяцев
        }

        private void AddPercent1()
        {
            // метод, который имитирует обращение к сети с получением данных про инфляцию за месяц и добавляет её до общей суммы
            double localSum = sum;
            Thread.Sleep(200);  // ~запрос
            localSum *= 1.1;  // 10%
            sum = localSum;
            Dispatcher.Invoke(() =>
            {
                logTextBlock.Text += $"{sum}\n";
            });

            // Проблема: все потоки выводят отдно и то же число - 110
            // Задержка выделяет проблему гарантируя, что все потоки начинаются со 100
            // Это илюстрирует общую проблему асинхронных задач - при работе с общим ресурсом, необходима синхронизация.
        }

        private void AddPercent2()
        {
            // метод, который имитирует обращение к сети с получением данных про инфляцию за месяц и добавляет её до общей суммы
            Thread.Sleep(200);  // ~запрос
            double localSum = sum;
            localSum *= 1.1;  // 10%
            sum = localSum;
            Dispatcher.Invoke(() =>
            {
                logTextBlock.Text += $"{sum}\n";
            });

            // Перенос операции, уменьшает эффект, но не избавляется от него.
            // Числа выводятся разные, но с дублированием, вместо пошагового увеличения
        }


        private object sumLocker = new();  // объект сихронизации
        private void AddPercent3()
        {
            lock (sumLocker)  // блок синхронизации (lock), переводит sunLocker в "закрытое" состояние
            {
                // метод, который имитирует обращение к сети с получением данных про инфляцию за месяц и добавляет её до общей суммы
                double localSum = sum;
                Thread.Sleep(200);  // ~запрос
                localSum *= 1.1;  // 10%
                sum = localSum;
                Dispatcher.Invoke(() =>
                {
                    logTextBlock.Text += $"{sum}\n";
                });
            }  // завершение блока "открывает" sunLocker

            // пока sunLocker "закрытый", другие инструкции с блоком lock не начинают работу,
            // ожидая на "открытия" объекта синхронизации (sumLocker)

            // но вмещение всего тела метода у синхроблок производит к полной сериализации работы
            // - теряется асинхронность
        }

        private void AddPercent4()
        {
            // метод, который имитирует обращение к сети с получением данных про инфляцию за месяц и добавляет её до общей суммы
            Thread.Sleep(200);  // ~запрос

            lock (sumLocker)  // транзакция - смена общего ресурса
            {
                sum *= 1.1;
            }

            Dispatcher.Invoke(() =>  // вывод - за транзакцией, поэтому результаты непредсказуемые,
            {                        // но последний результат будет правильный
                logTextBlock.Text += $"{sum}\n";
            });
        }

        private void AddPercent5()
        {
            // метод, который имитирует обращение к сети с получением данных про инфляцию за месяц и добавляет её до общей суммы
            Thread.Sleep(200);  // ~запрос

            double localSum;
            lock (sumLocker)  // транзакция - смена общего ресурса
            {
                localSum = sum *= 1.1;  // копия вычисленной суммы в локальную переменную
            }

            Dispatcher.Invoke(() =>  // вывод - за транзакцией, но с локальной переменной,
            {                        // которая не разделяется с другими потоками
                logTextBlock.Text += $"{localSum}\n";  // порядок этих операций тоже свободный,
                                                       // но все будут выведены
            });
        }

        private void AddPercent6(object? data)
        {
            // метод, который имитирует обращение к сети с получением данных про инфляцию за месяц и добавляет её до общей суммы
            // и выводит месяц(который посчитался уже), с помощью переданного параметра

            var monthData = data as MonthData;  // преобразование

            Thread.Sleep(200);  // ~запрос

            double localSum;
            lock (sumLocker)  // транзакция - смена общего ресурса
            {
                localSum = sum *= 1.1;  // копия вычисленной суммы в локальную переменную
            }

            Dispatcher.Invoke(() =>  // вывод - за транзакцией, но с локальной переменной,
            {                        // которая не разделяется с другими потоками
                logTextBlock.Text += $"{monthData?.Month}) {localSum}\n";  // порядок этих операций тоже свободный,
                                                                          // но все будут выведены
            });
        }

        private void AddPercent7(object? data)
        {
            // метод, который имитирует обращение к сети с получением данных про инфляцию за месяц и добавляет её до общей суммы
            // и выводит месяц(который посчитался уже), с помощью переданного параметра

            var monthData = data as MonthData;  // преобразование

            Thread.Sleep(200);  // ~запрос

            double localSum;
            lock (sumLocker)  // транзакция - смена общего ресурса
            {
                localSum = sum *= 1.1;  // копия вычисленной суммы в локальную переменную
            }

            Dispatcher.Invoke(() =>  // вывод - за транзакцией, но с локальной переменной,
            {                        // которая не разделяется с другими потоками
                logTextBlock.Text += $"{monthData?.Month}) {localSum}\n";  // порядок этих операций тоже свободный,
                                                                           // но все будут выведены
            });

            // из-за того, что порядок не гарантируется, номер месяца не годится для определения
            // последнего потока, используем уменьшение счётчика потоков
            threadCount--;
            Thread.Sleep(1);  // если добавить тут паузу, то все потоки попадут под это условие, и каждый поток выведет итоговую запись
            if (threadCount == 0)
            {
                // добавляем итоговую запись
                Dispatcher.Invoke(() =>
                {
                    logTextBlock.Text += $"------------------\nresult = {sum}\n";
                });
            }
        }

        // ДЗ
        private object mainLocker = new();  // объект сихронизации
        private void AddPercentHW(object? data)
        {
            var months = data as MonthData;  // преобразовываем
            Thread.Sleep(200);  // запрос

            double localSum;
            lock (mainLocker)  // блок синхронизации для использ. общего ресурса (поля sum)
            {
                localSum = sum += (sum * months!.Percent / 100);  // считаем процент и прибавляем
            }
            Dispatcher.Invoke(() =>
            {
                logTextBlock.Text += $"{months?.Month}) {localSum} (+{months?.Percent}%)\n";
            });

            bool isLast = false;  // флаг для обозначения вывода результата
            lock (mainLocker)  // ещё блок синхронизации для использ. общего ресурса (поля threadCount)
            {
                threadCount--;
                Thread.Sleep(1);
                if (threadCount == 0)  // если это последний поток, устанавливаем флаг
                {
                    isLast = true;
                }
            }
            if (isLast)
            {
                Dispatcher.Invoke(() =>
                {
                    logTextBlock.Text += $"------------------\nresult = {sum}\n";  // вывод результата
                });
            }
        }

        class MonthData
        {
            public int Month { get; set; }
            public float Percent { get; set; }
        }
    }
}
