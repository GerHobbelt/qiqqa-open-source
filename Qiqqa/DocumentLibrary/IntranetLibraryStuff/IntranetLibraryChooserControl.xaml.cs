﻿using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using icons;
using Ookii.Dialogs.Wpf;
using Qiqqa.Common.GUI;
using Qiqqa.UtilisationTracking;
using Utilities;
using Utilities.GUI;
using Utilities.Misc;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;


namespace Qiqqa.DocumentLibrary.IntranetLibraryStuff
{
    /// <summary>
    /// Interaction logic for IntranetLibraryChooserControl.xaml
    /// </summary>
    public partial class IntranetLibraryChooserControl : StandardWindow
    {
        public IntranetLibraryChooserControl()
        {
            //Theme.Initialize(); -- already done in StandardWindow base class

            InitializeComponent();

            Title =
            Header.Caption = "Create/Join Intranet Library";

            Header.SubCaption = "Please confirm the details of the Intranet Library you wish to create or join.";
            Header.Img = Icons.GetAppIcon(Icons.WebLibrary_IntranetLibrary);

            ButtonCancel.Caption = "Cancel";
            ButtonCancel.Icon = Icons.GetAppIcon(Icons.Cancel);
            ButtonCancel.Click += ButtonCancel_Click;

            ButtonJoinCreate.Caption = "Create";
            ButtonJoinCreate.Icon = Icons.GetAppIcon(Icons.WebLibrary_IntranetLibrary);
            ButtonJoinCreate.Click += ButtonJoinCreate_Click;

            ObjButtonFolderChoose.Click += ObjButtonFolderChoose_Click;
            TxtPath.TextChanged += TxtPath_TextChanged;
        }

        private void ButtonJoinCreate_Click(object sender, RoutedEventArgs e)
        {
            FeatureTrackingManager.Instance.UseFeature(Features.StartPage_CreateIntranetLibrary);

            if (String.IsNullOrEmpty(TxtPath.Text))
            {
                MessageBoxes.Error("Please enter a path to your Intranet Library.");
                return;
            }

            string db_base_path = TxtPath.Text;
            string db_title = TxtTitle.Text;
            string db_description = TxtDescription.Text;

            SafeThreadPool.QueueUserWorkItem(() =>
            {
                try
                {
                    IntranetLibraryDetail.EnsureIntranetLibraryExists(db_base_path, db_title, db_description);
                }
                catch (Exception ex)
                {
                    Logging.Error(ex, "There was a problem while trying to connect to this Intranet Library.  Are you sure you have permission to access this folder?  Your Network or System Administrator can grant you this permission.\n\nThe detailed error message is:\n" + ex.Message);

                    WPFDoEvents.InvokeAsyncInUIThread(() =>
                    {
                        MessageBoxes.Error("There was a problem while trying to connect to this Intranet Library.  Are you sure you have permission to access this folder?  Your Network or System Administrator can grant you this permission.\n\nThe detailed error message is:\n" + ex.Message);
                    });
                }
                finally
                {
                    WPFDoEvents.InvokeAsyncInUIThread(() =>
                    {
                        Close();
                    });
                }
            });
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TxtPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            WPFDoEvents.SafeExec(() =>
            {
                ValidateFolder();
            });
        }

        private void ValidateFolder()
        {
            string base_path = "???";

            try
            {
                ButtonJoinCreate.Caption = "Create";

                base_path = TxtPath.Text;
                if (Directory.Exists(base_path))
                {
                    string library_detail_path = IntranetLibraryTools.GetLibraryDetailPath(base_path);
                    if (File.Exists(library_detail_path))
                    {
                        IntranetLibraryDetail library_detail = IntranetLibraryDetail.Read(library_detail_path);
                        TxtTitle.Text = library_detail.Title;
                        TxtDescription.Text = library_detail.Description;
                        ButtonJoinCreate.Caption = "Join";
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Warn(ex, "There was an exception while validating the selected Intranet Library path {0}.", base_path);
            }
        }

        private void ObjButtonFolderChoose_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog();

            dialog.Description = "Please select the shared directory for your Intranet Library.";
            dialog.UseDescriptionForTitle = true; // This applies to the Vista style dialog only, not the old dialog.

            string default_folder = TxtPath.Text;
            if (default_folder != null)
            {
                dialog.SelectedPath = default_folder;
            }

            if (!VistaFolderBrowserDialog.IsVistaFolderDialogSupported)
            {
                MessageBoxes.Warn("Because you are not using Windows Vista or later, the regular folder browser dialog will be used. Please use Windows Vista to see the new dialog.", "Sample folder browser dialog");
            }

            if ((bool)dialog.ShowDialog())
            {
                TxtPath.Text = dialog.SelectedPath;
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
        }
    }
}
