// ----------------------------------------------------------------
// <copyright file="FindControlsHelper.cs" company="AB4D d.o.o.">
//     Copyright (c) AB4D d.o.o.  All Rights Reserved
// </copyright>
// -----------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ab3d.Common
{
    internal class FindControlsHelper
    {
        /// <summary>
        /// Tries to find the Ab3d.Cameras.BaseCamera in the logical tree. This method is called when the TargetCamera or TargetCameraName is not set.
        /// </summary>
        /// <returns>Ab3d.Cameras.BaseCamera if found else null</returns>
        public static T FindFirstElement<T>(FrameworkElement startSearchElement)
            where T : DependencyObject
        {
            FrameworkElement root;

            T foundElement = null;

            try
            {
                if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(startSearchElement))
                {
                    // In design time find the child of DesignTimeWindow control
                    root = GetDesignTimeWindowChild(startSearchElement);
                }
                else
                {
                    // First get the root object Window, Page or first parent UserControl
                    // If this element is inside UserControl, the Camera is most probably also inside the same UserControl
                    root = GetRootControl(startSearchElement);
                }

                if (root != null)
                    foundElement = FindElement<T>(root);

                if (foundElement == null && root is UserControl)
                {
                    // Still did not find a Camera inside the UserControl - increase the search to the whole Page / Window
                    root = GetWindowOrPage(root);

                    // Now go through the children and search for ZoomPanel
                    if (root != null)
                        foundElement = FindElement<T>(root);
                }
            }
            catch
            {
                // just in case something goes wrong
                foundElement = null;
            }

            return foundElement;
        }

        /// <summary>
        /// Find Ab3d.Cameras.BaseCamera in element (going through its Content and Children)
        /// </summary>
        /// <param name="element">start element</param>
        /// <returns>Ab3d.Cameras.BaseCamera if found else null</returns>
        private static T FindElement<T>(object element)
            where T : DependencyObject
        {
            T foundElement = null;

            if (element is T)
            {
                // found
                //if (element is UIElement)
                //{
                //    if (!(((UIElement)element).IsEnabled))
                //        return null;
                //}

                foundElement = element as T;
            }
            //else
            //{
            //    // Commented because LogicalTreeHelper.GetChildren returns too many children - for example ColumnDefinitions, etc.
            //    foreach (DependencyObject oneChild in LogicalTreeHelper.GetChildren(element))
            //    {
            //        foundZoomPanel = FindCamera(oneChild);

            //        if (foundZoomPanel != null)
            //            break; // Already found
            //    }
            //}
            else if (element is ContentControl)
            {
                // Check the element's Content
                foundElement = FindElement<T>(((ContentControl)element).Content);
            }
            else if (element is Decorator) // for example Border
            {
                // Check the element's Content
                foundElement = FindElement<T>(((Decorator)element).Child);
            }
            else if (element is Page)
            {
                // Page is not ContentControl so handle it specially (note: Window is ContentControl)
                foundElement = FindElement<T>(((Page)element).Content);
            }
            else if (element is Panel)
            {
                Panel panel = element as Panel;

                // Check each child of a Panel
                foreach (UIElement oneChild in panel.Children)
                {
                    foundElement = FindElement<T>(oneChild);

                    if (foundElement != null)
                        break;
                }
            }
#if DXEngine
            else if (typeof(T) == typeof(Viewport3D) && element.GetType().Name == "DXViewportView")
            {
                foundElement = Ab3d.PowerToys.Common.DXEngineHelper.GetViewport3DFromDXViewport3D(element as UIElement) as T;
            }
#endif
            else if (element is Control)
            {
                var control = element as Control;
                if (control.Template != null)
                {
                    int childrenCount = VisualTreeHelper.GetChildrenCount(control);
                    for (int i = 0; i < childrenCount; i++)
                    {
                        var oneChild = VisualTreeHelper.GetChild(control, i);

                        if (oneChild != null)
                        {
                            foundElement = FindElement<T>(oneChild);
                            if (foundElement != null)
                                break;
                        }
                    }
                }
            }

            return foundElement;
        }

        /// <summary>
        /// For Design time only: Iterates through parent controls until root design time control is found (DesignTimeWindow or ArtboardBorder) is found - than returns its child
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        internal static FrameworkElement GetDesignTimeWindowChild(FrameworkElement element)
        {
            FrameworkElement currentElement;
            string parentTypeName = null;

            if (element == null)
                return null;

            currentElement = element;

            while (currentElement != null && currentElement.Parent != null)
            {
                parentTypeName = currentElement.Parent.GetType().Name;

                // In VS2012 the parents hierarchy looks like this (type FullName is displayed):
                // For Page:
                // System.Windows.Controls.Grid (root control defined by us inside root Page)
                // Microsoft.Expression.WpfPlatform.InstanceBuilders.PageInstance
                // Microsoft.Expression.DesignSurface.UserInterface.ArtboardBorder

                // For Window it looks like:
                // System.Windows.Controls.Grid (root control defined by us inside root Window)
                // Microsoft.Expression.WpfPlatform.InstanceBuilders.WindowInstance
                // Microsoft.Expression.DesignSurface.UserInterface.ArtboardBorder

                // For UserControl it looks like:
                // System.Windows.Controls.Grid (root control defined by us inside root UserControl)
                // System.Windows.Controls.UserControl
                // Microsoft.Expression.DesignSurface.UserInterface.ArtboardBorder

                if (parentTypeName == "DesignTimeWindow" /* VS 2010 */ ||
                    parentTypeName == "ArtboardBorder" /* VS 2012 - VS 2015 */)
                {
                    break;
                }

                currentElement = currentElement.Parent as FrameworkElement;
            }

            return currentElement;
        }

        /// <summary>
        /// Gets UserControl, Window or Page
        /// </summary>
        /// <param name="element">the start object</param>
        /// <returns>UserControl, Window or Page</returns>
        internal static FrameworkElement GetUserControlOrWindowOrPage(FrameworkElement element)
        {
            var currentElement = element;

            // Iterate through Parents until we hit UserControl / Window / Page or null
            while (currentElement != null && !(currentElement is UserControl) && !(currentElement is Window) && !(currentElement is Page))
                currentElement = currentElement.Parent as FrameworkElement;

            return currentElement;
        }

        /// <summary>
        /// Gets UserControl, Window or Page or any other top level FrameworkElement
        /// </summary>
        /// <param name="element">the start object</param>
        /// <returns>UserControl, Window or Page</returns>
        internal static FrameworkElement GetRootControl(FrameworkElement element)
        {
            if (element == null)
                return null;


            var currentElement = element.Parent as FrameworkElement;
            if (currentElement == null) // start element has no parent
                return null;

            for (;;)
            {
                var previousElement = currentElement.Parent as FrameworkElement;

                if (previousElement == null || currentElement is UserControl || currentElement is Window || currentElement is Page)
                    break;

                currentElement = previousElement;
            }

            return currentElement;
        }


        /// <summary>
        /// Gets Window or Page
        /// </summary>
        /// <param name="element">the start object</param>
        /// <returns>Window or Page</returns>
        internal static FrameworkElement GetWindowOrPage(FrameworkElement element)
        {
            FrameworkElement currentElement;

            currentElement = element;

            // Iterate through Parents until we hit Window / Page or null
            while (currentElement != null && !(currentElement is Window) && !(currentElement is Page))
                currentElement = currentElement.Parent as FrameworkElement;

            return currentElement;
        }


        /// <summary>
        /// Gets parent element of type T if found else returns null
        /// </summary>
        /// <param name="element">the start FrameworkElement</param>
        /// <returns>parent element of type T if found else returns null</returns>
        internal static T FindParentElement<T>(FrameworkElement element)
            where T : FrameworkElement
        {
            var currentElement = element;

            // Iterate through Parents until we hit UserControl / Window / Page or null
            while (currentElement != null && !(currentElement is T))
                currentElement = currentElement.Parent as FrameworkElement;

            return currentElement as T;
        }


        // Gets the Viewport3D with the nameToFind
        public static T FindElementByName<T>(FrameworkElement startElement, string nameToFind, int upLevels, int downLevels)
            where T : FrameworkElement
        {
            T foundElement;
            bool isDesignTime;
            FrameworkElement currentParent;
            FrameworkElement nextParent;


            isDesignTime = System.ComponentModel.DesignerProperties.GetIsInDesignMode(startElement);

            // In design time the FindName method does not work correctly because there is no NameScope defined (as checked with disassembly and reflector)
            if (!isDesignTime)
            {
                // First try to use FindName
                foundElement = startElement.FindName(nameToFind) as T;

                if (foundElement != null)
                    return foundElement;
            }


            currentParent = startElement;

            // Go for upLevels up through parents - if they exist
            for (int i = 0; i < upLevels; i++)
            {
                nextParent = currentParent.Parent as FrameworkElement;

                if (nextParent == null) // stop - no more parents
                    break;

                currentParent = nextParent;

                // Check if it is found
                if (currentParent is T && currentParent.Name == nameToFind)
                    return (T)currentParent;
            }

            // no parent
            if (currentParent == null || currentParent == startElement) 
                return null;


            if (!isDesignTime)
                foundElement = currentParent.FindName(nameToFind) as T;
            else
                foundElement = null;

            if (foundElement == null)
                foundElement = FindElementByName<T>(currentParent, nameToFind, downLevels);

            return foundElement;
        }

        /// <summary>
        /// Find Ab3d.Cameras.BaseCamera in element (going through its Content and Children)
        /// </summary>
        /// <param name="currentElement">start element</param>
        /// <param name="nameToFind">nameToFind</param>
        /// <param name="downLevels">number of levels down the logical tree to find for the name</param>
        /// <returns>Ab3d.Cameras.BaseCamera if found else null</returns>
        public static T FindElementByName<T>(FrameworkElement currentElement, string nameToFind, int downLevels)
            where T : DependencyObject
        {
            T foundElement = null;

            if (currentElement == null)
                return null;

            if (currentElement is T && currentElement.Name == nameToFind)
            {
                foundElement = currentElement as T;
            }

            // First check if we can go deeper into the tree
            if (foundElement == null && downLevels > 0)
            {
                if (currentElement is ContentControl)
                {
                    // Check the element's Content
                    foundElement = FindElementByName<T>(((ContentControl)currentElement).Content as FrameworkElement, nameToFind, downLevels - 1);
                }
                else if (currentElement is Decorator) // for example Border
                {
                    // Check the element's Content
                    foundElement = FindElementByName<T>(((Decorator)currentElement).Child as FrameworkElement, nameToFind, downLevels - 1);
                }
                else if (currentElement is Page)
                {
                    // Page is not ContentControl so handle it specially (note: Window is ContentControl)
                    foundElement = FindElementByName<T>(((Page)currentElement).Content as FrameworkElement, nameToFind, downLevels - 1);
                }
                else if (currentElement is Panel)
                {
                    Panel panel = currentElement as Panel;

                    // Check each child of a Panel
                    foreach (UIElement oneChild in panel.Children)
                    {
                        foundElement = FindElementByName<T>(oneChild as FrameworkElement, nameToFind, downLevels - 1);

                        if (foundElement != null)
                            break;
                    }
                }
                else if (currentElement is Control)
                {
                    var control = currentElement as Control;
                    if (control.Template != null)
                    {
                        int childrenCount = VisualTreeHelper.GetChildrenCount(control);
                        for (int i = 0; i < childrenCount; i++)
                        {
                            var oneChild = VisualTreeHelper.GetChild(control, i) as FrameworkElement;

                            if (oneChild != null)
                            {
                                foundElement = FindElementByName<T>(oneChild, nameToFind, downLevels - 1);
                                if (foundElement != null)
                                    break;
                            }
                        }
                    }
                }
            }

            return foundElement;
        }
    }
}
