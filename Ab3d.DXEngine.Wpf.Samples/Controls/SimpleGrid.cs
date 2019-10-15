// ----------------------------------------------------------------
// <copyright file="SimpleGrid.cs" company="AB4D d.o.o.">
//     Copyright (c) AB4D d.o.o.  All Rights Reserved
// </copyright>
// -----------------------------------------------------------------

// License note:
// You may use this control free of charge and for any project you wish. Just do not blame me for any problems with the control.

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
using System.Windows.Shapes;

namespace Ab3d.DXEngine.Wpf.Samples.Controls
{
    // NOTE: This code is not fully tested yet

    // Sample usage:
    //<win:SimpleGrid RowsCount="0"                        - default value - RowsCount == 0 - means that rows are automatically added
    //                ColumnsCount="3" 
    //                IsSecondColumnAutoWidth="False"      - second columns width is set to "*"
    //                ColumnSpacing="20" RowSpacing="10"   - additional columns and rows are added to show additional space
    //                IsCenteredVerticalAlignment="True">  - Set VerticalAlignment = "Center" to all children
    //    <TextBlock>111111</TextBlock>
    //    <TextBlock>2</TextBlock>
    //    <TextBlock>3</TextBlock>
    //    <TextBox Text="12345"></TextBox>
    //    <TextBlock>4444444</TextBlock>
    //    <TextBlock>55</TextBlock>
    //    <TextBlock Grid.Column="1">66</TextBlock>          <!-- Set fixed column; additional children will be positioned on the cells after that -->
    //    <TextBlock>77</TextBlock>
    //</win:SimpleGrid>

    // TODO: Consider create a proper layout management instead of hacking Grid - http://www.codeproject.com/Articles/238307/A-Two-Column-Grid-for-WPF
    public class SimpleGrid : Grid
    {
        public static readonly DependencyProperty ColumnsCountProperty = DependencyProperty.Register("ColumnsCount", typeof(int), typeof(SimpleGrid),
            new FrameworkPropertyMetadata((int)2, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public int ColumnsCount
        {
            get
            {
                return (int)base.GetValue(ColumnsCountProperty);
            }
            set
            {
                base.SetValue(ColumnsCountProperty, value);
            }
        }


        public static readonly DependencyProperty RowsCountProperty = DependencyProperty.Register("RowsCount", typeof(int), typeof(SimpleGrid),
            new FrameworkPropertyMetadata((int)0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public int RowsCount
        {
            get
            {
                return (int)base.GetValue(RowsCountProperty);
            }
            set
            {
                base.SetValue(RowsCountProperty, value);
            }
        }


        public static readonly DependencyProperty ColumnSpacingProperty = DependencyProperty.Register("ColumnSpacing", typeof(double), typeof(SimpleGrid),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double ColumnSpacing
        {
            get
            {
                return (double)base.GetValue(ColumnSpacingProperty);
            }
            set
            {
                base.SetValue(ColumnSpacingProperty, value);
            }
        }

        public static readonly DependencyProperty RowSpacingProperty = DependencyProperty.Register("RowSpacing", typeof(double), typeof(SimpleGrid),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public double RowSpacing
        {
            get
            {
                return (double)base.GetValue(RowSpacingProperty);
            }
            set
            {
                base.SetValue(RowSpacingProperty, value);
            }
        }


        public static readonly DependencyProperty IsFirstColumnAutoWidthProperty = DependencyProperty.Register("IsFirstColumnAutoWidth", typeof(bool), typeof(SimpleGrid),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public bool IsFirstColumnAutoWidth
        {
            get
            {
                return (bool)base.GetValue(IsFirstColumnAutoWidthProperty);
            }
            set
            {
                base.SetValue(IsFirstColumnAutoWidthProperty, value);
            }
        }

        public static readonly DependencyProperty IsSecondColumnAutoWidthProperty = DependencyProperty.Register("IsSecondColumnAutoWidth", typeof(bool), typeof(SimpleGrid),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public bool IsSecondColumnAutoWidth
        {
            get
            {
                return (bool)base.GetValue(IsSecondColumnAutoWidthProperty);
            }
            set
            {
                base.SetValue(IsSecondColumnAutoWidthProperty, value);
            }
        }

        public static readonly DependencyProperty IsThirdColumnAutoWidthProperty = DependencyProperty.Register("IsThirdColumnAutoWidth", typeof(bool), typeof(SimpleGrid),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public bool IsThirdColumnAutoWidth
        {
            get
            {
                return (bool)base.GetValue(IsThirdColumnAutoWidthProperty);
            }
            set
            {
                base.SetValue(IsThirdColumnAutoWidthProperty, value);
            }
        }

        public static readonly DependencyProperty IsCenteredVerticalAlignmentProperty = DependencyProperty.Register("IsCenteredVerticalAlignment", typeof(bool), typeof(SimpleGrid),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public bool IsCenteredVerticalAlignment
        {
            get
            {
                return (bool)base.GetValue(IsCenteredVerticalAlignmentProperty);
            }
            set
            {
                base.SetValue(IsCenteredVerticalAlignmentProperty, value);
            }
        }

        // Used to store original Column Index
        public static readonly DependencyProperty OriginalColumnIndexProperty = DependencyProperty.Register("OriginalColumnIndex", typeof(int), typeof(SimpleGrid));

        // Used to store original row Index
        public static readonly DependencyProperty OriginalRowIndexProperty = DependencyProperty.Register("OriginalRowIndex", typeof(int), typeof(SimpleGrid));



        public SimpleGrid()
        {
        }

        protected override Size MeasureOverride(Size constraint)
        {
            CreateLayout();

            return base.MeasureOverride(constraint);
        }

        private void CreateLayout()
        {
            int columnsCount = ColumnsCount; // Local accessor
            int rowsCount = RowsCount;
            double columnSpacing = ColumnSpacing;
            double rowSpacing = RowSpacing;
            bool isCenteredVerticalAlignment = IsCenteredVerticalAlignment;

            int maxColumnsCount;

            bool isLayoutUpdate;

            if (this.ColumnDefinitions.Count > 0)
            {
                this.ColumnDefinitions.Clear();
                this.RowDefinitions.Clear();

                isLayoutUpdate = true;
            }
            else
            {
                isLayoutUpdate = false; // This is the first layout creation - the Grid.GetColumn and Grid.GetRow values are correct
            }

            for (int i = 0; i < columnsCount; i++)
            {
                var columnDefinition = new ColumnDefinition();

                if ((i == 0 && IsFirstColumnAutoWidth) ||
                    (i == 1 && IsSecondColumnAutoWidth) ||
                    (i == 2 && IsThirdColumnAutoWidth) ||
                     i > 2)
                {
                    columnDefinition.Width = new GridLength(1, GridUnitType.Auto);
                }
                else
                {
                    columnDefinition.Width = new GridLength(1, GridUnitType.Star);
                }

                this.ColumnDefinitions.Add(columnDefinition);

                if (columnSpacing > 0 && i < columnsCount - 1)
                {
                    // Create space with adding empty columns
                    columnDefinition = new ColumnDefinition();
                    columnDefinition.Width = new GridLength(columnSpacing, GridUnitType.Pixel);
                    this.ColumnDefinitions.Add(columnDefinition);
                }
            }

            maxColumnsCount = this.ColumnDefinitions.Count;


            if (rowsCount > 0) // If rows are not added automatically
            {
                for (int i = 0; i < rowsCount; i++)
                {
                    var rowDefinition = new RowDefinition();
                    rowDefinition.Height = new GridLength(1, GridUnitType.Auto);

                    this.RowDefinitions.Add(rowDefinition);

                    if (rowSpacing > 0)
                    {
                        rowDefinition = new RowDefinition();
                        rowDefinition.Height = new GridLength(rowSpacing, GridUnitType.Pixel);

                        this.RowDefinitions.Add(rowDefinition);
                    }
                }
            }


            int columnIndex = 0;
            int rowIndex = 0;

            foreach (UIElement child in this.Children)
            {
                int fixedColumnIndex;
                int fixedRowIndex;

                if (!isLayoutUpdate)
                {
                    fixedColumnIndex = Grid.GetColumn(child);

                    if (fixedColumnIndex != 0)
                        child.SetValue(OriginalColumnIndexProperty, fixedColumnIndex);


                    fixedRowIndex = Grid.GetRow(child);

                    if (fixedRowIndex != 0)
                        child.SetValue(OriginalRowIndexProperty, fixedColumnIndex);
                }
                else
                {
                    //object fixedColumnIndexObject = child.GetValue(OriginalColumnIndexProperty);

                    //if (fixedColumnIndexObject != null)
                    //    fixedColumnIndex = (int) fixedColumnIndexObject;
                    //else
                    //    fixedColumnIndex = 0;

                    //object fixedRowIndexObject = child.GetValue(OriginalRowIndexProperty);

                    //if (fixedRowIndexObject != null)
                    //    fixedRowIndex = (int) fixedRowIndexObject;
                    //else
                    //    fixedRowIndex = 0;
                    
                    fixedColumnIndex = (int)child.GetValue(OriginalColumnIndexProperty);
                    fixedRowIndex = (int)child.GetValue(OriginalRowIndexProperty);
                }

                if (fixedColumnIndex != 0)
                {
                    columnIndex = fixedColumnIndex;

                    if (columnSpacing > 0)
                        columnIndex *= 2;
                }
                    
                Grid.SetColumn(child, columnIndex);


                if (fixedRowIndex != 0)
                    rowIndex = fixedRowIndex;
                else
                    Grid.SetRow(child, rowIndex);


                var childElement = child as FrameworkElement;
                if (childElement != null)
                {
                    if (isCenteredVerticalAlignment)
                        childElement.VerticalAlignment = VerticalAlignment.Center;
                }

                if (rowsCount == 0 && rowIndex >= this.RowDefinitions.Count) // We automatically create rows
                {
                    if (rowSpacing > 0 && rowIndex > 0)
                    {
                        var spacerRowDefinition = new RowDefinition();
                        spacerRowDefinition.Height = new GridLength(rowSpacing, GridUnitType.Pixel);

                        this.RowDefinitions.Add(spacerRowDefinition);
                    }

                    var rowDefinition = new RowDefinition();
                    rowDefinition.Height = new GridLength(1, GridUnitType.Auto);

                    this.RowDefinitions.Add(rowDefinition);
                }


                columnIndex++;

                if (columnSpacing > 0)
                    columnIndex++;

                if (columnIndex >= maxColumnsCount)
                {
                    columnIndex = 0;
                    rowIndex ++;

                    if (rowSpacing > 0)
                        rowIndex++;
                }
            }
        }
    }
}
