using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

#if SHARPDX
using SharpDX.Direct3D11;
#endif

namespace Ab3d.DirectX.Client.Diagnostics
{
    /// <summary>
    /// Interaction logic for RenderingFilterWindow.xaml
    /// </summary>
    public partial class RenderingFilterWindow : Window
    {
        public readonly DXScene CurrentDXScene;

        public event EventHandler ValueChanged;

        public RenderingFilterWindow(DXScene dxScene)
        {
            if (dxScene == null)
                return;

            CurrentDXScene = dxScene;

            InitializeComponent();

            CreateRenderingQueuesEditor();
            CreateRenderingStepsEditor();
        }
        
        private void CreateRenderingQueuesEditor()
        {
            RenderingQueuesStackPanel.Children.Clear();

            foreach (var renderingQueue in CurrentDXScene.RenderingQueues)
            {
                string renderingQueueTitle = renderingQueue.Name;

                if (renderingQueue.Count == 1)
                    renderingQueueTitle += " (1 item)";
                else if (renderingQueue.Count > 1)
                    renderingQueueTitle += string.Format(" ({0} items)", renderingQueue.Count);


                var checkBox = new CheckBox()
                {
                    Content   = renderingQueueTitle,
                    IsChecked = renderingQueue.IsRenderingEnabled,
                    Margin    = new Thickness(0, 3, 0, 0)
                };

                checkBox.Checked += delegate(object sender, RoutedEventArgs args)
                {
                    renderingQueue.IsRenderingEnabled = true;
                    OnValueChanged();
                };
                
                checkBox.Unchecked += delegate(object sender, RoutedEventArgs args)
                {
                    renderingQueue.IsRenderingEnabled = false;
                    OnValueChanged();
                };

                RenderingQueuesStackPanel.Children.Add(checkBox);
            }
        }

        private void CreateRenderingStepsEditor()
        {
            RenderingStepsStackPanel.Children.Clear();

            foreach (var renderingStep in CurrentDXScene.RenderingSteps)
            {
                var checkBox = new CheckBox()
                {
                    Content   = renderingStep.Name,
                    ToolTip   = renderingStep.Description,
                    IsChecked = renderingStep.IsEnabled,
                    Margin    = new Thickness(0, 3, 0, 0)
                };

                checkBox.Checked += delegate (object sender, RoutedEventArgs args)
                {
                    renderingStep.IsEnabled = true;
                    OnValueChanged();
                };

                checkBox.Unchecked += delegate (object sender, RoutedEventArgs args)
                {
                    renderingStep.IsEnabled = false;
                    OnValueChanged();
                };

                RenderingStepsStackPanel.Children.Add(checkBox);
            }
        }

        protected void OnValueChanged()
        {
            if (ValueChanged != null)
                ValueChanged(this, null);
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
        {
            CreateRenderingQueuesEditor();
            CreateRenderingStepsEditor();
        }
    }
}
