﻿using System;
using System.Collections.Generic;
using System.Windows;
using Qiqqa.Common.Configuration;
using Qiqqa.DocumentLibrary;
using Qiqqa.DocumentLibrary.WebLibraryStuff;
using Qiqqa.Documents.PDF;
using Qiqqa.Documents.PDF.PDFControls;
using Utilities;
using Utilities.GUI;
using Utilities.Misc;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;


namespace Qiqqa.Common
{
    internal class RestoreDesktopManager
    {
        public static void SaveDesktop()
        {
            List<string> restore_settings = new List<string>();

            // Get the remembrances
            List<FrameworkElement> framework_elements = MainWindowServiceDispatcher.Instance.MainWindow.DockingManager.GetAllFrameworkElements();
            foreach (FrameworkElement framework_element in framework_elements)
            {
                {
                    LibraryControl library_control = framework_element as LibraryControl;
                    if (null != library_control)
                    {
                        Logging.Info("Remembering a library control {0}", library_control.LibraryRef.Id);
                        restore_settings.Add(String.Format("PDF_LIBRARY,{0}", library_control.LibraryRef.Id));
                    }
                }

                {
                    PDFReadingControl pdf_reading_control = framework_element as PDFReadingControl;
                    if (null != pdf_reading_control)
                    {
                        Logging.Info("Remembering a PDF reader {0}", pdf_reading_control.PDFRendererControlStats.pdf_document.Fingerprint);
                        restore_settings.Add(String.Format("PDF_DOCUMENT,{0},{1}", pdf_reading_control.PDFRendererControlStats.pdf_document.LibraryRef.Id, pdf_reading_control.PDFRendererControlStats.pdf_document.Fingerprint));
                    }
                }
            }

            // Store the remembrances
            File.WriteAllLines(Filename, restore_settings);
        }

        public static void RestoreDesktop()
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            try
            {
                // Get the remembrances
                if (File.Exists(Filename))
                {
                    string[] restore_settings = File.ReadAllLines(Filename);
                    foreach (string restore_setting in restore_settings)
                    {
                        try
                        {
                            if (restore_setting.StartsWith("PDF_LIBRARY"))
                            {
                                string[] parts = restore_setting.Split(',');
                                string library_id = parts[1];

                                WebLibraryDetail web_library_detail = WebLibraryManager.Instance.GetLibrary(library_id);
                                if (null == web_library_detail)
                                {
                                    Logging.Warn("RestoreDesktop: Cannot find library anymore for Id {0}", library_id);
                                }
                                else
                                {
                                    WPFDoEvents.InvokeInUIThread(() => MainWindowServiceDispatcher.Instance.OpenLibrary(web_library_detail));
                                }
                            }
                            else if (restore_setting.StartsWith("PDF_DOCUMENT"))
                            {
                                string[] parts = restore_setting.Split(',');
                                string library_id = parts[1];
                                string document_fingerprint = parts[2];

                                WebLibraryDetail web_library_detail = WebLibraryManager.Instance.GetLibrary(library_id);
                                if (null == web_library_detail)
                                {
                                    Logging.Warn("RestoreDesktop: Cannot find library anymore for Id {0}", library_id);
                                }
                                else
                                {
                                    PDFDocument pdf_document = web_library_detail.Xlibrary.GetDocumentByFingerprint(document_fingerprint);
                                    if (null == pdf_document)
                                    {
                                        Logging.Warn("RestoreDesktop: Cannot find document anymore for fingerprint {0}", document_fingerprint);
                                    }
                                    else
                                    {
                                        WPFDoEvents.InvokeAsyncInUIThread(() =>
                                        {
                                            try
                                            {
                                                MainWindowServiceDispatcher.Instance.OpenDocument(pdf_document);
                                            }
                                            catch (Exception ex)
                                            {
                                                Logging.Error(ex, "RestoreDesktop: Error occurred while attempting to restore the view (tab) for document fingerprint {0}, library Id {1}", document_fingerprint, library_id);
                                            }
                                        });
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logging.Warn(ex, "There was a problem restoring desktop with state {0}", restore_setting);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Error(ex, "There was a problem restoring the saved desktop state.");
            }

            Logging.Warn("Finished restoring desktop.");
        }

        private static string Filename => Path.GetFullPath(Path.Combine(ConfigurationManager.Instance.BaseDirectoryForUser, @"Qiqqa.restore_desktop"));
    }
}
