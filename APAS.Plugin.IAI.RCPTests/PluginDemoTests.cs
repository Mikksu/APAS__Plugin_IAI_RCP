using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;

namespace APAS.Plugin.IAI.RCP.Tests
{
    [TestClass()]
    public class PluginDemoTests
    {
        [TestMethod()]
        public void ControlTest()
        {
            var plugin = new PluginDemo(null);
            var win = new Window
            {
                Content = plugin.UserView,
                SizeToContent = SizeToContent.WidthAndHeight
            };
            win.ShowDialog();
        }
    }
}