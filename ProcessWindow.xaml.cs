using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace SPNP
{
    public partial class ProcessWindow : Window
    {
        public ObservableCollection<ProcessInfo> Processes { get; set; } = null!;
        private bool isHandled;
        private double totalSecCpuTime, totalMemoryMB;

        public ProcessWindow()
        {
            InitializeComponent();
            Processes = new ObservableCollection<ProcessInfo>();
            isHandled = false;
            this.DataContext = this;
        }


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
    }

    public class ProcessInfo
    {
        public string ProcessName { get; set; } = null!;
        public int Id { get; set; }
        public string TotalProcessorTime { get; set; } = null!;
        public string TotalMemory { get; set; } = null!;
    }
}
