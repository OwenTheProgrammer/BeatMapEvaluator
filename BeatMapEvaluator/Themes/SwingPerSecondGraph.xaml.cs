using LiveCharts;
using System.Windows.Controls;

namespace BeatMapEvaluator.Themes
{
    /// <summary>Interaction logic for SwingPerSecondGraph.xaml</summary>
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
