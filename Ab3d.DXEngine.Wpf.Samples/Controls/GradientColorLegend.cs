// ----------------------------------------------------------------
// <copyright file="GradientColorLegend.cs" company="AB4D d.o.o.">
//     Copyright (c) AB4D d.o.o.  All Rights Reserved
// </copyright>
// -----------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Ab3d.DXEngine.Wpf.Samples.Controls
{
    public class GradientColorLegend : Grid
    {
        public class LegendLabel
        {
            private string _displayText;

            /// <summary>
            /// Relative position: 0 = bottom, 1 = top
            /// </summary>
            public double RelativePosition { get; set; }

            public string DisplayText
            {
                get { return _displayText; }
                set
                {
                    _displayText = value;

                    if (LabelTextBlock != null)
                        LabelTextBlock.Text = value;
                }
            }

            public TextBlock LabelTextBlock { get; set; }


            public LegendLabel(double relativePosition, TextBlock labelTextBlock)
            {
                RelativePosition = relativePosition;
                DisplayText = labelTextBlock.Text;
                LabelTextBlock = labelTextBlock;
            }

            public LegendLabel(double relativePosition, string displayText)
            {
                RelativePosition = relativePosition;
                DisplayText = displayText;
                LabelTextBlock = null;
            }

            public void CreateDefaultTextBlock()
            {
                LabelTextBlock = new TextBlock()
                {
                    Text = DisplayText,
                    FontSize = 12,
                    Foreground = Brushes.Black
                };
            }
        }

        private double _labelLineLength = 7; // Length of little horizontal line
        private double _labelLineMargin = 3; // Margin betwen TextBlock and little horizontal line

        private Border _gradientBorder;
        private Canvas _labelsCanvas;
        private LinearGradientBrush _gradientBrush;

        private readonly List<LegendLabel> _legendLabels;

        public List<LegendLabel> LegendLabels
        {
            get { return _legendLabels; }
        }

        public LinearGradientBrush GradientBrush
        {
            get { return _gradientBrush; }
            set
            {
                _gradientBrush = value;
                _gradientBorder.Background = value;
            }
        }


        public GradientColorLegend()
        {
            _legendLabels = new List<LegendLabel>();

            // This control is rendered as 2 column Grid:
            // 1. column's width is automatically scaled to show the labels
            // 2. column occupies the remaining of the size (Total control width - 1. column)
            this.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto)});
            this.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star)});

            _gradientBorder = new Border()
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(2, 2, 2, 2),
                SnapsToDevicePixels = true  // Prevent bluered lines if they are drawn between pixel boundaries
            };

            Grid.SetColumn(_gradientBorder, 1);
            this.Children.Add(_gradientBorder);

            _labelsCanvas = new Canvas();
            Grid.SetColumn(_labelsCanvas, 0);
            this.Children.Add(_labelsCanvas);

            this.Loaded += delegate(object sender, RoutedEventArgs args)
            {
                UpdateLegendLabels();
            };

            this.SizeChanged += delegate(object sender, SizeChangedEventArgs args)
            {
                UpdateLegendLabels();
            };
        }

        public void UpdateLegendLabels()
        {
            _labelsCanvas.Children.Clear();

            if (_legendLabels.Count == 0)
                return;

            double maxTextWidth = 0;

            // In first pass ensure that TextBlocks are created for each LegendLabel
            // we also measure the TextBlocks and get the longest width
            foreach (var legendLabel in _legendLabels)
            {
                if (legendLabel.LabelTextBlock == null)
                    legendLabel.CreateDefaultTextBlock();

                var textBlock = legendLabel.LabelTextBlock;

                // Measure the TextBlock to get its width
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                if (textBlock.DesiredSize.Width > maxTextWidth)
                    maxTextWidth = textBlock.DesiredSize.Width;
            }


            // After we have the maxTextWidth, we can define the size of the Canvas
            // Available size is the height of this control minus thickness of gradient border
            double availableYSize = this.ActualHeight - _gradientBorder.BorderThickness.Bottom - _gradientBorder.BorderThickness.Top;

            _labelsCanvas.Width = maxTextWidth + _labelLineMargin + _labelLineLength; // We add little margin (5)
            _labelsCanvas.Height = availableYSize; 
            

            // Now position the TextBlocks
            foreach (var legendLabel in _legendLabels)
            {
                var relativePosition = legendLabel.RelativePosition;

                if (relativePosition < 0 || relativePosition > 1)  // Is out-of-bounds and not visible
                    continue;


                var textBlock = legendLabel.LabelTextBlock;


                double yPos = (1 - relativePosition) * availableYSize;


                // Create and add little horizontal line
                var line = new Line()
                {
                    X1 = maxTextWidth + _labelLineMargin,
                    Y1 = yPos,
                    X2 = maxTextWidth + _labelLineMargin + _labelLineLength,
                    Y2 = yPos,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    SnapsToDevicePixels = true // Prevent bluered lines if they are drawn between pixel boundaries
                };

                _labelsCanvas.Children.Add(line);


                // Position TextBlock
                double textBlockHeight = textBlock.DesiredSize.Height;

                yPos -= textBlockHeight * 0.5;

                //if (yPos < 0) // adjust position of top label
                //    yPos = 0;

                //if (yPos > availableYSize - textBlockHeight) // adjust position of bottom label
                //    yPos = availableYSize - textBlockHeight;


                double xPos = maxTextWidth - textBlock.DesiredSize.Width; // Right align


                // Set x and y positions inside Canvas
                Canvas.SetLeft(textBlock, xPos);
                Canvas.SetTop(textBlock, yPos);

                // Add to Canvas
                _labelsCanvas.Children.Add(textBlock);
            }
        }
    }
}