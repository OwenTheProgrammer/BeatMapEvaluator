using LiveCharts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BeatMapEvaluator.Themes
{
    /// <summary>
    /// Interaction logic for SwingPerSecondGraph.xaml
    /// </summary>
    public partial class SwingPerSecondGraph : UserControl
    {
        public ChartValues<int> spsData { get; set; }
        public int StepRate { get; set; }

        public SwingPerSecondGraph() {
            InitializeComponent();

            spsData = new ChartValues<int>();
            StepRate = 2;
            DataContext = this;
            spsChart.Update(true);
        }
    }
}
