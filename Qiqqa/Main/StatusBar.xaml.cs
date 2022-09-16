﻿using System;
using System.Collections.Generic;
using System.Deployment.Application;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Qiqqa.Common.BackgroundWorkerDaemonStuff;
using Utilities;
using Qiqqa.ClientVersioning;
using Utilities.GUI;
using Utilities.Misc;
using Utilities.Shutdownable;

namespace Qiqqa.Main
{
    /// <summary>
    /// Interaction logic for StatusBar.xaml
    /// </summary>
    public partial class StatusBar : UserControl
    {
        private Dictionary<string, StatusBarItem> status_bar_items = new Dictionary<string, StatusBarItem>();
        private Dictionary<string, StatusManager.StatusEntry> status_entries_still_to_process = new Dictionary<string, StatusManager.StatusEntry>();
        private bool status_entries_still_to_process_fresh_thread_running = false;
        private object status_entries_still_to_process_lock = new object();

        public StatusBar()
        {
            InitializeComponent();

            this.Loaded += StatusBar_Loaded;
            this.Unloaded += StatusBar_Unloaded;

            string post_version_type = ApplicationDeployment.IsNetworkDeployed ? "o" : "s";
            CmdVersion.Caption = "v." + ClientVersion.CurrentVersion + post_version_type;
            CmdVersion.MinWidth = 0;
            CmdVersion.Click += CmdVersion_Click;

            ObjScrollViewer.MouseMove += ObjScrollViewer_MouseMove;

            CmdVersion.ToolTip = "This shows the version of Qiqqa you are using.\nClick here to show the release notes for historical versions and for instructions on how to get an older version of Qiqqa.";
        }

        private void StatusBar_Unloaded(object sender, RoutedEventArgs e)
        {
            StatusManager.Instance.OnStatusEntryUpdate -= StatusManager_OnStatusEntryUpdate;
        }

        private void StatusBar_Loaded(object sender, RoutedEventArgs e)
        {
            StatusManager.Instance.OnStatusEntryUpdate += StatusManager_OnStatusEntryUpdate;
            StatusManager.Instance.UpdateStatus("AppStart", "Launching Qiqqa...");
        }

        private void CmdVersion_Click(object sender, RoutedEventArgs e)
        {

            e.Handled = true;
        }

        private void ObjScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (0 == ObjScrollViewer.ScrollableWidth)
            {
                ObjScrollViewer.ScrollToHorizontalOffset(0);
            }
            else
            {
                double offset = ObjScrollViewer.ScrollableWidth * e.GetPosition(ObjScrollViewer).X / ObjScrollViewer.ActualWidth;
                ObjScrollViewer.ScrollToHorizontalOffset(offset);
            }
        }

        private void StatusManager_OnStatusEntryUpdate(StatusManager.StatusEntry status_entry)
        {
            bool do_invoke = false;

            //Utilities.LockPerfTimer l1_clk = Utilities.LockPerfChecker.Start();
            lock (status_entries_still_to_process_lock)
            {
                //l1_clk.LockPerfTimerStop();
                status_entries_still_to_process[status_entry.key] = status_entry;
                if (!status_entries_still_to_process_fresh_thread_running)
                {
                    status_entries_still_to_process_fresh_thread_running = true;
                    do_invoke = true;
                }
            }

            if (do_invoke)
            {
                WPFDoEvents.InvokeAsyncInUIThread(() => StatusManager_OnStatusEntryUpdate_GUI(), DispatcherPriority.Background);
            }
        }

        private void StatusManager_OnStatusEntryUpdate_GUI()
        {
            while (true)
            {
                StatusManager.StatusEntry status_entry = null;
                //Utilities.LockPerfTimer l1_clk = Utilities.LockPerfChecker.Start();
                lock (status_entries_still_to_process_lock)
                {
                    //l1_clk.LockPerfTimerStop();
                    if (status_entries_still_to_process.Count > 0)
                    {
                        var pair = status_entries_still_to_process.First();
                        status_entry = pair.Value;
                        status_entries_still_to_process.Remove(pair.Key);
                    }
                }

                // Is there nothing left to do?
                if (null == status_entry)
                {
                    //Utilities.LockPerfTimer l2_clk = Utilities.LockPerfChecker.Start();
                    lock (status_entries_still_to_process_lock)
                    {
                        //l2_clk.LockPerfTimerStop();
                        status_entries_still_to_process_fresh_thread_running = false;
                        break;
                    }
                }

                // Process this entry
                StatusBarItem status_bar_item;
                if (!status_bar_items.TryGetValue(status_entry.key, out status_bar_item))
                {
                    status_bar_item = new StatusBarItem();
                    status_bar_items[status_entry.key] = status_bar_item;

                    ObjStatusBarContainer.Children.Clear();

                    var status_bar_items_ordered =
                        from status_bar_item_ordered in status_bar_items.Values
                        orderby status_bar_item_ordered.CreationTime ascending
                        select status_bar_item_ordered;

                    foreach (StatusBarItem child in status_bar_items_ordered)
                    {
                        ObjStatusBarContainer.Children.Add(child);
                    }
                }

                status_bar_item.SetStatus(status_entry);

                ExpungeStaleStatusBarItems();
            }
        }

        private void ExpungeStaleStatusBarItems()
        {
            List<KeyValuePair<string, StatusBarItem>> status_bar_items_to_remove = new List<KeyValuePair<string, StatusBarItem>>();
            foreach (var pair in status_bar_items)
            {
                double ms = pair.Value.TimeSinceLastStatusUpdate.ElapsedMilliseconds;
                if (ms >= 3000) // 3 seconds before we remove a 'stale' status item
                {
                    status_bar_items_to_remove.Add(pair);
                }
            }

            foreach (var pair in status_bar_items_to_remove)
            {
                ObjStatusBarContainer.Children.Remove(pair.Value);
                status_bar_items.Remove(pair.Key);
            }
        }
    }
}
