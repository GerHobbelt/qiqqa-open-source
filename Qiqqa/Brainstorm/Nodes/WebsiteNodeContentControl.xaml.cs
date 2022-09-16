﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using icons;
using Qiqqa.Common.Configuration;
using Utilities;
using Utilities.GUI;
using Utilities.Reflection;

namespace Qiqqa.Brainstorm.Nodes
{
    /// <summary>
    /// Interaction logic for WebsiteNodeContentControl.xaml
    /// </summary>
    public partial class WebsiteNodeContentControl : UserControl, IDisposable
    {
        private AugmentedBindable<WebsiteNodeContent> website_node_content;
        private BackgroundFader fader;

        public WebsiteNodeContentControl(NodeControl node_control, WebsiteNodeContent website_node_content)
        {
            this.website_node_content = new AugmentedBindable<WebsiteNodeContent>(website_node_content);
            DataContext = this.website_node_content;

            InitializeComponent();

            Focusable = true;

            ImageDocumentIcon.Source = Icons.GetAppIcon(Icons.BrainstormWebsite);

            TextUrl.FontWeight = FontWeights.Bold;
            TextTitle.TextWrapping = TextWrapping.Wrap;
            TextLastVisited.TextTrimming = TextTrimming.CharacterEllipsis;
            TextVisitedCount.TextTrimming = TextTrimming.CharacterEllipsis;

            MouseDoubleClick += WebsiteNodeContentControl_MouseDoubleClick;

            fader = new BackgroundFader(this);
        }

        private void WebsiteNodeContentControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ++website_node_content.Underlying.VisitedCount;
            website_node_content.Underlying.LastVisited = DateTime.UtcNow;
            website_node_content.NotifyPropertyChanged();

            try
            {
#if UNUSED_CODE
                // Utilities code; see https://github.com/jimmejardine/qiqqa-open-source/issues/26
                Process.Start(website_node_content.Underlying.Url);
#else
                // Qiqqa code; see https://github.com/jimmejardine/qiqqa-open-source/issues/26
                WebsiteAccess.OpenWebsite(website_node_content.Underlying.Url);
#endif
            }
            catch (Exception ex)
            {
                MessageBoxes.Error(ex, "There was a problem launching your web page");
            }
        }

        #region --- IDisposable ------------------------------------------------------------------------

        ~WebsiteNodeContentControl()
        {
            Logging.Debug("~WebsiteNodeContentControl()");
            Dispose(false);
        }

        public void Dispose()
        {
            Logging.Debug("Disposing WebsiteNodeContentControl");
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private int dispose_count = 0;
        protected virtual void Dispose(bool disposing)
        {
            Logging.Debug("WebsiteNodeContentControl::Dispose({0}) @{1}", disposing, dispose_count);

            WPFDoEvents.SafeExec(() =>
            {
                if (dispose_count == 0)
                {
                    // Get rid of managed resources / get rid of cyclic references:
                    fader?.Dispose();
                }
                fader = null;
            });

            WPFDoEvents.SafeExec(() =>
            {
                website_node_content = null;
                DataContext = null;
            });

            ++dispose_count;
        }

        #endregion

    }
}
