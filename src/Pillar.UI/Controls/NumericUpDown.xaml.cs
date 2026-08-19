// NumericUpDown.xaml.cs
// Implements a lightweight reusable WPF numeric editor for CAD tool options that need bounded decimal input.
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pillar.UI.Controls;

/// <summary>
/// Provides a bounded decimal input with repeatable increment and decrement buttons.
/// </summary>
public partial class NumericUpDown : UserControl
{
    private const int MaximumDecimalPlaces = 15;

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(NumericUpDown),
            new FrameworkPropertyMetadata(
                0.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValuePropertyChanged,
                CoerceValueProperty));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(double),
            typeof(NumericUpDown),
            new PropertyMetadata(0.0, OnRangePropertyChanged),
            IsFiniteDouble);

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(double),
            typeof(NumericUpDown),
            new PropertyMetadata(double.MaxValue, OnRangePropertyChanged),
            IsFiniteDouble);

    public static readonly DependencyProperty IncrementProperty =
        DependencyProperty.Register(
            nameof(Increment),
            typeof(double),
            typeof(NumericUpDown),
            new PropertyMetadata(1.0),
            IsPositiveFiniteDouble);

    public static readonly DependencyProperty DecimalPlacesProperty =
        DependencyProperty.Register(
            nameof(DecimalPlaces),
            typeof(int),
            typeof(NumericUpDown),
            new PropertyMetadata(2, OnDecimalPlacesPropertyChanged),
            IsSupportedDecimalPlaces);

    private bool _isApplyingUserValue;
    private bool _isSynchronizingRange;
    private double _editStartValue;

    /// <summary>
    /// Creates a numeric up-down editor and synchronizes its initial text.
    /// </summary>
    public NumericUpDown()
    {
        InitializeComponent();
        UpdateTextFromValue(false);
    }

    /// <summary>
    /// Raised when the numeric value changes after a committed edit or stepping input.
    /// </summary>
    public event EventHandler? ValueChanged;

    /// <summary>
    /// Gets or sets the current numeric value.
    /// </summary>
    public double Value
    {
        get { return (double)GetValue(ValueProperty); }
        set { SetValue(ValueProperty, value); }
    }

    /// <summary>
    /// Gets or sets the lowest accepted numeric value.
    /// </summary>
    public double Minimum
    {
        get { return (double)GetValue(MinimumProperty); }
        set { SetValue(MinimumProperty, value); }
    }

    /// <summary>
    /// Gets or sets the highest accepted numeric value.
    /// </summary>
    public double Maximum
    {
        get { return (double)GetValue(MaximumProperty); }
        set { SetValue(MaximumProperty, value); }
    }

    /// <summary>
    /// Gets or sets the amount added or removed by the spinner buttons.
    /// </summary>
    public double Increment
    {
        get { return (double)GetValue(IncrementProperty); }
        set { SetValue(IncrementProperty, value); }
    }

    /// <summary>
    /// Gets or sets how many decimal places are shown in the editor.
    /// </summary>
    public int DecimalPlaces
    {
        get { return (int)GetValue(DecimalPlacesProperty); }
        set { SetValue(DecimalPlacesProperty, value); }
    }

    /// <summary>
    /// Accepts finite numeric bounds.
    /// </summary>
    private static bool IsFiniteDouble(object value)
    {
        double numericValue = (double)value;
        return !double.IsNaN(numericValue) && !double.IsInfinity(numericValue);
    }

    /// <summary>
    /// Accepts finite positive spinner increments.
    /// </summary>
    private static bool IsPositiveFiniteDouble(object value)
    {
        double numericValue = (double)value;
        return numericValue > 0.0 && !double.IsNaN(numericValue) && !double.IsInfinity(numericValue);
    }

    /// <summary>
    /// Keeps displayed precision within the meaningful range of a double.
    /// </summary>
    private static bool IsSupportedDecimalPlaces(object value)
    {
        int decimalPlaces = (int)value;
        return decimalPlaces >= 0 && decimalPlaces <= MaximumDecimalPlaces;
    }

    /// <summary>
    /// Coerces the current value when callers update value or range properties.
    /// </summary>
    private static object CoerceValueProperty(DependencyObject dependencyObject, object baseValue)
    {
        NumericUpDown numericUpDown = (NumericUpDown)dependencyObject;
        double value = (double)baseValue;

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            value = numericUpDown.Minimum;
        }

        return numericUpDown.ClampValue(value);
    }

    /// <summary>
    /// Synchronizes text and notifies listeners when the coerced value changes.
    /// </summary>
    private static void OnValuePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        NumericUpDown numericUpDown = (NumericUpDown)dependencyObject;

        if (numericUpDown.ValueTextBox != null && !numericUpDown._isApplyingUserValue)
        {
            numericUpDown.UpdateTextFromValue(false);

            if (numericUpDown.ValueTextBox.IsKeyboardFocusWithin)
            {
                numericUpDown._editStartValue = numericUpDown.Value;
            }
        }

        numericUpDown.ValueChanged?.Invoke(numericUpDown, EventArgs.Empty);
    }

    /// <summary>
    /// Revalidates the current value after range bounds change.
    /// </summary>
    private static void OnRangePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        NumericUpDown numericUpDown = (NumericUpDown)dependencyObject;

        if (numericUpDown._isSynchronizingRange)
        {
            return;
        }

        numericUpDown._isSynchronizingRange = true;

        try
        {
            if (e.Property == MinimumProperty && numericUpDown.Minimum > numericUpDown.Maximum)
            {
                numericUpDown.SetCurrentValue(MaximumProperty, numericUpDown.Minimum);
            }
            else if (e.Property == MaximumProperty && numericUpDown.Maximum < numericUpDown.Minimum)
            {
                numericUpDown.SetCurrentValue(MinimumProperty, numericUpDown.Maximum);
            }

            numericUpDown.CoerceValue(ValueProperty);
        }
        finally
        {
            numericUpDown._isSynchronizingRange = false;
        }

        numericUpDown.UpdateTextFromValue(false);
    }

    /// <summary>
    /// Reformats the current text when the displayed precision changes.
    /// </summary>
    private static void OnDecimalPlacesPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        NumericUpDown numericUpDown = (NumericUpDown)dependencyObject;
        numericUpDown.UpdateTextFromValue(false);
    }

    /// <summary>
    /// Captures the committed value so Escape can cancel the current text edit.
    /// </summary>
    private void ValueTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        BeginTextEdit();
    }

    /// <summary>
    /// Establishes the value restored when the user cancels this edit.
    /// </summary>
    private void BeginTextEdit()
    {
        _editStartValue = Value;
    }

    /// <summary>
    /// Restores a formatted valid value when the user leaves the editor.
    /// </summary>
    private void ValueTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _ = sender;

        if (e.NewFocus is DependencyObject newFocus && IsAncestorOf(newFocus))
        {
            return;
        }

        CommitTextEdit();
    }

    /// <summary>
    /// Commits Enter, cancels Escape, and supports standard arrow-key stepping.
    /// </summary>
    private void ValueTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        _ = sender;

        if (e.Key == Key.Enter)
        {
            CommitTextEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelTextEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            StepValue(1);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            StepValue(-1);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Steps with the mouse wheel only while the text editor owns keyboard focus.
    /// </summary>
    private void NumericUpDown_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _ = sender;

        if (!ValueTextBox.IsKeyboardFocusWithin || e.Delta == 0)
        {
            return;
        }

        StepValue(e.Delta > 0 ? 1 : -1);
        e.Handled = true;
    }

    /// <summary>
    /// Increases the value by one configured increment.
    /// </summary>
    private void IncreaseButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        StepValue(1);
    }

    /// <summary>
    /// Decreases the value by one configured increment.
    /// </summary>
    private void DecreaseButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        StepValue(-1);
    }

    /// <summary>
    /// Applies the current text once and establishes a new cancel baseline.
    /// </summary>
    private void CommitTextEdit()
    {
        double committedValue = ResolveTextValue();
        ApplyUserValue(committedValue);
        _editStartValue = Value;
        UpdateTextFromValue(true);
    }

    /// <summary>
    /// Restores the value that was present when the current edit began.
    /// </summary>
    private void CancelTextEdit()
    {
        ApplyUserValue(ClampValue(_editStartValue));
        _editStartValue = Value;
        UpdateTextFromValue(true);
    }

    /// <summary>
    /// Resolves the editing buffer to a valid value without publishing an intermediate change.
    /// </summary>
    private double ResolveTextValue()
    {
        string text = ValueTextBox.Text.Trim();
        double parsedValue;

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsedValue)
            && !double.IsNaN(parsedValue)
            && !double.IsInfinity(parsedValue))
        {
            return NormalizeUserValue(parsedValue);
        }

        return Value;
    }

    /// <summary>
    /// Moves the current or typed value by one increment and publishes only the final result.
    /// </summary>
    private void StepValue(int direction)
    {
        double baseValue = ResolveTextValue();
        double steppedValue = AddIncrement(baseValue, direction);
        ApplyUserValue(NormalizeUserValue(steppedValue));
        _editStartValue = Value;
        UpdateTextFromValue(false);
    }

    /// <summary>
    /// Uses decimal arithmetic for ordinary UI values to avoid repeated binary increment drift.
    /// </summary>
    private double AddIncrement(double value, int direction)
    {
        try
        {
            decimal decimalValue = (decimal)value;
            decimal decimalIncrement = (decimal)Increment;
            return (double)(decimalValue + (decimalIncrement * direction));
        }
        catch (OverflowException)
        {
            double result = value + (Increment * direction);

            if (double.IsPositiveInfinity(result))
            {
                return Maximum;
            }

            if (double.IsNegativeInfinity(result))
            {
                return Minimum;
            }

            return result;
        }
    }

    /// <summary>
    /// Applies one user-originated value while allowing the caller to control text formatting.
    /// </summary>
    private void ApplyUserValue(double value)
    {
        _isApplyingUserValue = true;

        try
        {
            SetCurrentValue(ValueProperty, value);
        }
        finally
        {
            _isApplyingUserValue = false;
        }
    }

    /// <summary>
    /// Clamps and rounds committed user input to the precision shown by the control.
    /// </summary>
    private double NormalizeUserValue(double value)
    {
        double clampedValue = ClampValue(value);
        double roundedValue = Math.Round(clampedValue, DecimalPlaces, MidpointRounding.AwayFromZero);
        return ClampValue(roundedValue);
    }

    /// <summary>
    /// Formats the current value into the text box without re-entering text parsing.
    /// </summary>
    private void UpdateTextFromValue(bool moveCaretToEnd)
    {
        if (ValueTextBox == null)
        {
            return;
        }

        int selectionStart = ValueTextBox.SelectionStart;
        int selectionLength = ValueTextBox.SelectionLength;
        string format = "F" + DecimalPlaces.ToString(CultureInfo.InvariantCulture);
        ValueTextBox.Text = Value.ToString(format, CultureInfo.CurrentCulture);

        if (moveCaretToEnd)
        {
            ValueTextBox.CaretIndex = ValueTextBox.Text.Length;
            ValueTextBox.SelectionLength = 0;
        }
        else if (ValueTextBox.IsKeyboardFocusWithin)
        {
            int restoredSelectionStart = Math.Min(selectionStart, ValueTextBox.Text.Length);
            int availableSelectionLength = ValueTextBox.Text.Length - restoredSelectionStart;
            ValueTextBox.Select(restoredSelectionStart, Math.Min(selectionLength, availableSelectionLength));
        }
    }

    /// <summary>
    /// Keeps a value inside the configured numeric range.
    /// </summary>
    private double ClampValue(double value)
    {
        double minimum = Minimum;
        double maximum = Maximum;

        if (maximum < minimum)
        {
            maximum = minimum;
        }

        return Math.Min(maximum, Math.Max(minimum, value));
    }
}
