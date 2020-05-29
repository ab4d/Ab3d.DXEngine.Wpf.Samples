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
using SharpDX.Direct3D11;

namespace Ab3d.DirectX.Client.Diagnostics
{
    /// <summary>
    /// Interaction logic for RenderingFilterWindow.xaml
    /// </summary>
    public partial class RenderingFilterWindow : Window
    {
        public readonly DXScene CurrentDXScene;

        public event EventHandler ValueChanged;

        private List<RenderingQueue> _allRenderingQueues;
        private List<RenderingQueue> _disabledRenderingQueues;


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
            if (_allRenderingQueues == null)
            {
                _allRenderingQueues = CurrentDXScene.RenderingQueues.ToList();
                _disabledRenderingQueues = new List<RenderingQueue>();
            }


            RenderingQueuesStackPanel.Children.Clear();

            foreach (var renderingQueue in _allRenderingQueues)
            {
                string renderingQueueTitle = renderingQueue.Name;

                if (renderingQueue.Count == 1)
                    renderingQueueTitle += " (1 item)";
                else if (renderingQueue.Count > 1)
                    renderingQueueTitle += string.Format(" ({0} items)", renderingQueue.Count);


                var checkBox = new CheckBox()
                {
                    Content   = renderingQueueTitle,
                    IsChecked = !_disabledRenderingQueues.Contains(renderingQueue),
                    Margin    = new Thickness(0, 3, 0, 0)
                };

                checkBox.Checked += delegate(object sender, RoutedEventArgs args)
                {
                    _disabledRenderingQueues.Remove(renderingQueue);

                    RecreateRenderingQueuesOnDXScene();
                    CreateRenderingQueuesEditor();
                    OnValueChanged();
                };
                
                checkBox.Unchecked += delegate(object sender, RoutedEventArgs args)
                {
                    _disabledRenderingQueues.Add(renderingQueue);

                    RecreateRenderingQueuesOnDXScene();
                    CreateRenderingQueuesEditor();
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

        private void RecreateRenderingQueuesOnDXScene()
        {
            // First remove all RenderingQueues
            var allRenderingQueues = CurrentDXScene.RenderingQueues.ToList();

            foreach (var oneRenderingQueue in allRenderingQueues)
                CurrentDXScene.RemoveRenderingQueue(oneRenderingQueue);

            // Now add enabled RenderingQueues
            RenderingQueue previousRenderingQueue = null;
            foreach (var oneRenderingQueue in _allRenderingQueues)
            {
                if (!_disabledRenderingQueues.Contains(oneRenderingQueue))
                {
                    CurrentDXScene.AddRenderingQueueAfter(oneRenderingQueue, previousRenderingQueue);
                    previousRenderingQueue = oneRenderingQueue;
                }
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
            _allRenderingQueues = null;

            CreateRenderingQueuesEditor();
            CreateRenderingStepsEditor();
        }
    }
}
