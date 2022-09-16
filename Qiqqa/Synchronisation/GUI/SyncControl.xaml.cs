﻿using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using icons;
using Qiqqa.Common.Configuration;
using Qiqqa.Common.GUI;
using Qiqqa.Synchronisation.BusinessLogic;
using Utilities;
using Utilities.GUI;

namespace Qiqqa.Synchronisation.GUI
{
    /// <summary>
    /// Interaction logic for SyncControl.xaml
    /// </summary>
    public partial class SyncControl : StandardWindow
    {
        public static readonly string TITLE = "Web Library Sync";
        private SyncControlGridItemSet sync_control_grid_item_set;

        public SyncControl()
        {
            Theme.Initialize();

            InitializeComponent();

            DataContext = ConfigurationManager.Instance.ConfigurationRecord_Bindable;

            MouseWheelDisabler.DisableMouseWheelForControl(GridLibraryGrid);

            Title = TITLE;
            Closing += SyncControl_Closing;
            KeyUp += SyncControl_KeyUp;

            ButtonSyncMetadata.Icon = Icons.GetAppIcon(Icons.SyncWithCloud);
            ButtonSyncDocuments.Icon = Icons.GetAppIcon(Icons.SyncPDFsWithCloud);

            ButtonSync.Icon = Icons.GetAppIcon(Icons.SyncWithCloud);
            ButtonSync.Caption = "Start sync";
            ButtonSync.Click += ButtonSync_Click;
            ButtonRefresh.Icon = Icons.GetAppIcon(Icons.Refresh);
            ButtonRefresh.Caption = "Refresh";
            ButtonRefresh.Click += ButtonRefresh_Click;
            ButtonCancel.Icon = Icons.GetAppIcon(Icons.Cancel);
            ButtonCancel.Caption = "Cancel sync";
            ButtonCancel.Click += ButtonCancel_Click;
        }

        private void GRIDCHECKBOX_Checked(object sender, RoutedEventArgs e)
        {
            // THIS HACK IS NEEDED BECAUSE I DONT KNOW HOW TO GET THE CHECKBOX TO UPDATE ITS BINDINGS NICELY WITH A SINGLE CLICK :-(
            CheckBox cb = (CheckBox)sender;
            var a = cb.DataContext;
            cb.BindingGroup.CommitEdit();
        }

        private void SyncControl_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    ButtonSync_Click(null, null);
                    e.Handled = true;
                    break;

                case Key.Escape:
                    ButtonCancel_Click(null, null);
                    e.Handled = true;
                    break;

                default:
                    break;
            }
        }

        private void HyperlinkPremium_Click(object sender, RoutedEventArgs e)
        {
            WebsiteAccess.OpenWebsite(WebsiteAccess.GetPremiumUrl("SYNC_INSTRUCTIONS"));
        }

        private void HyperlinkPremiumPlus_Click(object sender, RoutedEventArgs e)
        {
            WebsiteAccess.OpenWebsite(WebsiteAccess.GetPremiumPlusUrl("SYNC_INSTRUCTIONS"));
        }

        private void SyncControl_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            Visibility = Visibility.Collapsed;
        }

        private void ButtonSync_Click(object sender, RoutedEventArgs e)
        {
            LibrarySyncManager.Instance.Sync(sync_control_grid_item_set);
            if (!KeyboardTools.IsCTRLDown())
            {
                Close();
            }
        }

        private void ButtonRefresh_Click(object sender, RoutedEventArgs e)
        {
            LibrarySyncManager.Instance.RefreshSyncControl(sync_control_grid_item_set, this);
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        internal void SetSyncParameters(SyncControlGridItemSet sync_control_grid_item_set)
        {
            this.sync_control_grid_item_set = sync_control_grid_item_set;

            // Clear up
            GridLibraryGrid.ItemsSource = null;

            // Freshen up
            if (null != sync_control_grid_item_set)
            {
                // Populate the grid
                GridLibraryGrid.ItemsSource = sync_control_grid_item_set.grid_items;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // base.OnClosed() invokes this class' Closed() code, so we flipped the order of exec to reduce the number of surprises for yours truly.
            // This NULLing stuff is really the last rites of Dispose()-like so we stick it at the end here.

            try
            {
                sync_control_grid_item_set = null;

                DataContext = null;
            }
            catch (Exception ex)
            {
                Logging.Error(ex);
            }
        }
    }
}
