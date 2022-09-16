﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;
using icons;
using Qiqqa.Common.Configuration;
using Qiqqa.Documents.PDF.PDFRendering;
#if !HAS_MUPDF_PAGE_RENDERER
using Utilities.PDF.Sorax;
#endif
using Utilities;
using Utilities.Files;
using Utilities.GUI;
using Utilities.Misc;
using Utilities.OCR;
using Utilities.PDF.MuPDF;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;


namespace Qiqqa.Documents.PDF
{
    public partial class PDFDocument
    {
        public const int TEXT_PAGES_PER_GROUP = 20;

        private Dictionary<int, TypedWeakReference<WordList>> texts = new Dictionary<int, TypedWeakReference<WordList>>();
        private object texts_lock = new object();

        public delegate void OnPageTextAvailableDelegate(int page_from, int page_to);
        public event OnPageTextAvailableDelegate OnPageTextAvailable;

        internal byte[] GetPageByHeightAsImage(int page, int height, int width)
        {
            // fake it while we test other parts of the UI and can dearly do without the shenanigans of the PDF page rendering system:
            //
            bool allow = ConfigurationManager.IsEnabled("RenderPDFPagesForReading") ||
                ConfigurationManager.IsEnabled("RenderPDFPagesForSidePanels") ||
                ConfigurationManager.IsEnabled("RenderPDFPagesForOCR");

            if (!allow)
            {
                return Backgrounds.GetBackgroundAsByteArray(Backgrounds.PageRenderingDisabled);
            }

#if !HAS_MUPDF_PAGE_RENDERER
            return SoraxPDFRenderer.GetPageByHeightAsImage(DocumentPath, PDFPassword, page, height);
#else
            return MuPDFRenderer.GetPageByHeightAsImage(DocumentPath, PDFPassword, page, height, width);
#endif
        }

        public void CauseAllPDFPagesToBeOCRed()
        {
            // jobqueue this one too - saves us one PDF access + parse action inline when invoked in the UI thread by OpenDocument()
            SafeThreadPool.QueueUserWorkItem(() =>
            {
                int pgcount = PageCount;

                for (int i = pgcount; i >= 1; --i)
                {
                    _ = GetOCRText(i);
                }
            });
        }

        /// <summary>
        /// Returns the OCR words on the page.  Null if the words are not yet available.
        /// The page will be queued for OCRing if they are not available...
        /// Page is 1 based...
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public WordList GetOCRText(int page, bool queue_for_ocr = true)
        {
            WPFDoEvents.AssertThisCodeIs_NOT_RunningInTheUIThread();

            if (page > PageCount || page < 1)
            {
                // dump stacktrace with this one so we know who instigated this out-of-bounds request.
                //
                // Boundary issue was discovered during customer log file analysis (log files courtesy of Chris Hicks)
                try
                {
                    throw new ArgumentException($"INTERNAL ERROR: requesting page text for page {page} which lies outside the detected document page range 1..{PageCount} for PDF document {Fingerprint}");
                }
                catch (Exception ex)
                {
                    Logging.Error(ex);
                }
            }

            //Utilities.LockPerfTimer l1_clk = Utilities.LockPerfChecker.Start();
            lock (texts_lock)
            {
                //l1_clk.LockPerfTimerStop();

                // First check our cache
                {
                    TypedWeakReference<WordList> word_list_weak;
                    texts.TryGetValue(page, out word_list_weak);
                    if (null != word_list_weak)
                    {
                        WordList word_list = word_list_weak.TypedTarget;
                        if (null != word_list)
                        {
                            return word_list;
                        }
                    }
                }

                // Then check for an existing SINGLE file
                {
                    string filename = MakeFilename_TextSingle(page);
                    try
                    {
                        if (File.Exists(filename))
                        {
                            // Get this ONE page
                            Dictionary<int, WordList> word_lists = WordList.ReadFromFile(filename, page);
                            WordList word_list = word_lists[page];
                            if (null == word_list)
                            {
                                throw new Exception(String.Format("No words on page {0} in OCR file {1}", page, filename));
                            }
                            texts[page] = new TypedWeakReference<WordList>(word_list);
                            return word_list;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Warn(ex, "There was an error loading the OCR text for {0} page {1}.", Fingerprint, page);
                        FileTools.Delete(filename);
                    }
                }

                // Then check for an existing GROUP file
                {
                    string filename = MakeFilename_TextGroup(page);
                    try
                    {
                        if (File.Exists(filename))
                        {
                            Dictionary<int, WordList> word_lists = WordList.ReadFromFile(filename);

                            // cache these word lists for later queries:
                            foreach (var pair in word_lists)
                            {
                                texts[pair.Key] = new TypedWeakReference<WordList>(pair.Value);
                            }

                            // now see if we've got a slot for the requested page:
                            WordList word_list;
                            word_lists.TryGetValue(page, out word_list);
                            if (null != word_list)
                            {
                                return word_list;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Warn(ex, "There was an error loading the OCR text group for {0} page {1}.", Fingerprint, page);
                        FileTools.Delete(filename);
                    }
                }
            }

            // If we get this far then the text was not available so queue extraction
            if (queue_for_ocr)
            {
                // If we have never tried the GROUP version before, queue for it
                string filename = MakeFilename_TextGroup(page);
                PDFTextExtractor.Job job = new PDFTextExtractor.Job(this, page);

                if (!File.Exists(filename) && PDFTextExtractor.Instance.JobGroupHasNotFailedBefore(job))
                {
                    PDFTextExtractor.Instance.QueueJobGroup(job);
                }
                else
                {
                    PDFTextExtractor.Instance.QueueJobSingle(job);
                }
            }

            return null;
        }

        internal string GetFullOCRText(int page)
        {
            StringBuilder sb = new StringBuilder();

            WordList word_list = GetOCRText(page);
            if (null != word_list)
            {
                foreach (Word word in word_list)
                {
                    sb.Append(word.Text);
                    sb.Append(' ');
                }
            }

            return sb.ToString();
        }

        // Gets the full concatenated text for this document.
        // This is slow as it concatenates all the words from the OCR result...
        internal string GetFullOCRText()
        {
            StringBuilder sb = new StringBuilder();

            for (int page = 1; page <= PageCount; ++page)
            {
                WordList word_list = GetOCRText(page);
                if (null != word_list)
                {
                    foreach (Word word in word_list)
                    {
                        sb.Append(word.Text);
                        sb.Append(' ');
                    }
                }
            }

            return sb.ToString();
        }

        public void ClearOCRText()
        {
            Logging.Info("Clearing OCR for document " + Fingerprint);

            // Delete the OCR files
            for (int page = 1; page <= PageCount; ++page)
            {
                // First the SINGLE file
                {
                    string filename = MakeFilename_TextSingle(page);

                    if (File.Exists(filename))
                    {
                        try
                        {
                            File.Delete(filename);
                        }
                        catch (Exception ex)
                        {
                            Logging.Error(ex, "There was a problem while trying to delete the OCR file " + filename);
                        }
                    }
                }

                // Then the MULTI file
                {
                    string filename = MakeFilename_TextGroup(page);

                    if (File.Exists(filename))
                    {
                        try
                        {
                            File.Delete(filename);
                        }
                        catch (Exception ex)
                        {
                            Logging.Error(ex, "There was a problem while trying to delete the OCR file " + filename);
                        }
                    }
                }
            }

            // Clear out the old texts
            //Utilities.LockPerfTimer l1_clk = Utilities.LockPerfChecker.Start();
            lock (texts_lock)
            {
                //l1_clk.LockPerfTimerStop();
                texts.Clear();
            }
        }

        public void ForceOCRText(string language = "eng")
        {
            Logging.Info("Forcing OCR for document {0} in language {1}", Fingerprint, language);

            // Clear out the old texts
            FlushCachedTexts();

            // To truly FORCE the OCR to run again, we have to nuke the old results stored on disk as well!
            ClearOCRText();

            // Queue all the pages for OCR
            for (int page = 1; page <= PageCount; ++page)
            {
                PDFTextExtractor.Job job = new PDFTextExtractor.Job(this, page);
                job.force_job = true;
                job.language = language;
                PDFTextExtractor.Instance.QueueJobSingle(job);
            }
        }

        internal void DumpImageCacheStats(out int pages_count, out int pages_bytes)
        {
            pages_count = 0;
            pages_bytes = 0;
        }

        public void FlushCachedPageRenderings()
        {
            Logging.Info("Flushing the cached page renderings for {0}", Fingerprint);

            // TODO: ditch cached PDF page images?
        }

        public void FlushCachedTexts()
        {
            Logging.Info("Flushing the cached texts for {0}", Fingerprint);

            //Utilities.LockPerfTimer l1_clk = Utilities.LockPerfChecker.Start();
            lock (texts_lock)
            {
                //l1_clk.LockPerfTimerStop();
                texts.Clear();
            }
        }
    }
}
