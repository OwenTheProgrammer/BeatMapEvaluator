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
    public partial class SwingPerSecondGraph : UserControl {

        //Swings Per Second values
        public ChartValues<int> LeftHandSwings { get; set; }
        public ChartValues<int> RightHandSwings { get; set; }

        //The step count per bar sample on the graph
        public int StepRate { get; set; }

        public SwingPerSecondGraph() {
            InitializeComponent();

            //Init update
            LeftHandSwings = new ChartValues<int>();
            RightHandSwings = new ChartValues<int>();
            StepRate = 5;
            DataContext = this;
            spsChart.Update(true);
        }
    }
}
