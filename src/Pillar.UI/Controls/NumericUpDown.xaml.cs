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
            new PropertyMetadata(0.0, OnRangePropertyChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(double),
            typeof(NumericUpDown),
            new PropertyMetadata(double.PositiveInfinity, OnRangePropertyChanged));

    public static readonly DependencyProperty IncrementProperty =
        DependencyProperty.Register(
            nameof(Increment),
            typeof(double),
            typeof(NumericUpDown),
            new PropertyMetadata(1.0));

    public static readonly DependencyProperty DecimalPlacesProperty =
        DependencyProperty.Register(
            nameof(DecimalPlaces),
            typeof(int),
            typeof(NumericUpDown),
            new PropertyMetadata(2, OnDecimalPlacesPropertyChanged));

    private bool _isSynchronizingText;
    private bool _isCommittingTextInput;

    /// <summary>
    /// Creates a numeric up-down editor and synchronizes its initial text.
    /// </summary>
    public NumericUpDown()
    {
        InitializeComponent();
        UpdateTextFromValue();
    }

    /// <summary>
    /// Raised when the numeric value changes after typing or spinner input.
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

        if (numericUpDown.ValueTextBox != null
            && (!numericUpDown._isCommittingTextInput || !numericUpDown.ValueTextBox.IsKeyboardFocusWithin))
        {
            numericUpDown.UpdateTextFromValue();
        }

        numericUpDown.ValueChanged?.Invoke(numericUpDown, EventArgs.Empty);
    }

    /// <summary>
    /// Revalidates the current value after range bounds change.
    /// </summary>
    private static void OnRangePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        NumericUpDown numericUpDown = (NumericUpDown)dependencyObject;
        numericUpDown.CoerceValue(ValueProperty);
        numericUpDown.UpdateTextFromValue();
    }

    /// <summary>
    /// Reformats the current text when the displayed precision changes.
    /// </summary>
    private static void OnDecimalPlacesPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        NumericUpDown numericUpDown = (NumericUpDown)dependencyObject;
        numericUpDown.UpdateTextFromValue();
    }

    /// <summary>
    /// Applies typed numeric input when the text parses as a valid number.
    /// </summary>
    private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isSynchronizingText)
        {
            return;
        }

        CommitTextToValue(false);
    }

    /// <summary>
    /// Restores a formatted valid value when the user leaves the editor.
    /// </summary>
    private void ValueTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        CommitTextToValue(true);
        UpdateTextFromValue();
    }

    /// <summary>
    /// Commits Enter and cancels invalid partial text with Escape.
    /// </summary>
    private void ValueTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        _ = sender;

        if (e.Key == Key.Enter)
        {
            CommitTextToValue(true);
            UpdateTextFromValue();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            UpdateTextFromValue();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Increases the value by one configured increment.
    /// </summary>
    private void IncreaseButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        StepValue(Increment);
    }

    /// <summary>
    /// Decreases the value by one configured increment.
    /// </summary>
    private void DecreaseButton_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        StepValue(-Increment);
    }

    /// <summary>
    /// Applies text to the value, optionally falling back to the nearest valid value.
    /// </summary>
    private void CommitTextToValue(bool coerceInvalidText)
    {
        string text = ValueTextBox.Text.Trim();
        double parsedValue;

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue)
            && IsFiniteValueInRange(parsedValue))
        {
            _isCommittingTextInput = true;

            try
            {
                Value = parsedValue;
            }
            finally
            {
                _isCommittingTextInput = false;
            }

            return;
        }

        if (coerceInvalidText)
        {
            Value = ClampValue(Value);
        }
    }

    /// <summary>
    /// Moves the current value by the requested delta and keeps the result in range.
    /// </summary>
    private void StepValue(double delta)
    {
        CommitTextToValue(true);
        double increment = Math.Abs(Increment);

        if (increment <= 0.0 || double.IsNaN(increment) || double.IsInfinity(increment))
        {
            increment = 1.0;
        }

        double direction = delta < 0.0 ? -1.0 : 1.0;
        Value = ClampValue(Value + (increment * direction));
        UpdateTextFromValue();
    }

    /// <summary>
    /// Formats the current value into the text box without re-entering text parsing.
    /// </summary>
    private void UpdateTextFromValue()
    {
        if (ValueTextBox == null)
        {
            return;
        }

        _isSynchronizingText = true;

        try
        {
            int decimalPlaces = Math.Max(0, DecimalPlaces);
            string format = "F" + decimalPlaces.ToString(CultureInfo.InvariantCulture);
            ValueTextBox.Text = Value.ToString(format, CultureInfo.InvariantCulture);
        }
        finally
        {
            _isSynchronizingText = false;
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

    /// <summary>
    /// Gets whether a typed value can safely become the committed numeric value.
    /// </summary>
    private bool IsFiniteValueInRange(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return false;
        }

        return value >= Minimum && value <= Maximum;
    }
}
