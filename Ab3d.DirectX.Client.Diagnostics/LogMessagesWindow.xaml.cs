using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ab3d.DirectX.Client.Diagnostics
{
    /// <summary>
    /// Interaction logic for LogMessagesWindow.xaml
    /// </summary>
    public partial class LogMessagesWindow : Window
    {
        private List<Tuple<DXDiagnostics.LogLevels, string>> _logMessages;

        public List<Tuple<DXDiagnostics.LogLevels, string>> LogMessages
        {
            get
            {
                return _logMessages;
            }

            set
            {
                _logMessages = value;
                UpdateLogMessages();
            }
        }

        // Because some messages can be deleted (if there are too many of them)
        // we need a number at which the indexing start
        public int MessageStartIndex { get; set; }


        public LogMessagesWindow()
        {
            InitializeComponent();
        }

        public void UpdateLogMessages()
        {
            if (LogMessages == null)
            {
                InfoTextBox.Text = "";
                return;
            }

            var sb = new StringBuilder();

            int indexOffset = MessageStartIndex;

            for (var i = _logMessages.Count - 1; i >= 0; i--)
                sb.AppendFormat("{0,2}  {1}: {2}\r\n\r\n", i + indexOffset, _logMessages[i].Item1, _logMessages[i].Item2);

            InfoTextBox.Text = sb.ToString();
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ClearButton_OnClick(object sender, RoutedEventArgs e)
        {
            _logMessages.Clear();
            InfoTextBox.Text = "";

            this.Close();
        }
    }
}
