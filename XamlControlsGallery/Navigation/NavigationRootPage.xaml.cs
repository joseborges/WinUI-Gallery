﻿//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************
using AppUIBasics.Common;
using AppUIBasics.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.Devices.Input;
using Windows.Gaming.Input;
using Windows.System.Profile;
using Windows.UI.ViewManagement;
using Microsoft.UI.Dispatching;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace AppUIBasics
{
    public sealed partial class NavigationRootPage : Page
    {
        public static NavigationRootPage Current;
        public static Frame RootFrame = null;

        public Windows.System.VirtualKey ArrowKey;

        private RootFrameNavigationHelper _navHelper;
        private PageHeader _header;
        private bool _isGamePadConnected;
        private bool _isKeyboardConnected;
        private Microsoft.UI.Xaml.Controls.NavigationViewItem _allControlsMenuItem;
        private Microsoft.UI.Xaml.Controls.NavigationViewItem _newControlsMenuItem;

        public Microsoft.UI.Xaml.Controls.NavigationView NavigationView
        {
            get { return NavigationViewControl; }
        }

        public DeviceType DeviceFamily { get; set; }

        public bool IsFocusSupported
        {
            get
            {
                return DeviceFamily == DeviceType.Xbox || _isGamePadConnected || _isKeyboardConnected;
            }
        }

        public PageHeader PageHeader
        {
            get
            {
                return _header ?? (_header = UIHelper.GetDescendantsOfType<PageHeader>(NavigationViewControl).FirstOrDefault());
            }
        }

        public string AppTitleText
        {
            get
            {
#if !UNIVERSAL
                return "WinUI 3 Controls Gallery";
#else
                return "WinUI 3 Controls Gallery (UWP)";
#endif
            }
        }

        public NavigationRootPage()
        {
            this.InitializeComponent();

#if !UNIVERSAL
            AppTitleBar.Visibility = Visibility.Collapsed;
            App.CurrentWindow.Title = AppTitleText;
#endif

            _navHelper = new RootFrameNavigationHelper(rootFrame, NavigationViewControl);

            SetDeviceFamily();
            AddNavigationMenuItems();
            Current = this;
            RootFrame = rootFrame;

            this.GotFocus += (object sender, RoutedEventArgs e) =>
            {
                // helpful for debugging focus problems w/ keyboard & gamepad
                if (FocusManager.GetFocusedElement() is FrameworkElement focus)
                {
                    Debug.WriteLine("got focus: " + focus.Name + " (" + focus.GetType().ToString() + ")");
                }
            };

            Gamepad.GamepadAdded += OnGamepadAdded;
            Gamepad.GamepadRemoved += OnGamepadRemoved;

#if UNIVERSAL
            CoreApplication.GetCurrentView().TitleBar.LayoutMetricsChanged += (s, e) => UpdateAppTitle(s);
#endif

            _isKeyboardConnected = Convert.ToBoolean(new KeyboardCapabilities().KeyboardPresent);

            // remove the solid-colored backgrounds behind the caption controls and system back button if we are in left mode
            // This is done when the app is loaded since before that the actual theme that is used is not "determined" yet
            Loaded += delegate (object sender, RoutedEventArgs e)
            {
                NavigationOrientationHelper.UpdateTitleBar(NavigationOrientationHelper.IsLeftMode);
            };

            NavigationViewControl.RegisterPropertyChangedCallback(NavigationView.PaneDisplayModeProperty, new DependencyPropertyChangedCallback(OnPaneDisplayModeChanged));
        }

        private void OnPaneDisplayModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var navigationView = sender as NavigationView;
            NavigationRootPage.Current.AppTitleBar.Visibility = navigationView.PaneDisplayMode == NavigationViewPaneDisplayMode.Top ? Visibility.Collapsed : Visibility.Visible;
        }

        void UpdateAppTitle(CoreApplicationViewTitleBar coreTitleBar)
        {
            //ensure the custom title bar does not overlap window caption controls
            Thickness currMargin = AppTitleBar.Margin;
            AppTitleBar.Margin = new Thickness(currMargin.Left, currMargin.Top, coreTitleBar.SystemOverlayRightInset, currMargin.Bottom);
        }

        public bool CheckNewControlSelected()
        {
            return _newControlsMenuItem.IsSelected;
        }

        private void AddNavigationMenuItems()
        {
            foreach (var group in ControlInfoDataSource.Instance.Groups.OrderBy(i => i.Title))
            {
                var item = new Microsoft.UI.Xaml.Controls.NavigationViewItem() { Content = group.Title, Tag = group.UniqueId, DataContext = group };
                AutomationProperties.SetName(item, group.Title);
                if (group.ImagePath.ToLowerInvariant().EndsWith(".png"))
                {
                    item.Icon = new BitmapIcon() { UriSource = new Uri(group.ImagePath, UriKind.RelativeOrAbsolute) };
                }
                else
                {
                    item.Icon = new FontIcon()
                    {
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        Glyph = group.ImagePath
                    };
                }
                NavigationViewControl.MenuItems.Add(item);

                if (group.UniqueId == "AllControls")
                {
                    this._allControlsMenuItem = item;
                }
                else if (group.UniqueId == "NewControls")
                {
                    this._newControlsMenuItem = item;
                }
            }

            // Move "What's New" and "All Controls" to the top of the NavigationView
            NavigationViewControl.MenuItems.Remove(_allControlsMenuItem);
            NavigationViewControl.MenuItems.Remove(_newControlsMenuItem);
            NavigationViewControl.MenuItems.Insert(0, _allControlsMenuItem);
            NavigationViewControl.MenuItems.Insert(0, _newControlsMenuItem);

            // Separate the All/New items from the rest of the categories.
            NavigationViewControl.MenuItems.Insert(2, new Microsoft.UI.Xaml.Controls.NavigationViewItemSeparator());

            _newControlsMenuItem.Loaded += OnNewControlsMenuItemLoaded;
        }

        private void SetDeviceFamily()
        {
            var familyName = AnalyticsInfo.VersionInfo.DeviceFamily;

            if (!Enum.TryParse(familyName.Replace("Windows.", string.Empty), out DeviceType parsedDeviceType))
            {
                parsedDeviceType = DeviceType.Other;
            }

            DeviceFamily = parsedDeviceType;
        }

        private void OnNewControlsMenuItemLoaded(object sender, RoutedEventArgs e)
        {
            if (IsFocusSupported && NavigationViewControl.DisplayMode == Microsoft.UI.Xaml.Controls.NavigationViewDisplayMode.Expanded)
            {
                controlsSearchBox.Focus(FocusState.Keyboard);
            }
        }

        private void OnGamepadRemoved(object sender, Gamepad e)
        {
            _isGamePadConnected = Gamepad.Gamepads.Any();
        }

        private void OnGamepadAdded(object sender, Gamepad e)
        {
            _isGamePadConnected = Gamepad.Gamepads.Any();
        }

        private void OnNavigationViewItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            // Close any open teaching tips before navigation
            CloseTeachingTips();

            if (args.IsSettingsInvoked)
            {
                rootFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                var invokedItem = args.InvokedItemContainer;

                if (invokedItem == _allControlsMenuItem)
                {
                    rootFrame.Navigate(typeof(AllControlsPage));
                }
                else if (invokedItem == _newControlsMenuItem)
                {
                    rootFrame.Navigate(typeof(NewControlsPage));
                }
                else
                {
                    var itemId = ((ControlInfoDataGroup)invokedItem.DataContext).UniqueId;
                    rootFrame.Navigate(typeof(SectionPage), itemId);
                }
            }
        }

        private void OnRootFrameNavigated(object sender, NavigationEventArgs e)
        {
            // Close any open teaching tips before navigation
            CloseTeachingTips();

            if (e.SourcePageType == typeof(AllControlsPage) ||
                e.SourcePageType == typeof(NewControlsPage))
            {
                NavigationViewControl.AlwaysShowHeader = false;
            }
            else
            {
                NavigationViewControl.AlwaysShowHeader = true;

                bool isFilteredPage = e.SourcePageType == typeof(SectionPage) || e.SourcePageType == typeof(SearchResultsPage);
                PageHeader?.UpdateBackground(isFilteredPage);
            }

            TestContentLoadedCheckBox.IsChecked = true;
        }

        private void OnRootFrameNavigating(object sender, NavigatingCancelEventArgs e)
        {
            TestContentLoadedCheckBox.IsChecked = false;
        }

        private void CloseTeachingTips()
        {
            if (Current?.PageHeader != null)
            {
                Current.PageHeader.TeachingTip1.IsOpen = false;
                Current.PageHeader.TeachingTip3.IsOpen = false;
            }
        }

        private void OnControlsSearchBoxTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var suggestions = new List<ControlInfoDataItem>();

                var querySplit = sender.Text.Split(" ");
                foreach (var group in ControlInfoDataSource.Instance.Groups)
                {
                    var matchingItems = group.Items.Where(
                        item =>
                        {
                            // Idea: check for every word entered (separated by space) if it is in the name, 
                            // e.g. for query "split button" the only result should "SplitButton" since its the only query to contain "split" and "button"
                            // If any of the sub tokens is not in the string, we ignore the item. So the search gets more precise with more words
                            bool flag = item.IncludedInBuild;
                            foreach (string queryToken in querySplit)
                            {
                                // Check if token is not in string
                                if (item.Title.IndexOf(queryToken, StringComparison.CurrentCultureIgnoreCase) < 0)
                                {
                                    // Token is not in string, so we ignore this item.
                                    flag = false;
                                }
                            }
                            return flag;
                        });
                    foreach (var item in matchingItems)
                    {
                        suggestions.Add(item);
                    }
                }
                if (suggestions.Count > 0)
                {
                    controlsSearchBox.ItemsSource = suggestions.OrderByDescending(i => i.Title.StartsWith(sender.Text, StringComparison.CurrentCultureIgnoreCase)).ThenBy(i => i.Title);
                }
                else
                {
                    controlsSearchBox.ItemsSource = new string[] { "No results found" };
                }
            }
        }

        private void OnControlsSearchBoxQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion != null && args.ChosenSuggestion is ControlInfoDataItem)
            {
                var itemId = (args.ChosenSuggestion as ControlInfoDataItem).UniqueId;
                NavigationRootPage.RootFrame.Navigate(typeof(ItemPage), itemId);
            }
            else if (!string.IsNullOrEmpty(args.QueryText))
            {
                NavigationRootPage.RootFrame.Navigate(typeof(SearchResultsPage), args.QueryText);
            }
        }

        private void NavigationViewControl_PaneClosing(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewPaneClosingEventArgs args)
        {
            UpdateAppTitleMargin(sender);
        }

        private void NavigationViewControl_PaneOpened(Microsoft.UI.Xaml.Controls.NavigationView sender, object args)
        {
            UpdateAppTitleMargin(sender);
        }

        private void NavigationViewControl_DisplayModeChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewDisplayModeChangedEventArgs args)
        {
            Thickness currMargin = AppTitleBar.Margin;
            if (sender.DisplayMode == Microsoft.UI.Xaml.Controls.NavigationViewDisplayMode.Minimal)
            {
                AppTitleBar.Margin = new Thickness((sender.CompactPaneLength * 2), currMargin.Top, currMargin.Right, currMargin.Bottom);

            }
            else
            {
                AppTitleBar.Margin = new Thickness(sender.CompactPaneLength, currMargin.Top, currMargin.Right, currMargin.Bottom);
            }

            UpdateAppTitleMargin(sender);
        }

        private void UpdateAppTitleMargin(Microsoft.UI.Xaml.Controls.NavigationView sender)
        {
            const int smallLeftIndent = 4, largeLeftIndent = 24;

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7))
            {
                AppTitle.TranslationTransition = new Vector3Transition();

                if (sender.DisplayMode == Microsoft.UI.Xaml.Controls.NavigationViewDisplayMode.Expanded && sender.IsPaneOpen)
                {
                    AppTitle.Translation = new System.Numerics.Vector3(smallLeftIndent, 0, 0);
                }
                else
                {
                    AppTitle.Translation = new System.Numerics.Vector3(largeLeftIndent, 0, 0);
                }
            }
            else
            {
                Thickness currMargin = AppTitle.Margin;

                if (sender.DisplayMode == Microsoft.UI.Xaml.Controls.NavigationViewDisplayMode.Expanded && sender.IsPaneOpen)
                {
                    AppTitle.Margin = new Thickness(smallLeftIndent, currMargin.Top, currMargin.Right, currMargin.Bottom);
                }
                else
                {
                    AppTitle.Margin = new Thickness(largeLeftIndent, currMargin.Top, currMargin.Right, currMargin.Bottom);
                }
            }
        }

        private void CtrlF_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            controlsSearchBox.Focus(FocusState.Programmatic);
        }

        #region Helpers for test automation

        private static string _error = string.Empty;
        private static string _log = string.Empty;

        private async void WaitForIdleInvokerButton_Click(object sender, RoutedEventArgs e)
        {
            _idleStateEnteredCheckBox.IsChecked = false;
            await Windows.System.Threading.ThreadPool.RunAsync(WaitForIdleWorker);

            _logReportingTextBox.Text = _log;

            if (_error.Length == 0)
            {
                _idleStateEnteredCheckBox.IsChecked = true;
            }
            else
            {
                // Setting Text will raise a property-changed event, so even if we
                // immediately set it back to the empty string, we'll still get the
                // error-reported event that we can detect and handle.
                _errorReportingTextBox.Text = _error;
                _errorReportingTextBox.Text = string.Empty;

                _error = string.Empty;
            }
        }

        private static void WaitForIdleWorker(IAsyncAction action)
        {
            _error = IdleSynchronizer.TryWait(out _log);
        }

        private void CloseAppInvokerButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        private void GoBackInvokerButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if(this.rootFrame.CanGoBack)
            {
                this.rootFrame.GoBack();
            }
        }

        private void WaitForDebuggerInvokerButton_Click(object sender, RoutedEventArgs e)
        {
            DebuggerAttachedCheckBox.IsChecked = false;

            var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            var workItem = new Windows.System.Threading.WorkItemHandler((IAsyncAction _) =>
            {
                while (!IsDebuggerPresent())
                {
                    Thread.Sleep(1000);
                }

                DebugBreak();

                dispatcherQueue.TryEnqueue(
                    DispatcherQueuePriority.Low,
                    new DispatcherQueueHandler(() =>
                {
                    DebuggerAttachedCheckBox.IsChecked = true;
                }));
            });

            var asyncAction = Windows.System.Threading.ThreadPool.RunAsync(workItem);
        }

        [DllImport("kernel32.dll")]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll")]
        private static extern void DebugBreak();

        #endregion
    }

    public enum DeviceType
    {
        Desktop,
        Mobile,
        Other,
        Xbox
    }
}
