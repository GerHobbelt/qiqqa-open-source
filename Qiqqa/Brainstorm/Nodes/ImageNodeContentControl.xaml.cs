﻿using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Qiqqa.Brainstorm.Nodes
{
    /// <summary>
    /// Interaction logic for ImageNodeContentControl.xaml
    /// </summary>
    public partial class ImageNodeContentControl : Grid
    {
        private ImageNodeContent image_node_content;

        public ImageNodeContentControl(NodeControl node_control, ImageNodeContent image_node_content)
        {
            this.image_node_content = image_node_content;

            InitializeComponent();

            Focusable = true;

            Image.Stretch = Stretch.Fill;
            Image.Source = image_node_content.BitmapSource;

            MouseDown += ImageNodeContentControl_MouseDown;
        }

        private void ImageNodeContentControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (2 == e.ClickCount)
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Filter = "Image files|*.jpeg;*.jpg;*.png;*.gif;*.bmp" + "|" + "All files|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (true == dialog.ShowDialog())
                {
                    image_node_content.ImageNodeContentFromPath(dialog.FileName);
                    Image.Source = image_node_content.BitmapSource;
                }

                e.Handled = true;
            }
        }
    }
}
