﻿using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Documents;
using Qiqqa.DocumentLibrary.WebLibraryStuff;
using Qiqqa.Documents.PDF;
using Utilities;
using Utilities.Collections;

namespace Qiqqa.DocumentLibrary.LibraryFilter.GeneralExplorers
{
    /// <summary>
    /// Interaction logic for TagExplorerControl.xaml
    /// </summary>
    public partial class YearExplorerControl : UserControl
    {
        private WebLibraryDetail web_library_detail;

        public delegate void OnTagSelectionChangedDelegate(HashSet<string> fingerprints, Span descriptive_span);
        public event OnTagSelectionChangedDelegate OnTagSelectionChanged;

        public YearExplorerControl()
        {
            InitializeComponent();

            ToolTip = "So what years are interesting to you?";

            YearExplorerTree.DescriptionTitle = "Years";

            YearExplorerTree.GetNodeItems = GetNodeItems;

            YearExplorerTree.OnTagSelectionChanged += YearExplorerTree_OnTagSelectionChanged;
        }

        // -----------------------------

        public WebLibraryDetail LibraryRef
        {
            get => web_library_detail;
            set
            {
                web_library_detail = value;
                YearExplorerTree.LibraryRef = value;
            }
        }

        public void Reset()
        {
            YearExplorerTree.Reset();
        }

        // -----------------------------

        internal static MultiMapSet<string, string> GetNodeItems(WebLibraryDetail web_library_detail, HashSet<string> parent_fingerprints)
        {
            List<PDFDocument> pdf_documents = null;
            if (null == parent_fingerprints)
            {
                pdf_documents = web_library_detail.Xlibrary.PDFDocuments;
            }
            else
            {
                pdf_documents = web_library_detail.Xlibrary.GetDocumentByFingerprints(parent_fingerprints);
            }
            Logging.Debug特("YearExplorerControl: processing {0} documents from library {1}", pdf_documents.Count, web_library_detail.Title);

            MultiMapSet<string, string> tags_with_fingerprints = new MultiMapSet<string, string>();
            foreach (PDFDocument pdf_document in pdf_documents)
            {
                // The category of year
                tags_with_fingerprints.Add(GetYearCategory(pdf_document), pdf_document.Fingerprint);

                // The year itself
                string year_combined = pdf_document.YearCombined;
                if (Constants.UNKNOWN_YEAR != year_combined)
                {
                    tags_with_fingerprints.Add(year_combined, pdf_document.Fingerprint);
                }

            }

            return tags_with_fingerprints;
        }

        private static string GetYearCategory(PDFDocument pdf_document)
        {
            /*
             * So the ranges we are looking for are (if this year is y=2010)
             * 0. still to come...
             * 1. this year (y)
             * 2. last year (y-1)
             * 3. the last 5 years (y-5 to y-1)
             * 4. the last decade (y-10 to y-5)
             * 5. before that (to y--10)
             * 6. (unknown)
             */
            int year_of_document;
            if (Int32.TryParse(pdf_document.YearCombined, out year_of_document))
            {
                int this_year = DateTime.UtcNow.Year;

                if (this_year + 1 <= year_of_document) return "() still to come...?!";
                if (this_year + 0 <= year_of_document) return "(i) this year";
                if (this_year - 1 <= year_of_document) return "(ii) last year";
                if (this_year - 2 <= year_of_document) return "(iii) last 2 years";
                if (this_year - 3 <= year_of_document) return "(iv) last 3 years";
                if (this_year - 5 <= year_of_document) return "(v) the last 5 years";
                if (this_year - 10 <= year_of_document) return "(vi) the last decade";
                if (this_year - 20 <= year_of_document) return "(vii) the last 20 years";
                return "(viii) before all that";
            }
            else
            {
                return "(x) (unknown)";
            }
        }

        private void YearExplorerTree_OnTagSelectionChanged(HashSet<string> fingerprints, Span descriptive_span)
        {
            OnTagSelectionChanged?.Invoke(fingerprints, descriptive_span);
        }
    }
}
