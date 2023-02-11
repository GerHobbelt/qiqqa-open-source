﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Qiqqa.Documents.PDF.PDFControls.Page.Text;
using Qiqqa.Documents.PDF.PDFControls.Page.Tools;
using Qiqqa.Documents.PDF.Search;
using Utilities;
using Utilities.GUI;
using Utilities.GUI.Animation;
using Utilities.Misc;
using Utilities.OCR;

namespace Qiqqa.Documents.PDF.PDFControls.Page.Search
{
    /// <summary>
    /// Interaction logic for PDFSearchLayer.xaml
    /// </summary>
    public partial class PDFSearchLayer : PageLayer, IDisposable
    {
        private WeakReference<PDFRendererControl> pdf_renderer_control;

        private int page;
        private PDFSearchResultSet search_result_set = null;

        public PDFSearchLayer(PDFRendererControl pdf_renderer_control, int page)
        {
            WPFDoEvents.AssertThisCodeIsRunningInTheUIThread();

            this.pdf_renderer_control = new WeakReference<PDFRendererControl>(pdf_renderer_control);
            this.page = page;

            InitializeComponent();

            Background = Brushes.Transparent;

            SizeChanged += PDFSearchLayer_SizeChanged;

            //Unloaded += PDFSearchLayer_Unloaded;
            Dispatcher.ShutdownStarted += Dispatcher_ShutdownStarted;
        }

        private PDFRendererControl GetPDFRendererControl()
        {
            if (pdf_renderer_control != null && pdf_renderer_control.TryGetTarget(out var control) && control != null)
            {
                return control;
            }
            return null;
        }

        private void Dispatcher_ShutdownStarted(object sender, EventArgs e)
        {
            Dispose();
        }

        private void PDFSearchLayer_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        private void PDFSearchLayer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            foreach (PDFTextItem pdf_text_item in Children.OfType<PDFTextItem>())
            {
                ResizeTextItem(pdf_text_item);
            }
        }

        private void ResizeTextItem(PDFTextItem pdf_text_item)
        {
            SetLeft(pdf_text_item, pdf_text_item.word.Left * ActualWidth);
            SetTop(pdf_text_item, pdf_text_item.word.Top * ActualHeight);
            pdf_text_item.Width = pdf_text_item.word.Width * ActualWidth;
            pdf_text_item.Height = pdf_text_item.word.Height * ActualHeight;
        }

        internal void SetSearchKeywords(PDFSearchResultSet search_result_set)
        {
            this.search_result_set = search_result_set;
            DoSearch();
        }

        internal override void PageTextAvailable()
        {
            DoSearch();
        }

        private void DoSearch()
        {
            PDFTextItemPool.Instance.RecyclePDFTextItemsFromChildren(Children);
            Children.Clear();

            if (null == search_result_set)
            {
                return;
            }

            foreach (PDFSearchResult search_result in search_result_set[page])
            {
                foreach (Word word in search_result.words)
                {
                    PDFTextItem pdf_text_item = PDFTextItemPool.Instance.GetPDFTextItem(word);
                    pdf_text_item.Tag = search_result;
                    pdf_text_item.SetAppearance(TextSearchBrushes.Instance.GetBrushPair(search_result.keyword_index));
                    ResizeTextItem(pdf_text_item);
                    Children.Add(pdf_text_item);
                }
            }
        }

        internal PDFSearchResult SetCurrentSearchPosition(PDFSearchResult previous_search_result_placeholder)
        {
            PDFRendererControl pdf_renderer_control = GetPDFRendererControl();

            if (pdf_renderer_control != null)
            {
                foreach (PDFTextItem pdf_text_item in Children.OfType<PDFTextItem>())
                {
                    ASSERT.Test(pdf_text_item != null);

                    PDFSearchResult search_result_placeholder = pdf_text_item.Tag as PDFSearchResult;

                    // If there was no previous search location, we use the first we find
                    // If the last text item was the match position, use this next one
                    if (previous_search_result_placeholder == search_result_placeholder)
                    {
                        pdf_renderer_control.SelectPage(page);
                        pdf_text_item.BringIntoView();
                        pdf_text_item.Opacity = 0;
                        Animations.Fade(pdf_text_item, 0.1, 1);

                        return search_result_placeholder;
                    }
                }
            }

            return null;
        }

        internal PDFSearchResult SetNextSearchPosition(PDFSearchResult previous_search_result_placeholder)
        {
            WPFDoEvents.AssertThisCodeIsRunningInTheUIThread();

            bool have_found_last_search_item = false;

            PDFRendererControl pdf_renderer_control = GetPDFRendererControl();

            if (pdf_renderer_control != null)
            {
                foreach (PDFTextItem pdf_text_item in Children.OfType<PDFTextItem>())
                {
                    ASSERT.Test(pdf_text_item != null);

                    PDFSearchResult search_result_placeholder = pdf_text_item?.Tag as PDFSearchResult;

                    // If there was no previous search location, we use the first we find
                    // If the last text item was the match position, use this next one
                    if (null == previous_search_result_placeholder || have_found_last_search_item && previous_search_result_placeholder != search_result_placeholder)
                    {
                        pdf_renderer_control.SelectPage(page);
                        pdf_text_item.BringIntoView();
                        pdf_text_item.Opacity = 0;
                        Animations.Fade(pdf_text_item, 0.1, 1);
                        return search_result_placeholder;
                    }

                    // If we have just found the last match, flag that the next one is the successor match
                    if (previous_search_result_placeholder == search_result_placeholder)
                    {
                        have_found_last_search_item = true;
                    }
                }
            }

            // We have not managed to find a search position if we get this far
            return null;
        }

        #region --- IDisposable ------------------------------------------------------------------------

        ~PDFSearchLayer()
        {
            Logging.Debug("~PDFSearchLayer()");
            Dispose(false);
        }

        public override void Dispose()
        {
            Logging.Debug("Disposing PDFSearchLayer");
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private int dispose_count = 0;
        protected virtual void Dispose(bool disposing)
        {
            Logging.Debug("PDFSearchLayer::Dispose({0}) @{1}", disposing, dispose_count);

            WPFDoEvents.InvokeInUIThread(() =>
            {
                WPFDoEvents.SafeExec(() =>
                {
                    if (dispose_count == 0)
                    {
                        foreach (var el in Children)
                        {
                            IDisposable node = el as IDisposable;
                            if (null != node)
                            {
                                node.Dispose();
                            }
                        }
                    }
                });

                WPFDoEvents.SafeExec(() =>
                {
                    Children.Clear();
                });

                WPFDoEvents.SafeExec(() =>
                {
                    DataContext = null;

                    Dispatcher.ShutdownStarted -= Dispatcher_ShutdownStarted;
                });

                WPFDoEvents.SafeExec(() =>
                {
                    // Clear the references for sanity's sake
                    pdf_renderer_control = null;
                    search_result_set = null;
                });

                ++dispose_count;

                //base.Dispose(disposing);     // parent only throws an exception (intentionally), so depart from best practices and don't call base.Dispose(bool)
            });
        }

        #endregion

    }
}
