using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace SPNP
{
    public partial class DLLWindow : Window
    {
        public DLLWindow()
        {
            InitializeComponent();
        }

        // работа с DDL выглядит следующим способом: в C# объявляется метод, который будет отвечать за вызов процедуры из DDL
        [DllImport("User32.dll")]
        public
            static  // статическое соединение - защита от GC(сборщика мусора)
            extern  // ссылка на внешний модуль (неуправляемый код)
            int MessageBoxA(  // переписываем заголовок ф-ции из "User32.dll" в C# (используя C# типы)
                IntPtr hWnd,  // IntPtr <-> HWND
                string lpText,  // string <-> LPCSTR
                string lpCaption,  // string <-> LPCSTR
                uint uType  // uint <-> UINT
            );

        private void BtnAlert_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxA(
                IntPtr.Zero,  // NULL ptr
                "Привет Даниил",
                "Приветствие",
                0x40
            );
            MessageBoxA(
                IntPtr.Zero,  // NULL ptr
                "Привет Даниил2",
                "Приветствие2",
                0x31
            );
        }




        // для того чтобы передать (маршализовать) адрес метода в ф-цию, описываем тип-делегат
        public delegate void ThreadMethod();
        // EntryPoint позволяет использовать другое название для метода
        [DllImport("Kernel32.dll", EntryPoint = "CreateThread")]
        public static extern
            IntPtr NewThread(
                IntPtr lpThreadAttributes,
                uint dwStackSize,
                ThreadMethod lpStartAddress,  // вместо IntPtr - делегат
                IntPtr lpParameter,
                uint dwCreationFlags,
                IntPtr lpThreadId
            );
        // ThreadMethod - при маршализации, он будет сам автоматически конвертироваться в IntPtr.
        // Если бы указали IntPtr, то мы бы сами должны были указать адресс метода.

        // описываем сам метод (за сигнатурой делегата)
        public void ErrorMessage()
        {
            MessageBoxA(
                IntPtr.Zero,
                "Привет Даниил2",
                "Приветствие2",
                0x14  // MB_ICONERROR + MB_YESNO
            );
            // после завершение работы, снимаем "фиксатор" - GC сможет управлять объектом
            methodHandle.Free();
        }

        // GC может менять адреса объектов (и их методов), а в DLL передаются сами адреса, а не ссылки,
        // поэтому следует зафиксировать объект, на который будет ссылаться DLL, чтобы не потерять адресс метода
        GCHandle methodHandle;  // "фиксатор"
        private void BtnNewThread_Click(object sender, RoutedEventArgs e)
        {
            // создаём новый объект с методом ErrorMessage
            ThreadMethod method = new(ErrorMessage);

            // фиксируем его в памяти и сохраняем дескриптор(handle)
            methodHandle = GCHandle.Alloc(method);

            // передаём ссылку на этот объект в неуправляемый код
            NewThread(IntPtr.Zero, 0, method, IntPtr.Zero, 0, IntPtr.Zero);
        }




        [DllImport("Kernel32.dll", EntryPoint = "Beep")]
        public static extern bool Sound(uint frequency, uint duration);

        private void BtnSound_Click(object sender, RoutedEventArgs e)
        {
            Sound(440, 300);
        }
    }
}
