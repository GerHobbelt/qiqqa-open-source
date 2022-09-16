﻿using System;
using Qiqqa.Common.Configuration;
using Qiqqa.DocumentLibrary.WebLibraryStuff;
using Qiqqa.Documents.PDF;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;


namespace Qiqqa.DocumentLibrary
{
    public static class QiqqaManualTools
    {
        private static readonly Lazy<string> QiqqaManualFilename = new Lazy<string>(() => Path.GetFullPath(Path.Combine(ConfigurationManager.Instance.StartupDirectoryForQiqqa, @"The Qiqqa Manual.pdf")));

        private static readonly Lazy<string> LoexManualFilename = new Lazy<string>(() => Path.GetFullPath(Path.Combine(ConfigurationManager.Instance.StartupDirectoryForQiqqa, @"The Qiqqa Manual - LOEX.pdf")));


        private static PDFDocument AddQiqqaManualToLibrary(WebLibraryDetail web_library_detail)
        {
            FilenameWithMetadataImport fwmi = new FilenameWithMetadataImport();
            fwmi.filename = QiqqaManualFilename.Value;
            fwmi.tags.Add("manual");
            fwmi.tags.Add("help");
            fwmi.bibtex =
                "@booklet{qiqqa_manual" + "\n" +
                ",	title	= {The Qiqqa Manual}" + "\n" +
                ",	author	= {The Qiqqa Team,}" + "\n" +
                ",	year	= {2013}" + "\n" +
                "}"
                ;

            PDFDocument pdf_document = ImportingIntoLibrary.AddNewPDFDocumentsToLibraryWithMetadata_SYNCHRONOUS(web_library_detail, true, new FilenameWithMetadataImport[] { fwmi });

            return pdf_document;
        }

        private static PDFDocument AddLoexManualToLibrary(WebLibraryDetail web_library_detail)
        {
            FilenameWithMetadataImport fwmi = new FilenameWithMetadataImport();
            fwmi.filename = LoexManualFilename.Value;
            fwmi.tags.Add("manual");
            fwmi.tags.Add("help");
            fwmi.bibtex =
                "@article{qiqqatechmatters" + "\n" +
                ",	title	= {TechMatters: “Qiqqa” than you can say Reference Management: A Tool to Organize the Research Process}" + "\n" +
                ",	author	= {Krista Graham}" + "\n" +
                ",	year	= {2014}" + "\n" +
                ",	publication	= {LOEX Quarterly}" + "\n" +
                ",	volume	= {40}" + "\n" +
                ",	pages	= {4-6}" + "\n" +
                "}"
                ;

            PDFDocument pdf_document = ImportingIntoLibrary.AddNewPDFDocumentsToLibraryWithMetadata_SYNCHRONOUS(web_library_detail, true, new FilenameWithMetadataImport[] { fwmi });

            return pdf_document;
        }

        public static PDFDocument AddManualsToLibrary(WebLibraryDetail web_library_detail)
        {
            AddLoexManualToLibrary(web_library_detail);
            return AddQiqqaManualToLibrary(web_library_detail);
        }
    }
}
