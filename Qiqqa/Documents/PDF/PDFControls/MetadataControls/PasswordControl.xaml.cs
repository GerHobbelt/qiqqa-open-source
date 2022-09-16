﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Utilities.GUI;
using Utilities.Reflection;

namespace Qiqqa.Documents.PDF.PDFControls.MetadataControls
{
    /// <summary>
    /// Interaction logic for PasswordControl.xaml
    /// </summary>
    public partial class PasswordControl : UserControl
    {
        public PasswordControl()
        {
            InitializeComponent();

            ButtonClear.Caption = "Forget";
            ButtonClear.ToolTip = "Forget the password associated with this PDF.";
            ButtonClear.Click += ButtonClear_Click;

            ButtonSet.Caption = "Set";
            ButtonSet.ToolTip = "Set the password associated with this document to the value in the box above.";
            ButtonSet.Click += ButtonSet_Click;

            TxtPassword.KeyDown += TxtPassword_KeyDown;

            DataContextChanged += PasswordControl_DataContextChanged;
        }

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SetPassword();
                e.Handled = true;
            }
        }

        private void SetPassword()
        {
            string password = TxtPassword.Password;
            if (String.IsNullOrEmpty(password))
            {
                ForgetPassword();
            }
            else
            {
                AugmentedBindable<PDFDocument> pdf_document_bindable = DataContext as AugmentedBindable<PDFDocument>;
                PDFDocument pdf_document = pdf_document_bindable.Underlying;
                pdf_document.AddPassword(password);
                ReBind();
            }
        }

        private void ButtonSet_Click(object sender, RoutedEventArgs e)
        {
            SetPassword();
        }

        private void ButtonClear_Click(object sender, RoutedEventArgs e)
        {
            ForgetPassword();
        }

        private void ForgetPassword()
        {
            if (MessageBoxes.AskQuestion("Are you sure you want to forget this password?"))
            {
                AugmentedBindable<PDFDocument> pdf_document_bindable = DataContext as AugmentedBindable<PDFDocument>;
                PDFDocument pdf_document = pdf_document_bindable.Underlying;
                pdf_document.RemovePassword();
                ReBind();
            }
        }

        private void PasswordControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ReBind();
        }

        private void ReBind()
        {
            TxtPassword.Clear();
            TxtDescription.Text = "You have no password associated with this PDF.  If it is a password protected PDF, enter the password in the box below.";

            AugmentedBindable<PDFDocument> pdf_document_bindable = DataContext as AugmentedBindable<PDFDocument>;
            if (null != pdf_document_bindable)
            {
                PDFDocument pdf_document = pdf_document_bindable.Underlying;

                string password = pdf_document.GetPassword();
                if (!String.IsNullOrEmpty(password))
                {
                    TxtDescription.Text = "You have a password associated with this PDF.  Enter a new one in the box below or clear it with the Forget button.";
                }
            }
        }
    }
}
