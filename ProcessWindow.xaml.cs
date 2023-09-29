using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Security.Principal;
using System.Threading;

namespace SPNP
{
    public partial class ProcessWindow : Window
    {
        public ObservableCollection<ProcessInfo> Processes { get; set; } = null!;
        private bool isHandled;
        private double totalSecCpuTime, totalMemoryMB;

        private static Mutex? mutex;
        private static string mutexName = "SPNP_PW_MUTEX";

        public ProcessWindow()
        {
            CheckPreviousLunch();

            InitializeComponent();
            Processes = new ObservableCollection<ProcessInfo>();
            isHandled = false;
            this.DataContext = this;
        }

        private void CheckPreviousLunch()
        {
            // Синхронизация между процессами
            // Если работа выполняется в одном процессе, то даже разные потоки могут иметь доступ к общим ресурсам,
            // в том числе ресурсов синхронизации.
            // Когда язык идёт про разные процессы, то единым способом разделить ресурс представляется ОС.
            // Она позволяет регистрировать ресурсы по имени, Мьютекс.
            // Mutex это объект синхронизации, то есть true - закрываем mutex (доступ из других потоков не будет).

            try  // Mutex регистрируется при первом запуске окна
            {    // поэтому он может быть уже существующим в системе
                mutex = Mutex.OpenExisting(mutexName);  // пытаемся открыть
            }
            catch { }

            if (mutex is not null)  // Mutex зарегистрирован в системе - другой процесс его зарегистрировал
            {   // хотя он зарегистрирован, он может быть как открытым, так и закрытым
                if (!mutex.WaitOne(1))  // проверка на закрытость - попытка ждать его небольшое время
                {   // не позже чем 1 мс WaitOne(1) вернёт false, если Mutex не освободился
                    // или успешно переведёт его в закрытое состояние (и вернёт true).
                    // дальнейший код - в случае работы другого окна
                    MessageBox.Show("Запущен другой экземпляр окна");
                    // остановить конструктор можно только исключением
                    throw new ApplicationException();  // не забыть try-catch при создании данного окна
                }
            }
            else  // если Mutex не зарегистрирован, значит это первый запуск окна вообще
            {
                // создаём Mutex, первый параметр - true, закрытое состояние, второй - название
                mutex = new Mutex(true, mutexName);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            mutex?.ReleaseMutex();  // освобождаем mutex - другие процессы смогут открывать свои окна
        }


        #region Работа с процессами
        private void ShowProcesses_Click(object sender, RoutedEventArgs e)
        {
            Process[] processes = Process.GetProcesses();
            treeViewProc.Items.Clear();
            string prevName = "";
            TreeViewItem? item = null;
            foreach (Process process in processes.OrderBy(p => p.ProcessName))
            {
                if (process.ProcessName != prevName)
                {
                    prevName = process.ProcessName;
                    item = new TreeViewItem() { Header = prevName };
                    treeViewProc.Items.Add(item);
                }
                var subItem = new TreeViewItem()
                {
                    Header = String.Format("{0} {1}", process.Id, process.ProcessName),
                    Tag = process
                };
                subItem.MouseDoubleClick += TreeViewItem_MouseDoubleClick;
                item?.Items.Add(subItem);
            }
        }

        private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                string message = "";
                if (item.Tag is Process process)
                {
                    foreach (ProcessThread thread in process.Threads)
                    {
                        message += thread.Id + "\r\n";
                    }
                }
                else
                {
                    message = "No process in tag";
                }
                MessageBox.Show(message);
            }
        }
        #endregion


        #region HW Диспетчер задач (Процессы)
        private void ShowProcessesHW_Click(object sender, RoutedEventArgs e)
        {
            Process[] processes = Process.GetProcesses();  // все процессы

            totalSecCpuTime = GetAllProcessorTime(processes);  // общее кол-во секунд работы процессора во всех процессах
            totalMemoryMB = GetAllMemory(processes);  // общее кол-во памяти во всех процессах
            textBlockAmountProcesses.Text = processes.Length.ToString();  // общее кол-во процессов
            treeViewHWProc.Items.Clear();

            string prevName = "";
            TreeViewItem? item = null;
            List<Process> processesGroup = new List<Process>();  // список, хранит процессы которые совпадают по названию
            foreach (Process process in processes.OrderBy(p => p.ProcessName))  // сортируем по названию
            {
                if (process.ProcessName != prevName)  // если это новое название
                {
                    if (treeViewHWProc.Items.Count != 0)  // если это не первый элемент в дереве
                    {
                        item!.Tag = processesGroup;  // тэгу для "заголовка дерева" присваиваем список процессов
                        processesGroup = new List<Process>();
                    }
                    prevName = process.ProcessName;
                    item = new TreeViewItem() { Header = prevName };
                    treeViewHWProc.Items.Add(item);
                }
                var subItem = new TreeViewItem()
                {
                    Header = String.Format("{0} - {1}", process.Id, process.ProcessName),
                    Tag = process  // тэгу обычному дочерниму элементу дерева присваиваем процесс
                };
                processesGroup.Add(process);
                item!.Items.Add(subItem);
            }
        }

        private void TreeViewItemHW_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                if (!isHandled)  // чтобы не было всплывания
                {
                    Processes.Clear();
                    if (item.Tag is Process process)  // если это "дочерний" элемент дерева
                    {
                        textBlockAmountSameNameProcesses.Text = "0";
                        textBlockAmountCPUTime.Text = GetProcessorTimeInSeconds(process).ToString();
                        textBlockAmountMemory.Text = GetMemoryInMB(process).ToString();

                        AddProcessesInCollection(process);
                        isHandled = true;
                    }
                    else if (item.Tag is List<Process> listProc)  // если это "заголовочный" элемент дерева
                    {
                        textBlockAmountSameNameProcesses.Text = listProc.Count.ToString();
                        textBlockAmountCPUTime.Text = GetAllProcessorTime(listProc.ToArray()).ToString();
                        textBlockAmountMemory.Text = GetAllMemory(listProc.ToArray()).ToString();

                        foreach (Process proc in listProc)
                        {
                            AddProcessesInCollection(proc);
                        }
                    }
                }
                else
                {
                    isHandled = false;
                }
            }
        }

        private void AddProcessesInCollection(Process process)
        {
            Processes.Add(new ProcessInfo
            {
                Id = process.Id,
                ProcessName = process.ProcessName,
                TotalProcessorTime = $"{GetPercentProcessorTime(process)}/{GetProcessorTimeInSeconds(process)}",
                TotalMemory = $"{GetPercentMemory(process)}/{GetMemoryInMB(process)}"
            });
        }

        private double GetAllProcessorTime(Process[] listProc)
        {
            // получаем общее кол-во секунд работы процессора из списка процессов
            double totalSec = 0;
            foreach (Process process in listProc)
            {
                totalSec += GetProcessorTimeInSeconds(process);
            }
            return Math.Round(totalSec, 2);
        }

        private double GetProcessorTimeInSeconds(Process process)
        {
            // получаем кол-во секунд работы процессора в процессе
            double secCpuTime = 0;
            try
            {
                secCpuTime = process.TotalProcessorTime.TotalSeconds;
            }
            catch (Exception) { }
            return Math.Round(secCpuTime, 2);
        }

        private double GetPercentProcessorTime(Process process)
        {
            // получаем процент работы процессора в процессе относительно общего кол-ва работы процессора во всех процессах
            return Math.Round(GetProcessorTimeInSeconds(process) * 100 / totalSecCpuTime, 2);
        }

        private double GetAllMemory(Process[] listProc)
        {
            // получаем общее кол-во памяти из списка процессов
            double totalMemoryMB = 0;
            foreach (Process process in listProc)
            {
                totalMemoryMB += GetMemoryInMB(process);
            }
            return Math.Round(totalMemoryMB, 2);
        }

        private double GetMemoryInMB(Process process)
        {
            // получаем кол-во памяти в процессе
            double totalMemoryMB = 0;
            try
            {
                totalMemoryMB = Convert.ToDouble(process.WorkingSet64 / 1024 / 1024);
            }
            catch (Exception) { }
            return Math.Round(totalMemoryMB, 2);
        }

        private double GetPercentMemory(Process process)
        {
            // получаем кол-во памяти в процессе относительно общего кол-ва памяти во всех процессах
            return Math.Round(GetProcessorTimeInSeconds(process) * 100 / totalMemoryMB, 2);
        }

        #endregion


        #region Создание процессов
        private Process? notepadProcess;

        private void BtnStartNotepad_Click(object sender, RoutedEventArgs e)
        {
            notepadProcess ??= Process.Start("notepad.exe");
        }

        private void BtnStopNotepad_Click(object sender, RoutedEventArgs e)
        {
            notepadProcess?.Kill();  // true с подпроцессами - закрывает все блокноты
            //notepadProcess?.CloseMainWindow();
            //notepadProcess?.WaitForExit();
            notepadProcess?.Dispose();  // это unmanage ресурс, сборщик мусора не может освободить память
            notepadProcess = null;
        }

        private void BtnStartEdit_Click(object sender, RoutedEventArgs e)
        {
            string dir = AppContext.BaseDirectory;  // путь до папки exe файла
            int binPosition = dir.IndexOf("bin");  // выше папки bin - корень проекта
            string projectRoot = dir[..binPosition];  // часть пути к bin
            notepadProcess ??= Process.Start(  // аргументы командной строки передаются вторым параметром
                "notepad.exe",
                $"{projectRoot}ProcessWindow.xaml.cs"
            );
        }

        private Process? browserProcess;
        private void BtnStartBrowser_Click(object sender, RoutedEventArgs e)
        {
            string filename = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
            if (File.Exists(filename))
            {
                browserProcess ??= Process.Start(filename, "-url https://github.com/DaniilSob2004?tab=repositories");
            }
            else
            {
                MessageBox.Show("Путь указан неверно!");
            }
        }

        #endregion


        #region HW Запустить процесс калькулятор, диспетчер задач и параметры системы(настройки)

        private Process? calcProcess;

        private void BtnStartCalc_Click(object sender, RoutedEventArgs e)
        {
            calcProcess ??= Process.Start("calc");
        }

        private void BtnStopCalc_Click(object sender, RoutedEventArgs e)
        {
            Process[] calcProcesses = Process.GetProcessesByName("CalculatorApp");
            foreach (Process calcProcess in calcProcesses)
            {
                calcProcess.Kill();
            }
            calcProcess = null;
        }


        private Process? taskManagerProcess;

        private void BtnStartTaskManager_Click(object sender, RoutedEventArgs e)
        {
            if (taskManagerProcess is null)
            {
                taskManagerProcess = new Process();
                taskManagerProcess.StartInfo.FileName = "taskmgr.exe";
                taskManagerProcess.StartInfo.UseShellExecute = true;  // процесс будет запущен с использ. системной оболочки(shell), т.е. система будет решать, как обрабатывать команду
                // process.StartInfo.Verb = "runas";  // запуск от имени администратора
                try
                {
                    taskManagerProcess.Start();
                }
                catch (Exception)
                {
                    MessageBox.Show("Ошибка при запуске диспетчера задач!");
                }
            }
            else
            {
                MessageBox.Show("Уже запущенно!");
            }
        }

        private void BtnStopTaskManager_Click(object sender, RoutedEventArgs e)
        {
            taskManagerProcess?.CloseMainWindow();
            taskManagerProcess?.Kill();
            taskManagerProcess = null;
        }

        private void BtnStartSystemSettings_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("control.exe", "desk.cpl");
        }

        #endregion
    }

    public class ProcessInfo
    {
        public string ProcessName { get; set; } = null!;
        public int Id { get; set; }
        public string TotalProcessorTime { get; set; } = null!;
        public string TotalMemory { get; set; } = null!;
    }
}
