using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace Swiddler.Behaviors
{
    public static class TextBox
    {
        public static bool GetSelectAllText(System.Windows.Controls.TextBox textBox) => (bool)textBox.GetValue(SelectAllTextProperty);
        public static void SetSelectAllText(System.Windows.Controls.TextBox textBox, bool value) => textBox.SetValue(SelectAllTextProperty, value);

        public static readonly DependencyProperty SelectAllTextProperty =
            DependencyProperty.RegisterAttached("SelectAllText", typeof(bool), typeof(TextBox),
                new UIPropertyMetadata(false, OnSelectAllTextChanged));

        private static void OnSelectAllTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // https://stackoverflow.com/questions/660554/how-to-automatically-select-all-text-on-focus-in-wpf-textbox

            if (d is TextBoxBase textBox)
            {
                if (e.NewValue is bool == true)
                {
                    textBox.GotFocus += SelectAll;
                    textBox.PreviewMouseDown += IgnoreMouseButton;
                }
                else
                {
                    textBox.GotFocus -= SelectAll;
                    textBox.PreviewMouseDown -= IgnoreMouseButton;
                }
            }
        }

        private static void SelectAll(object sender, RoutedEventArgs e)
        {
            (e.OriginalSource as TextBoxBase)?.SelectAll();
        }

        private static void IgnoreMouseButton(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBoxBase textBox)
            {
                if (!textBox.IsReadOnly && textBox.IsKeyboardFocusWithin) return;

                e.Handled = true;
                textBox.Focus();
            }
        }



        public static string GetNumericRange(System.Windows.Controls.TextBox textBox) => (string)textBox.GetValue(NumericRangeProperty);
        public static void SetNumericRange(System.Windows.Controls.TextBox textBox, string value) => textBox.SetValue(NumericRangeProperty, value);

        public static readonly DependencyProperty NumericRangeProperty =
            DependencyProperty.RegisterAttached("NumericRange", typeof(string), typeof(TextBox),
                new UIPropertyMetadata(OnNumericRangeChanged));

        private static void OnNumericRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBoxBase textBox)
            {
                if (e.NewValue is string value)
                {
                    var vals = value.Split(' ');

                    if (vals.Length == 2)
                    {
                        if (int.TryParse(vals[0], out var n1) && 
                            int.TryParse(vals[1], out var n2))
                        {
                            new NumericRangeHandler(textBox) { MinValue = n1, MaxValue = n2 };
                        }
                    }
                }
            }
        }

        class NumericRangeHandler
        {
            public int MinValue { get; set; }
            public int MaxValue { get; set; }

            System.Windows.Controls.TextBox textBox;
            string oldText;
            int oldCaret, oldSelectionStart, oldSelectionLen;

            public NumericRangeHandler(TextBoxBase textBox)
            {
                this.textBox = (System.Windows.Controls.TextBox)textBox;

                textBox.PreviewTextInput += OnPreviewTextInput;
                textBox.TextChanged += OnTextChanged;
                DataObject.AddPastingHandler(textBox, PastingHandler);

                oldText = this.textBox.Text;
            }

            private void OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            {
                if (!string.IsNullOrEmpty(textBox.Text)) // allow empty
                {
                    if (int.TryParse(textBox.Text, out var num) == false || num < MinValue || num > MaxValue)
                    {
                        //textBox.Text = Math.Max(Math.Min(num, MaxValue), MinValue).ToString(CultureInfo.InvariantCulture);
                        textBox.Text = oldText;
                        textBox.CaretIndex = oldCaret;
                        textBox.SelectionStart = oldSelectionStart;
                        textBox.SelectionLength= oldSelectionLen;
                    }
                }

                oldText = textBox.Text;
            }

            void PastingHandler(object sender, DataObjectPastingEventArgs e)
            {
                StoreOldState();
            }

            void OnPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
            {
                StoreOldState();
                if (int.TryParse(e.Text, out var _) == false)
                    e.Handled = true;
            }

            void StoreOldState()
            {
                oldCaret = textBox.CaretIndex;
                oldSelectionStart = textBox.SelectionStart;
                oldSelectionLen = textBox.SelectionLength;
            }

        }



    }
}
