﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using icons;
using Microsoft.WindowsAPICodePack.Dialogs;
using Qiqqa.Common.GUI;
using Qiqqa.DocumentLibrary.WebLibraryStuff;
using Qiqqa.UtilisationTracking;
using Utilities;
using Utilities.GUI;
using Utilities.Misc;
using Utilities.Reflection;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Qiqqa.DocumentLibrary.Import.Manual
{
    /// <summary>
    /// Provides UI for importing documents from 3rd parties.
    /// </summary>
    public partial class ImportFromThirdParty : StandardWindow
    {
        private WebLibraryDetail web_library_detail;
        private Providers _currentProvider;
        private UiState _currentUiState;
        private string _currentSelectedExportFile;
        private string _currentSelectedSupplementaryFolder;
        private const long THRESHOLD_IMPORT_FILESIZE_WARNING_BYTES = 100 * 1024;
        private Auto.MendeleyImporter.MendeleyDatabaseDetails mdd;
        private Auto.EndnoteImporter.EndnoteDatabaseDetails edd;

        public ImportFromThirdParty(WebLibraryDetail _web_library_detail)
        {
            web_library_detail = _web_library_detail;

            Theme.Initialize();

            InitializeComponent();

            btnProvider_BibTeX.Icon = Icons.GetAppIcon(Icons.Import_BibTeX);
            btnProvider_Mendeley.Icon = Icons.GetAppIcon(Icons.Import_Mendeley);
            btnProvider_Zotero.Icon = Icons.GetAppIcon(Icons.Import_Zotero);
            btnProvider_EndNote.Icon = Icons.GetAppIcon(Icons.Import_EndNote);
            btnProvider_JabRef.Icon = Icons.GetAppIcon(Icons.Import_JabRef);

            btnProvider_BibTeX.Click += ProviderChosen_Click;
            btnProvider_Mendeley.Click += ProviderChosen_Click;
            btnProvider_Zotero.Click += ProviderChosen_Click;
            btnProvider_EndNote.Click += ProviderChosen_Click;
            btnProvider_JabRef.Click += ProviderChosen_Click;

            btnChooseFile_BibTeX.Click += ChooseExportFile;
            btnChooseFile_Mendeley.Click += ChooseExportFile;
            btnChooseFile_Zotero.Click += ChooseExportFile;
            btnChooseFile_EndNote.Click += ChooseExportFile;
            btnChooseFile_JabRef.Click += ChooseExportFile;

            btnChooseFile_EndNoteLibraryFolder.Click += ChooseSupplementaryFolder;

            btnBack.Caption = "Back";
            btnBack.Icon = Icons.GetAppIcon(Icons.Back);
            btnBack.Click += btnBack_Click;

            btnCancel.Caption = "Cancel";
            btnCancel.Icon = Icons.GetAppIcon(Icons.Cancel);
            btnCancel.Click += btnCancel_Click;

            btnSelectAll.Caption = "Select all";
            btnSelectNone.Caption = "Select none";
            btnSelectAll.Click += btnSelectAll_Click;
            btnSelectNone.Click += btnSelectNone_Click;

            btnImport.Caption = "Import Selected Entries";
            btnImport.Icon = Icons.GetAppIcon(Icons.DocumentsImportFromThirdParty);
            btnImport.Click += btnImport_Click;

            Header.Caption = "Import";
            Header.Img = Icons.GetAppIcon(Icons.DocumentsImportFromThirdParty);

            SetUiState(UiState.ChooseProvider);

            {
                edd = Auto.EndnoteImporter.DetectEndnoteDatabaseDetails();
                if (0 < edd.documents_found)
                {
                    AugmentedToolBarButton ab = new AugmentedToolBarButton();
                    ab.Icon = Icons.GetAppIcon(Icons.Import_EndNote);
                    ab.Caption = "EndNote™\r\n" + edd.PotentialImportMessage;
                    ab.Click += CmdAutomaticEndnoteImport_Click;
                    ObjAutoImportDescriptionDocument.Children.Add(ab);

                    ObjAutoImportDescriptionDocument.Children.Add(new TextBlock());
                    ObjAutoImportDescriptionDocument.Children.Add(new TextBlock());
                }
            }
            {
                mdd = Auto.MendeleyImporter.DetectMendeleyDatabaseDetails();
                if (0 < mdd.documents_found)
                {
                    AugmentedToolBarButton ab = new AugmentedToolBarButton();
                    ab.Icon = Icons.GetAppIcon(Icons.Import_Mendeley);
                    ab.Caption = "Mendeley™\r\n" + mdd.PotentialImportMessage;
                    ab.Click += CmdAutomaticMendeleyImport_Click;
                    ObjAutoImportDescriptionDocument.Children.Add(ab);

                    ObjAutoImportDescriptionDocument.Children.Add(new TextBlock());
                    ObjAutoImportDescriptionDocument.Children.Add(new TextBlock());
                }
            }

            if (0 == mdd.documents_found && 0 == edd.documents_found)
            {
                TextBlock tb = new TextBlock();
                tb.TextWrapping = TextWrapping.Wrap;
                tb.Text = "Qiqqa has not been able to automatically detect any other reference managers on your computer.  Please use the manual import options to the right.";
                ObjAutoImportDescriptionDocument.Children.Add(tb);

                ObjAutoImportDescriptionDocument.Children.Add(new TextBlock());
            }
        }

        private void CmdAutomaticMendeleyImport_Click(object sender, RoutedEventArgs e)
        {
            FeatureTrackingManager.Instance.UseFeature(Features.Library_ImportAutoFromMendeley);
            ImportingIntoLibrary.AddNewPDFDocumentsToLibraryWithMetadata_ASYNCHRONOUS(web_library_detail, false, mdd.metadata_imports.ToArray());
            Close();
        }

        private void CmdAutomaticEndnoteImport_Click(object sender, RoutedEventArgs e)
        {
            FeatureTrackingManager.Instance.UseFeature(Features.Library_ImportAutoFromEndNote);
            ImportingIntoLibrary.AddNewPDFDocumentsToLibraryWithMetadata_ASYNCHRONOUS(web_library_detail, false, edd.metadata_imports.ToArray());
            Close();
        }

        private void btnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var entry in GetEntries())
            {
                entry.Underlying.Selected = false;
            }
        }

        private void btnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var entry in GetEntries())
            {
                entry.Underlying.Selected = true;
            }
        }

        private List<AugmentedBindable<BibTeXEntry>> GetEntries()
        {
            return lstEntries.DataContext as List<AugmentedBindable<BibTeXEntry>>;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            switch (_currentUiState)
            {
                case UiState.ChooseProvider:

                    break;

                case UiState.EntrySelection:
                    SetUiState(UiState.ProviderInstructions);
                    break;

                case UiState.ProviderInstructions:
                    SetUiState(UiState.ChooseProvider);
                    break;
            }
        }

        private void ProviderChosen_Click(object sender, RoutedEventArgs e)
        {
            if (e.Source == btnProvider_Mendeley)
            {
                _currentProvider = Providers.Mendeley;
            }
            else if (e.Source == btnProvider_Zotero)
            {
                _currentProvider = Providers.Zotero;
            }
            else if (e.Source == btnProvider_EndNote)
            {
                _currentProvider = Providers.EndNote;
            }
            else if (e.Source == btnProvider_BibTeX)
            {
                _currentProvider = Providers.BibTeX;
            }
            else if (e.Source == btnProvider_JabRef)
            {
                _currentProvider = Providers.JabRef;
            }
            else
            {
                throw new NotImplementedException();
            }

            SetUiState(UiState.ProviderInstructions);
        }

        private void ChooseExportFile(object sender, RoutedEventArgs e)
        {
            _currentSelectedExportFile = null;

#if DEBUG
            if (Runtime.IsRunningInVisualStudioDesigner) return;
#endif

            switch (_currentProvider)
            {
                case Providers.BibTeX:
                    _currentSelectedExportFile = GetFileNameFromDialog("Choose BibTeX Export File", ".bib", "BibTeXRecords.bib");
                    break;

                case Providers.Mendeley:
                    _currentSelectedExportFile = GetFileNameFromDialog("Choose Mendeley Export BibTeX File", ".bib", "My Collection.bib");
                    break;

                case Providers.Zotero:
                    _currentSelectedExportFile = GetFileNameFromDialog("Choose Zotero Export BibTeX File", ".bib", "My Library.bib");
                    break;

                case Providers.EndNote:
                    _currentSelectedExportFile = GetFileNameFromDialog("Choose EndNote Export Text File", ".txt", "My EndNote Library.txt");
                    break;

                case Providers.JabRef:
                    _currentSelectedExportFile = GetFileNameFromDialog("Choose JabRef Database File", ".bib", "JabRef.bib");
                    break;

                default:
                    throw new NotImplementedException();
            }

            if (String.IsNullOrEmpty(_currentSelectedExportFile)) return;

            switch (_currentProvider)
            {
                case Providers.BibTeX:
                case Providers.Mendeley:
                case Providers.Zotero:
                case Providers.JabRef:
                    // We have all we need.
                    ParseImportFileAndSwitchToSelectEntriesUi();
                    break;

                case Providers.EndNote:
                    // Wait for second file.
                    btnChooseFile_EndNoteLibraryFolder.IsEnabled = true;
                    return;

                default:
                    throw new NotImplementedException();
            }
        }

        public void DoAutomatedBibTeXImport(string filename)
        {
            _currentProvider = Providers.BibTeX;
            _currentSelectedExportFile = filename;
            ParseImportFileAndSwitchToSelectEntriesUi();
        }

        private void ChooseSupplementaryFolder(object sender, RoutedEventArgs e)
        {
            _currentSelectedSupplementaryFolder = null;

#if DEBUG
            if (Runtime.IsRunningInVisualStudioDesigner) return;
#endif

            switch (_currentProvider)
            {
                case Providers.EndNote:
                    _currentSelectedSupplementaryFolder = GetFolderNameFromDialog("Choose the EndNote Library's Data Folder (e.g. 'My EndNote Library.Data')", Path.GetDirectoryName(_currentSelectedExportFile));
                    break;

                default:
                    throw new ApplicationException("_currentProvider wrong: " + _currentProvider);
            }

            //Cancelled.
            if (String.IsNullOrEmpty(_currentSelectedSupplementaryFolder)) return;

            switch (_currentProvider)
            {
                case Providers.EndNote:
                    ParseImportFileAndSwitchToSelectEntriesUi();
                    break;
            }
        }



        /// <summary>
        /// Creates importer, executes it, changes UI
        /// </summary>
        private void ParseImportFileAndSwitchToSelectEntriesUi()
        {
            FileImporter importer = null;

            #region Check we can open the file, and warn about file size if necessary

            try
            {
                long fileSize = 0;
                using (FileStream fs = new FileStream(_currentSelectedExportFile, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    fileSize = fs.Length;
                }

                if (fileSize > THRESHOLD_IMPORT_FILESIZE_WARNING_BYTES)
                {
                    MessageBoxes.Warn("The file you are importing is quite large. It may take a little while to display the next screen.  Please be patient as we compare the file against your Library.");
                }
            }
            catch
            {
                MessageBoxes.Error("Unfortunately that file could not be read. Is it perhaps still open by another program?");
                return;
            }

            #endregion

            try
            {
                switch (_currentProvider)
                {
                    case Providers.Mendeley:
                        importer = new MendeleyImporter(web_library_detail, _currentSelectedExportFile, false);
                        break;

                    case Providers.BibTeX:
                        importer = new MendeleyImporter(web_library_detail, _currentSelectedExportFile, true);
                        break;

                    case Providers.Zotero:
                        importer = new ZoteroImporter(web_library_detail, _currentSelectedExportFile);
                        break;

                    case Providers.EndNote:
                        if (!EndNoteImporter.ValidateDocumentRootFolder(_currentSelectedSupplementaryFolder))
                        {
                            MessageBoxes.Warn("The data directory you have picked might not be the right one - it should have a subdirectory called \"PDF\" where EndNote has exported your PDF files.");
                        }

                        importer = new EndNoteImporter(web_library_detail, _currentSelectedExportFile, _currentSelectedSupplementaryFolder);
                        break;

                    case Providers.JabRef:
                        importer = new JabRefImporter(web_library_detail, _currentSelectedExportFile);
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
            catch (Exception ex)
            {
                Logging.Warn(ex, "Could not parse {0} import file", _currentProvider);
                MessageBoxes.Error("Unfortunately there was a problem importing that file.");
                return;
            }

            // Get entries from parsed file and populate list

            var result = importer.GetResult();
            List<AugmentedBindable<BibTeXEntry>> bindableEntries = new List<AugmentedBindable<BibTeXEntry>>(result.Entries.Count);
            foreach (var entry in result.Entries)
            {
                entry.Selected = !entry.ExistsInLibrary;
                bindableEntries.Add(new AugmentedBindable<BibTeXEntry>(entry));
            }

            lstEntries.DataContext = bindableEntries;

            SetUiState(UiState.EntrySelection);

            if (result.EntriesWithoutFileField > 0)
            {
                ShowNoFilesGuidance(result.Entries.Count, result.EntriesWithoutFileField);
            }
            else if (importer.InputFileAppearsToBeWrongFormat)
            {
                ShowWrongFormatGuidance();
            }
        }

        private void ShowWrongFormatGuidance()
        {
            // Default prefix
            string help = String.Format("The file you selected had content, but it did not appear to be suitable for import");
            string suffix = String.Empty;

            switch (_currentProvider)
            {
                case Providers.EndNote:
                    suffix = "Did you perhaps select your EndNote library file, instead of exporting your library and selecting the export file? ";
                    break;
            }

            MessageBoxes.Warn(help + Environment.NewLine + Environment.NewLine + suffix);
        }

        private void ShowNoFilesGuidance(int totalEntryCount, int entriesWithoutFileFieldCount)
        {
            // Default prefix
            string prefix = String.Format("The file you selected had {0} entries, but {1} of them were missing the \"file\" field, OR the file could not be found on disk in the location specified by the file entry." + Environment.NewLine + Environment.NewLine + "Without the file entry (or the file being in the correct place on disk), we can't import the PDF.", totalEntryCount, entriesWithoutFileFieldCount);
            string suffix = String.Empty;

            switch (_currentProvider)
            {
                case Providers.Mendeley:
                    break;

                case Providers.BibTeX:
                    suffix = "Please ensure your BibTeX entries conform to the example format.";
                    break;

                case Providers.Zotero:
                    suffix = "Please ensure you're using the latest version of Zotero, and the \"Export Files\" tickbox is checked.  Check our FAQ for more help.";
                    break;

                case Providers.EndNote:
                    prefix = String.Format("The file you selected had entries, but {0} of them were missing the \"%>\" field, (or the file could not be found) so we couldn't import the PDF.", entriesWithoutFileFieldCount);
                    suffix = "Please ensure you followed the export instructions to ensure the entries contain this field.";
                    break;

                case Providers.JabRef:
                    break;
            }

            string help = "If you are sure these PDF files exist, please get in touch with Qiqqa Support so we can investigate.";
            MessageBoxes.Warn(prefix + Environment.NewLine + Environment.NewLine + suffix + Environment.NewLine + Environment.NewLine + help);
        }

        private static string GetFileNameFromDialog(string title, string defaultExt, string filename)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.CheckFileExists = true;
            ofd.Multiselect = false;
            ofd.Title = title;
            ofd.DefaultExt = defaultExt;
            ofd.Filter = String.Format("{0} files|*{0}|All files (*.*)|*.*", defaultExt);
            ofd.FileName = filename;

            if (true != ofd.ShowDialog()) return null;
            return ofd.FileName;
        }

        private static string GetFolderNameFromDialog(string title, string defaultFolder)
        {
            using (CommonOpenFileDialog dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = true;
                dialog.Title = title;
                dialog.DefaultDirectory = defaultFolder;
                CommonFileDialogResult result = dialog.ShowDialog();
                if (result != CommonFileDialogResult.Ok)
                {
                    return null;
                }
                return dialog.FileName;
            }
        }


        private void SetUiState(UiState uiState)
        {
            _currentUiState = uiState;

            switch (uiState)
            {
                case UiState.ChooseProvider:
                    btnBack.IsEnabled = false;
                    btnImport.IsEnabled = false;

                    HideAllInstructions();
                    pnlEntrySelection.Visibility = Visibility.Collapsed;
                    pnlProviders.Visibility = Visibility.Visible;

                    btnProvider_BibTeX.Caption = "Generic BibTeX / Qiqqa Export";
                    btnProvider_Mendeley.Caption = "Mendeley™";
                    btnProvider_Zotero.Caption = "Zotero™";
                    btnProvider_EndNote.Caption = "EndNote™";
                    btnProvider_JabRef.Caption = "JabRef";
                    Header.SubCaption = "If you already have documents in a program listed below, we can import them. \r\nPlease choose the relevant program to get started.";

                    break;

                case UiState.ProviderInstructions:
                    btnBack.IsEnabled = true;
                    btnImport.IsEnabled = false;
                    btnChooseFile_EndNoteLibraryFolder.IsEnabled = false;

                    pnlProviders.Visibility = Visibility.Collapsed;
                    pnlEntrySelection.Visibility = Visibility.Collapsed;

                    Header.SubCaption = "Please follow the instructions below";

                    switch (_currentProvider)
                    {
                        case Providers.BibTeX:
                            pnlInstructions_BibTeX.Visibility = Visibility.Visible;
                            break;
                        case Providers.Mendeley:
                            pnlInstructions_Mendeley.Visibility = Visibility.Visible;
                            break;
                        case Providers.Zotero:
                            pnlInstructions_Zotero.Visibility = Visibility.Visible;
                            break;
                        case Providers.EndNote:
                            pnlInstructions_EndNote.Visibility = Visibility.Visible;
                            break;
                        case Providers.JabRef:
                            pnlInstructions_JabRef.Visibility = Visibility.Visible;
                            break;
                    }

                    break;

                case UiState.EntrySelection:
                    btnBack.IsEnabled = true;
                    btnImport.IsEnabled = true;

                    pnlProviders.Visibility = Visibility.Collapsed;
                    HideAllInstructions();

                    pnlEntrySelection.Visibility = Visibility.Visible;

                    pnlAlreadyImported.Visibility = GetEntries().Any(x => x.Underlying.ExistsInLibrary) ? Visibility.Visible : Visibility.Collapsed;

                    Header.SubCaption = "Tick the entries you wish to import";
                    break;
            }
        }

        private void HideAllInstructions()
        {
            foreach (UIElement ele in pnlInstructions.Children)
            {
                ele.Visibility = Visibility.Collapsed;
            }
        }

        private void btnImport_Click(object sender, RoutedEventArgs e)
        {
            FeatureTrackingManager.Instance.UseFeature(Features.Library_ImportFromThirdParty);

            IEnumerable<AugmentedBindable<BibTeXEntry>> allEntries = GetEntries().Where(x => x.Underlying.Selected);

            if (!allEntries.Any())
            {
                MessageBoxes.Error("Please select at least one entry to import, by checking the checkbox.");
                return;
            }

            StatusManager.Instance.UpdateStatus("ImportFromThirdParty", "Started importing documents");

            List<FilenameWithMetadataImport> filename_and_bibtex_imports = new List<FilenameWithMetadataImport>();
            foreach (AugmentedBindable<BibTeXEntry> entry in allEntries)
            {
                FilenameWithMetadataImport filename_with_metadata_import = new FilenameWithMetadataImport
                {
                    filename = entry.Underlying.Filename,
                    bibtex = entry.Underlying.BibTeX,
                    tags = entry.Underlying.Tags,
                    notes = entry.Underlying.Notes
                };

                filename_and_bibtex_imports.Add(filename_with_metadata_import);
            }

            ImportingIntoLibrary.AddNewPDFDocumentsToLibraryWithMetadata_ASYNCHRONOUS(web_library_detail, false, filename_and_bibtex_imports.ToArray());

            MessageBoxes.Info("{0} files are now being imported - this may take a little while.  You can track the import progress in the status bar.", filename_and_bibtex_imports.Count);

            Close();
        }


        private enum Providers
        {
            BibTeX,
            Mendeley,
            Zotero,
            EndNote,
            JabRef
        }

        private enum UiState
        {
            ChooseProvider,
            ProviderInstructions,
            EntrySelection
        }

        #region --- Test ------------------------------------------------------------------------

#if TEST
        public static void Test()
        {
            Library library = Library.GuestInstance;
            ImportFromThirdParty win = new ImportFromThirdParty(library);
            win.ShowDialog();
        }

        public static void TestDirectBibTexImport()
        {
            Library library = Library.GuestInstance;
            ImportFromThirdParty win = new ImportFromThirdParty(library);
            win.DoAutomatedBibTeXImport(@"c:\temp\omnipatents_export.bib");
            win.ShowDialog();
        }
#endif

        #endregion

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // base.OnClosed() invokes this class' Closed() code, so we flipped the order of exec to reduce the number of surprises for yours truly.
            // This NULLing stuff is really the last rites of Dispose()-like so we stick it at the end here.

            web_library_detail = null;

            mdd = null;
            edd = null;
        }
    }
}
