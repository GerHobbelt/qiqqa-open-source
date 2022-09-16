﻿using System;
using System.Windows;
using System.Windows.Controls;
using Qiqqa.Common;
using Qiqqa.Common.SpeedRead;
using Qiqqa.Documents.PDF.PDFControls.MetadataControls;
using Qiqqa.UtilisationTracking;
using Utilities;
using Utilities.GUI;

namespace Qiqqa.Documents.PDF.PDFControls.Page.Tools
{
    /// <summary>
    /// Interaction logic for PDFTextSelectPopup.xaml
    /// </summary>
    public partial class PDFTextSelectPopup : StackPanel
    {
        private string selected_text;
        private PDFDocument pdf_document;
        private AugmentedPopup popup;

        public PDFTextSelectPopup(string selected_text, PDFDocument pdf_document)
        {
            this.selected_text = selected_text;
            this.pdf_document = pdf_document;

            InitializeComponent();

            MenuItemCopy.Header = "Copy";
            MenuItemCopy.Click += MenuItemCopy_Click;

            MenuItemSpeedRead.Header = "Speed Read";
            MenuItemSpeedRead.Click += MenuItemSpeedRead_Click;

            MenuItemSearchInternet.Header = "Search the web";
            MenuItemSearchInternet.Click += MenuItemSearchInternet_Click;

            MenuItemSearchLibrary.Header = "Search your library";
            MenuItemSearchLibrary.Click += MenuItemSearchLibrary_Click;

            MenuItemWebsiteDictionary.Header = "Lookup in Dictionary.com";
            MenuItemWebsiteDictionary.Click += MenuItemWebsiteDictionary_Click;

            MenuItemTagSet.Header = "Add as a tag";
            MenuItemTagSet.Click += MenuItemTagSet_Click;

            MenuItemBibTexSet.Header = "Use as BibTeX search terms";
            MenuItemBibTexSet.Click += MenuItemBibTexSet_Click;

            MenuItemTitleSet.Header = "Use as paper Title";
            MenuItemTitleSet.Click += MenuItemTitleSet_Click;

            MenuItemYearSet.Header = "Use as paper Year";
            MenuItemYearSet.Click += MenuItemYearSet_Click;

            MenuItemAbstractSet.Header = "Use as paper Abstract override";
            MenuItemAbstractSet.Click += MenuItemAbstractSet_Click;
            MenuItemAbstractClear.Header = "Clear paper Abstract override";
            MenuItemAbstractClear.Click += MenuItemAbstractClear_Click;

            MenuItemAuthorSet.Header = "Use as paper Authors";
            MenuItemAuthorSet.Click += MenuItemAuthorSet_Click;
            MenuItemAuthorAppend.Header = "Append to paper Authors";
            MenuItemAuthorAppend.Click += MenuItemAuthorAppend_Click;

            popup = new AugmentedPopup(this);

            //Unloaded += PDFTextSelectPopup_Unloaded;
            Dispatcher.ShutdownStarted += Dispatcher_ShutdownStarted;
        }

        private void Dispatcher_ShutdownStarted(object sender, EventArgs e)
        {
            CleanUp();
        }

        private void PDFTextSelectPopup_Unloaded(object sender, RoutedEventArgs e)
        {
            CleanUp();
        }

        private void CleanUp()
        { 
            this.selected_text = null;
            this.pdf_document = null;
            this.popup = null;

            Dispatcher.ShutdownStarted -= Dispatcher_ShutdownStarted;
        }

        private void MenuItemBibTexSet_Click(object sender, RoutedEventArgs e)
        {
            using (var c = popup.AutoCloser)
            {
                GoogleBibTexSnifferControl sniffer = new GoogleBibTexSnifferControl();
                sniffer.Show(pdf_document, selected_text);
            }
        }

        private void MenuItemWebsiteDictionary_Click(object sender, RoutedEventArgs e)
        {
            using (var c = popup.AutoCloser)
            {
                FeatureTrackingManager.Instance.UseFeature(Features.Document_WebsiteDictionary);

                MainWindowServiceDispatcher.Instance.SearchDictionary(selected_text);
            }
        }

        private void MenuItemTagSet_Click(object sender, RoutedEventArgs e)
        {
            using (var c = popup.AutoCloser)
            {
                FeatureTrackingManager.Instance.UseFeature(Features.Document_TextToTag);

                pdf_document.AddTag(selected_text);
            }
        }

        public void Open()
        {
            popup.IsOpen = true;
        }

        private void MenuItemTitleSet_Click(object sender, RoutedEventArgs e)
        {
            using (var c = popup.AutoCloser)
            {
                FeatureTrackingManager.Instance.UseFeature(Features.Document_TextToMetadata);

                if (null != pdf_document.Title && pdf_document.Title.Length > 0 && 0 != pdf_document.Title.CompareTo(pdf_document.DownloadLocation))
                {
                    if (!MessageBoxes.AskQuestion("Are you sure you want to replace the title of this document with '{0}'?", selected_text))
                    {
                        return;
                    }
                }

                pdf_document.TitleCombined = selected_text;
                pdf_document.Bindable.NotifyPropertyChanged(nameof(pdf_document.TitleCombined));
            }
        }

        private void MenuItemYearSet_Click(object sender, RoutedEventArgs e)
        {
            using (var c = popup.AutoCloser)
            {
                FeatureTrackingManager.Instance.UseFeature(Features.Document_TextToMetadata);

                pdf_document.YearCombined = selected_text;
                pdf_document.Bindable.NotifyPropertyChanged(nameof(pdf_document.YearCombined));
            }
        }

        private void MenuItemAbstractSet_Click(object sender, RoutedEventArgs e)
        {
            using (var c = popup.AutoCloser)
            {
                FeatureTrackingManager.Instance.UseFeature(Features.Document_SetAbstract);

                pdf_document.Abstract = selected_text;
                pdf_document.Bindable.NotifyPropertyChanged(nameof(pdf_document.Abstract));
            }
        }

        private void MenuItemAbstractClear_Click(object sender, RoutedEventArgs e)
        {
            using (var c = popup.AutoCloser)
            {
                FeatureTrackingManager.Instance.UseFeature(Features.Document_ClearAbstract);

                pdf_document.Abstract = null;
                pdf_document.Bindable.NotifyPropertyChanged(nameof(pdf_document.Abstract));
            }
        }

        private void MenuItemAuthorSet_Click(object sender, RoutedEventArgs e)
        {
            using (var c = popup.AutoCloser)
            {
                FeatureTrackingManager.Instance.UseFeature(Features.Document_TextToMetadata);

                pdf_document.AuthorsCombined = selected_text;
                pdf_document.Bindable.NotifyPropertyChanged(nameof(pdf_document.AuthorsCombined));
            }
        }

        private void MenuItemAuthorAppend_Click(object sender, RoutedEventArgs e)
        {
            using (var c = popup.AutoCloser)
            {
                FeatureTrackingManager.Instance.UseFeature(Features.Document_TextToMetadata);

                string authors = pdf_document.AuthorsCombined;
                if (Constants.UNKNOWN_AUTHORS == authors)
                {
                    authors = "";
                }
                if (!String.IsNullOrEmpty(authors))
                {
                    authors = authors + " and ";
                }

                authors = authors + selected_text;

                pdf_document.AuthorsCombined = authors;
                pdf_document.Bindable.NotifyPropertyChanged(nameof(pdf_document.AuthorsCombined));
            }
        }

        private void MenuItemSearchInternet_Click(object sender, RoutedEventArgs e)
        {
            using (var c = popup.AutoCloser)
            {
                FeatureTrackingManager.Instance.UseFeature(Features.Document_SearchInternet);

                MainWindowServiceDispatcher.Instance.SearchWeb(selected_text);
            }
        }

        private void MenuItemSearchLibrary_Click(object sender, RoutedEventArgs e)
        {
            using (var c = popup.AutoCloser)
            {
                FeatureTrackingManager.Instance.UseFeature(Features.Document_SearchLibrary);

                MainWindowServiceDispatcher.Instance.SearchLibrary(pdf_document.LibraryRef, selected_text);
            }
        }

        private void MenuItemCopy_Click(object sender, RoutedEventArgs e)
        {
            using (var c = popup.AutoCloser)
            {
                FeatureTrackingManager.Instance.UseFeature(Features.Document_CopyText);

                try
                {
                    ClipboardTools.SetText(selected_text);
                }

                catch (Exception ex)
                {
                    Logging.Error(ex, "There was a problem copying text to the clipboard.");
                }
            }
        }

        private void MenuItemSpeedRead_Click(object sender, RoutedEventArgs e)
        {
            using (var c = popup.AutoCloser)
            {
                FeatureTrackingManager.Instance.UseFeature(Features.Document_TextSelectSpeedRead);

                try
                {
                    SpeedReadControl src = MainWindowServiceDispatcher.Instance.OpenSpeedRead();
                    src.UseText(selected_text);
                }

                catch (Exception ex)
                {
                    Logging.Error(ex, "There was a problem speed reading.");
                }
            }
        }


    }
}
