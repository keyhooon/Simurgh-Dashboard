using SimurghDashboard.Infrastructures.Native;
using SimurghDashboard.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace SimurghDashboard
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel mainViewModel)
        {
            InitializeComponent();
            this.DataContext = mainViewModel;
        }

        
    }
}
