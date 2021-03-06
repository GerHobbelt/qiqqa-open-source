﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Qiqqa.Brainstorm.Common;
using Qiqqa.Brainstorm.Nodes;
using Utilities.GUI;

namespace Qiqqa.Brainstorm.SceneManager
{
    /// <summary>
    /// Interaction logic for SelectedNodeControl.xaml
    /// </summary>
    public partial class SelectedNodeControl : UserControl
    {
        private SceneRenderingControl scene_rendering_control;
        private NodeControl node_control = null;
        private NodeControl.DimensionsChangedDelegate SelectedNode_OnDimensionsChangedDelegate;
        private NodeControl.DeletedDelegate SelectedNode_OnDeletedDelegate;

        #region --- Visual colours ------------------------------------------------------------------------------------

        private static Color control_color_base = Colors.Gold;
        private static Color background_color = ColorTools.MakeTransparentColor(control_color_base, 64);
        private static Color border_color = ColorTools.MakeTransparentColor(control_color_base, 200);
        private static Brush background_brush = new SolidColorBrush(background_color);
        private static Brush border_brush = new SolidColorBrush(border_color);
        private static Thickness border_thickness = new Thickness(1);

        #endregion


        public SelectedNodeControl(SceneRenderingControl scene_rendering_control)
        {
            this.scene_rendering_control = scene_rendering_control;

            InitializeComponent();

            ObjGridOverlay.Background = background_brush;
            ObjBorder.BorderBrush = border_brush;
            ObjBorder.BorderThickness = border_thickness;

            SelectedNode_OnDimensionsChangedDelegate = SelectedNode_OnDimensionsChanged;
            SelectedNode_OnDeletedDelegate = SelectedNode_OnDeleted;

            FormatSizer(SizerTL, Cursors.SizeNWSE);
            FormatSizer(SizerTR, Cursors.SizeNESW);
            FormatSizer(SizerBL, Cursors.SizeNESW);
            FormatSizer(SizerBR, Cursors.SizeNWSE);

            Focusable = true;
            Visibility = Visibility.Collapsed;
        }

        private void FormatSizer(Shape sizer, Cursor cursor)
        {
            sizer.Stroke = border_brush;
            sizer.Fill = border_brush;
            sizer.StrokeThickness = 1;
            sizer.Width = 15;
            sizer.Height = 15;

            sizer.IsHitTestVisible = true;
            sizer.Cursor = cursor;

            sizer.MouseDown += Sizer_MouseDown;
            sizer.MouseUp += Sizer_MouseUp;
            sizer.MouseMove += Sizer_MouseMove;
        }

        private void Sizer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Shape sizer = (Shape)sender;
            UpdateMouseTracking(e);
            sizer.CaptureMouse();
            e.Handled = true;
        }

        private void Sizer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Shape sizer = (Shape)sender;
            UpdateMouseTracking(e);
            sizer.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void Sizer_MouseMove(object sender, MouseEventArgs e)
        {
            UpdateMouseTracking(e);

            if (MouseButtonState.Pressed == e.LeftButton)
            {
                if (null != node_control)
                {
                    if (SizerTL == sender)
                    {
                        node_control.scene_data.SetDeltaWidth(-2 * mouse_left_delta_virtual);
                        node_control.scene_data.SetDeltaHeight(-2 * mouse_top_delta_virtual);
                    }
                    else if (SizerTR == sender)
                    {
                        node_control.scene_data.SetDeltaWidth(+2 * mouse_left_delta_virtual);
                        node_control.scene_data.SetDeltaHeight(-2 * mouse_top_delta_virtual);
                    }
                    else if (SizerBL == sender)
                    {
                        node_control.scene_data.SetDeltaWidth(-2 * mouse_left_delta_virtual);
                        node_control.scene_data.SetDeltaHeight(+2 * mouse_top_delta_virtual);
                    }
                    else if (SizerBR == sender)
                    {
                        node_control.scene_data.SetDeltaWidth(+2 * mouse_left_delta_virtual);
                        node_control.scene_data.SetDeltaHeight(+2 * mouse_top_delta_virtual);
                    }

                    scene_rendering_control.RecalculateNodeControlDimensions(node_control);


                    e.Handled = true;
                }
            }
        }


        public NodeControl Selected
        {
            get => node_control;

            set
            {
                // Do nothing if they are reselecting the same node control
                if (value == node_control)
                {
                    return;
                }

                // Unselect the existing node
                if (null != node_control)
                {
                    node_control.OnDimensionsChanged -= SelectedNode_OnDimensionsChangedDelegate;
                    node_control.OnDeleted -= SelectedNode_OnDeletedDelegate;

                    ISelectable selectable = node_control.NodeContentControl as ISelectable;
                    if (null != selectable) selectable.Deselect();

                    node_control = null;
                }

                // Select the new node
                if (null != value)
                {
                    node_control = value;
                    node_control.OnDimensionsChanged += SelectedNode_OnDimensionsChangedDelegate;
                    node_control.OnDeleted += SelectedNode_OnDeletedDelegate;

                    ISelectable selectable = node_control.NodeContentControl as ISelectable;
                    if (null != selectable) selectable.Select();

                    scene_rendering_control.Focus();
                    node_control.NodeContentControl.Focus();
                    Keyboard.Focus(node_control.NodeContentControl);
                }

                // Are we visible or not?
                if (null == node_control)
                {
                    Visibility = Visibility.Collapsed;
                }
                else
                {
                    node_control.RecalculateChildDimension();
                    SelectedNode_OnDimensionsChanged(node_control);
                    Visibility = Visibility.Visible;

                    node_control.Children[0].Focus();
                }
            }
        }

        private void SelectedNode_OnDimensionsChanged(NodeControl nc)
        {
            double SPACER = 4;

            Width = nc.Width + SPACER + SPACER;
            Height = nc.Height + SPACER + SPACER;

            Canvas.SetLeft(this, Canvas.GetLeft(nc) - SPACER);
            Canvas.SetTop(this, Canvas.GetTop(nc) - SPACER);
            Canvas.SetZIndex(this, Canvas.GetZIndex(nc) + 1);
        }

        private void SelectedNode_OnDeleted(NodeControl nc)
        {
            scene_rendering_control.RemoveSelectedNodeControl(Selected);
        }

        #region --- Mouse tracking ------------------------------------------------------------------------------------

        private double mouse_left_previous = 0;
        private double mouse_top_previous = 0;
        private double mouse_left_current = 0;
        private double mouse_top_current = 0;
        private double mouse_left_delta_virtual = 0;
        private double mouse_top_delta_virtual = 0;

        private void UpdateMouseTracking(MouseEventArgs e)
        {
            UpdateMouseTracking(e.GetPosition(scene_rendering_control));
        }

        private void UpdateMouseTracking(DragEventArgs e)
        {
            UpdateMouseTracking(e.GetPosition(scene_rendering_control));
        }

        private void UpdateMouseTracking(Point p)
        {
            mouse_left_previous = mouse_left_current;
            mouse_top_previous = mouse_top_current;
            mouse_left_current = p.X;
            mouse_top_current = p.Y;

            mouse_left_delta_virtual = (mouse_left_current - mouse_left_previous) / scene_rendering_control.CurrentPowerScale;
            mouse_top_delta_virtual = (mouse_top_current - mouse_top_previous) / scene_rendering_control.CurrentPowerScale;
        }

        #endregion
    }
}
