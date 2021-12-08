using System.Windows;
using System.Windows.Controls;

namespace APAS.Plugin.IAI.RCP.Views
{
    public partial class PluginDemoView : UserControl
    {
        public PluginDemoView()
        {
            InitializeComponent();

            // once the datacontext is set, register the corresponding event to blink the indicator.
            DataContextChanged += PluginDemoView_DataContextChanged;
        }

        private void PluginDemoView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is PluginDemo plugin)
            {
                plugin.OnCommShot += (s, arg) =>
                {
                   //blinkIndicator.Blink();
                };
            }
        }
    }
}
