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
