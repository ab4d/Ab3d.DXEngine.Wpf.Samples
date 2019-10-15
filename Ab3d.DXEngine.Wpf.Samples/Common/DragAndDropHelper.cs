// ----------------------------------------------------------------
// <copyright file="FileDropedEventArgs.cs" company="AB4D d.o.o.">
//     Copyright (c) AB4D d.o.o.  All Rights Reserved
// </copyright>
// ----------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Windows;

namespace Ab3d.DXEngine.Wpf.Samples.Common
{
    public class FileDropedEventArgs : EventArgs
    {
        public string FileName;

        public FileDropedEventArgs(string fileName)
        {
            FileName = fileName;
        }
    }

    public class DragAndDropHelper
    {
        private FrameworkElement _parentToAddDragAndDrop;
        private string[] _allowedFileExtensions;

        public event EventHandler<FileDropedEventArgs> FileDroped;

        public DragAndDropHelper(FrameworkElement pageToAddDragAndDrop, string allowedFileExtensions)
        {
            _parentToAddDragAndDrop = pageToAddDragAndDrop;

            if (string.IsNullOrEmpty(allowedFileExtensions) || allowedFileExtensions == "*" || allowedFileExtensions == ".*")
                _allowedFileExtensions = null; // no filter
            else
                _allowedFileExtensions = allowedFileExtensions.Split(';');

            pageToAddDragAndDrop.AllowDrop = true;
            pageToAddDragAndDrop.Drop += new System.Windows.DragEventHandler(pageToAddDragAndDrop_Drop);
            pageToAddDragAndDrop.DragOver += new System.Windows.DragEventHandler(pageToAddDragAndDrop_DragOver);
        }

        public void pageToAddDragAndDrop_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;

            if (e.Data.GetDataPresent("FileNameW"))
            {
                var dropData = e.Data.GetData("FileNameW");

                if (dropData is string[])
                {
                    var dropFileNames = dropData as string[];

                    var fileName = dropFileNames[0].ToString();
                    var fileExtension = System.IO.Path.GetExtension(fileName).ToLower();

                    if (_allowedFileExtensions == null)
                    {
                        e.Effects = DragDropEffects.Move;
                    }
                    else
                    {
                        foreach (string oneFileFilter in _allowedFileExtensions)
                        {
                            if (fileExtension == oneFileFilter)
                            {
                                e.Effects = DragDropEffects.Move;
                                break;
                            }
                        }
                    }
                }
            }
        }

        public void pageToAddDragAndDrop_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("FileNameW"))
            {
                var dropData = e.Data.GetData("FileNameW");

                if (dropData is string[])
                {
                    var dropFileNames = dropData as string[];

                    var fileName = dropFileNames[0].ToString();

                    if (FileDroped != null)
                        FileDroped(this, new FileDropedEventArgs(fileName));
                }
            }
        }
    }
}
