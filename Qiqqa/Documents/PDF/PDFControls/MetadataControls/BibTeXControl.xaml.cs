﻿using System;
using System.Windows;
using System.Windows.Controls;
using icons;
using Qiqqa.Documents.PDF.MetadataSuggestions;
using Qiqqa.Localisation;
using Utilities;
using Utilities.BibTex.Parsing;
using Utilities.GUI;
using Utilities.Reflection;
using Utilities.Strings;

namespace Qiqqa.Documents.PDF.PDFControls.MetadataControls
{
    /// <summary>
    /// Interaction logic for BibTeXControl.xaml
    /// </summary>
    public partial class BibTeXControl : UserControl
    {
        public BibTeXControl()
        {
            InitializeComponent();

            ButtonBibTexEditor.Caption = "Popup";
            ButtonBibTexEditor.ToolTip = "Edit this BibTeX in a larger popup window.";
            ButtonBibTexEditor.Icon = Icons.GetAppIcon(Icons.Window);
            ButtonBibTexEditor.IconHeight = 24;
            ButtonBibTexEditor.Click += ButtonBibTexEditor_Click;

            ButtonBibTexClear.Caption = "Clear";
            ButtonBibTexClear.ToolTip = "Clear this BibTeX.";
            ButtonBibTexClear.Icon = Icons.GetAppIcon(Icons.BibTeXReset);
            ButtonBibTexClear.IconHeight = 24;
            ButtonBibTexClear.Click += ButtonBibTexClear_Click;

            ButtonToggleBibTeX.Click += ButtonToggleBibTeX_Click;
            ButtonAckBibTeXParseErrors.Click += ButtonAckBibTeXParseErrors_Click;
            ButtonUndoBibTeXEdit.Click += ButtonUndoBibTeXEdit_Click;
            //ObjBibTeXEditorControl.RegisterOverlayButtons(ButtonAckBibTeXParseErrors, ButtonToggleBibTeX, ButtonUndoBibTeXEdit, IconHeight: 24);

            ButtonBibTexAutoFind.Caption = "Find";
            ButtonBibTexAutoFind.ToolTip = LocalisationManager.Get("LIBRARY/TIP/BIBTEX_AUTOFIND");
            ButtonBibTexAutoFind.Click += ButtonBibTexAutoFind_Click;
            ButtonBibTexAutoFind.MinWidth = 0;

            ButtonBibTexSniffer.Caption = "Sniffer";
            ButtonBibTexSniffer.ToolTip = LocalisationManager.Get("LIBRARY/TIP/BIBTEX_SNIFFER");
            ButtonBibTexSniffer.Click += ButtonBibTexSniffer_Click;
            ButtonBibTexSniffer.MinWidth = 0;

            ButtonUseSummary.Caption = "Summary";
            ButtonUseSummary.ToolTip = "Use your Reference Summary information to create a BibTeX record.";
            ButtonUseSummary.Click += ButtonUseSummary_Click;
            ButtonUseSummary.MinWidth = 0;
        }

        private void ButtonUndoBibTeXEdit_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxes.Error("Sorry!\n\nMethod has not been implemented yet!");
        }

        private void ButtonAckBibTeXParseErrors_Click(object sender, RoutedEventArgs e)
        {
            //ObjBibTeXEditorControl.ToggleBibTeXErrorView();
        }

        private void ButtonToggleBibTeX_Click(object sender, RoutedEventArgs e)
        {
            //ObjBibTeXEditorControl.ToggleBibTeXMode(TriState.Arbitrary);
        }

        private void ButtonBibTexClear_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBoxes.AskQuestion("Are you sure you wish to clear this BibTeX?"))
            {
                AugmentedBindable<PDFDocument> pdf_document_bindable = DataContext as AugmentedBindable<PDFDocument>;
                if (null == pdf_document_bindable)
                {
                    return;
                }

                pdf_document_bindable.Underlying.BibTex = "";
                pdf_document_bindable.NotifyPropertyChanged(nameof(pdf_document_bindable.Underlying.BibTex));
            }
        }

        private void ButtonUseSummary_Click(object sender, RoutedEventArgs e)
        {
            AugmentedBindable<PDFDocument> pdf_document_bindable = DataContext as AugmentedBindable<PDFDocument>;
            if (null == pdf_document_bindable)
            {
                return;
            }

            if (!String.IsNullOrEmpty(pdf_document_bindable.Underlying.BibTex))
            {
                if (!MessageBoxes.AskQuestion("You already have BibTeX associated with this record.  Are you sure you want to overwrite it?"))
                {
                    return;
                }
            }

            BibTexItem bibtem_item = new BibTexItem();
            bibtem_item.Type = "article";
            bibtem_item.Key = String.Format(
                "{0}{1}{2}"
                , Constants.UNKNOWN_AUTHORS != pdf_document_bindable.Underlying.AuthorsCombined ? StringTools.GetFirstWord(pdf_document_bindable.Underlying.AuthorsCombined) : ""
                , Constants.UNKNOWN_YEAR != pdf_document_bindable.Underlying.YearCombined ? StringTools.GetFirstWord(pdf_document_bindable.Underlying.YearCombined) : ""
                , Constants.TITLE_UNKNOWN != pdf_document_bindable.Underlying.TitleCombined ? StringTools.GetFirstWord(pdf_document_bindable.Underlying.TitleCombined) : ""
            );

            if (Constants.TITLE_UNKNOWN != pdf_document_bindable.Underlying.TitleCombined) bibtem_item["title"] = pdf_document_bindable.Underlying.TitleCombined;
            if (Constants.UNKNOWN_AUTHORS != pdf_document_bindable.Underlying.AuthorsCombined) bibtem_item["author"] = pdf_document_bindable.Underlying.AuthorsCombined;
            if (Constants.UNKNOWN_YEAR != pdf_document_bindable.Underlying.YearCombined) bibtem_item["year"] = pdf_document_bindable.Underlying.YearCombined;

            pdf_document_bindable.Underlying.BibTex = bibtem_item.ToBibTex();
            pdf_document_bindable.NotifyPropertyChanged(nameof(pdf_document_bindable.Underlying.BibTex));
        }

        private void ButtonBibTexEditor_Click(object sender, RoutedEventArgs e)
        {
            AugmentedBindable<PDFDocument> pdf_document_bindable = DataContext as AugmentedBindable<PDFDocument>;
            if (null == pdf_document_bindable)
            {
                return;
            }

            MetadataBibTeXEditorControl editor = new MetadataBibTeXEditorControl();
            editor.Show(pdf_document_bindable);
        }

        private void ButtonBibTexAutoFind_Click(object sender, RoutedEventArgs e)
        {
            AugmentedBindable<PDFDocument> pdf_document_bindable = DataContext as AugmentedBindable<PDFDocument>;
            if (null == pdf_document_bindable)
            {
                return;
            }

            bool found_bibtex = PDFMetadataInferenceFromBibTeXSearch.InferBibTeX(pdf_document_bindable.Underlying, true);
            if (!found_bibtex)
            {
                if (MessageBoxes.AskQuestion("Qiqqa was unable to automatically find BibTeX for this document.  Do you want to try using the BibTeX Sniffer?"))
                {
                    GoogleBibTexSnifferControl sniffer = new GoogleBibTexSnifferControl();
                    sniffer.Show(pdf_document_bindable.Underlying);
                }
            }
        }

        private void ButtonBibTexSniffer_Click(object sender, RoutedEventArgs e)
        {
            AugmentedBindable<PDFDocument> pdf_document_bindable = DataContext as AugmentedBindable<PDFDocument>;
            if (null == pdf_document_bindable)
            {
                return;
            }

            GoogleBibTexSnifferControl sniffer = new GoogleBibTexSnifferControl();
            sniffer.Show(pdf_document_bindable.Underlying);
        }
    }
}
