// NumericUpDownSmokeTests.cs
// Exercises committed numeric editing, validation, culture, cancellation, and precise spinner stepping.
using Pillar.UI.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pillar.UI.SmokeTests;

/// <summary>
/// Provides focused behavioral checks for the shared WPF numeric editor.
/// </summary>
internal static class NumericUpDownSmokeTests
{
    /// <summary>
    /// Runs the numeric editor checks and appends failures to the shared harness.
    /// </summary>
    public static void Run(List<string> failures)
    {
        EnsureApplication();
        RunTest(failures, "Numeric input commits once", ValidateInputCommitsOnce);
        RunTest(failures, "Numeric input clamps parsed value", ValidateParsedValueIsClamped);
        RunTest(failures, "Numeric spinner uses typed value", ValidateSpinnerUsesTypedValue);
        RunTest(failures, "Numeric input uses current culture", ValidateCurrentCultureIsUsed);
        RunTest(failures, "Numeric Escape restores edit value", ValidateEscapeRestoresEditValue);
        RunTest(failures, "Numeric properties reject invalid configuration", ValidatePropertiesRejectInvalidConfiguration);
        RunTest(failures, "Numeric stepping avoids accumulated residue", ValidateFractionalStepping);
        RunTest(failures, "Numeric value binding survives user input", ValidateValueBindingIsPreserved);
        RunTest(failures, "Numeric keyboard and focused wheel step", ValidateKeyboardAndFocusedWheelStepping);
        RunTest(failures, "Numeric spinner uses themed accessible buttons", ValidateSpinnerPresentation);
    }

    /// <summary>
    /// Confirms typing remains local until the edit is explicitly committed.
    /// </summary>
    private static void ValidateInputCommitsOnce()
    {
        NumericUpDown control = new NumericUpDown
        {
            Minimum = 0.0,
            Maximum = 20.0,
            Value = 2.0
        };
        TextBox textBox = GetValueTextBox(control);
        int changeCount = 0;
        control.ValueChanged += (_, _) => changeCount++;

        textBox.Text = "7.5";

        if (System.Math.Abs(control.Value - 2.0) > double.Epsilon || changeCount != 0)
        {
            throw new InvalidOperationException("Expected typing to leave the committed value unchanged.");
        }

        Invoke(control, "CommitTextEdit");

        if (System.Math.Abs(control.Value - 7.5) > double.Epsilon
            || changeCount != 1
            || textBox.CaretIndex != textBox.Text.Length)
        {
            throw new InvalidOperationException("Expected one committed value change with the caret at the end.");
        }
    }

    /// <summary>
    /// Confirms a valid out-of-range edit clamps the parsed number rather than the old value.
    /// </summary>
    private static void ValidateParsedValueIsClamped()
    {
        NumericUpDown control = new NumericUpDown
        {
            Minimum = 0.0,
            Maximum = 10.0,
            Value = 4.0
        };
        TextBox textBox = GetValueTextBox(control);
        textBox.Text = "15";

        Invoke(control, "CommitTextEdit");

        if (System.Math.Abs(control.Value - 10.0) > double.Epsilon)
        {
            throw new InvalidOperationException("Expected the parsed value to clamp to the maximum.");
        }

        textBox.Text = "not a number";
        Invoke(control, "CommitTextEdit");

        if (System.Math.Abs(control.Value - 10.0) > double.Epsilon)
        {
            throw new InvalidOperationException("Expected invalid text to preserve the last committed value.");
        }
    }

    /// <summary>
    /// Confirms a spinner click resolves the editing buffer before applying its increment.
    /// </summary>
    private static void ValidateSpinnerUsesTypedValue()
    {
        NumericUpDown control = new NumericUpDown
        {
            Minimum = 0.0,
            Maximum = 10.0,
            Increment = 1.0,
            DecimalPlaces = 0,
            Value = 2.0
        };
        TextBox textBox = GetValueTextBox(control);
        int changeCount = 0;
        control.ValueChanged += (_, _) => changeCount++;
        textBox.Text = "6";

        Invoke(control, "StepValue", 1);

        if (System.Math.Abs(control.Value - 7.0) > double.Epsilon || changeCount != 1)
        {
            throw new InvalidOperationException("Expected the spinner to publish typed value plus one increment once.");
        }
    }

    /// <summary>
    /// Confirms editing and formatting follow the user's current numeric culture.
    /// </summary>
    private static void ValidateCurrentCultureIsUsed()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            NumericUpDown control = new NumericUpDown
            {
                Minimum = 0.0,
                Maximum = 10.0,
                Value = 1.0
            };
            TextBox textBox = GetValueTextBox(control);
            textBox.Text = "1,5";

            Invoke(control, "CommitTextEdit");

            if (System.Math.Abs(control.Value - 1.5) > double.Epsilon || textBox.Text != "1,50")
            {
                throw new InvalidOperationException("Expected comma-decimal input and output for de-DE.");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    /// <summary>
    /// Confirms cancel restores the committed value present when editing began.
    /// </summary>
    private static void ValidateEscapeRestoresEditValue()
    {
        NumericUpDown control = new NumericUpDown
        {
            Minimum = 0.0,
            Maximum = 10.0,
            Value = 3.0
        };
        TextBox textBox = GetValueTextBox(control);
        Invoke(control, "BeginTextEdit");
        textBox.Text = "8";
        Invoke(control, "CancelTextEdit");

        if (System.Math.Abs(control.Value - 3.0) > double.Epsilon
            || textBox.Text != 3.0.ToString("F2", CultureInfo.CurrentCulture))
        {
            throw new InvalidOperationException("Expected cancel to restore and format the edit-start value.");
        }
    }

    /// <summary>
    /// Confirms invalid property values are rejected and crossing bounds remain ordered.
    /// </summary>
    private static void ValidatePropertiesRejectInvalidConfiguration()
    {
        NumericUpDown control = new NumericUpDown();
        ExpectArgumentException(() => control.Minimum = double.NaN);
        ExpectArgumentException(() => control.Maximum = double.PositiveInfinity);
        ExpectArgumentException(() => control.Increment = 0.0);
        ExpectArgumentException(() => control.Increment = -1.0);
        ExpectArgumentException(() => control.DecimalPlaces = 16);

        control.Minimum = 5.0;
        control.Maximum = 3.0;

        if (System.Math.Abs(control.Minimum - 3.0) > double.Epsilon
            || System.Math.Abs(control.Maximum - 3.0) > double.Epsilon)
        {
            throw new InvalidOperationException("Expected the most recently assigned bound to keep the range ordered.");
        }
    }

    /// <summary>
    /// Confirms repeated decimal increments normalize to the displayed precision.
    /// </summary>
    private static void ValidateFractionalStepping()
    {
        NumericUpDown control = new NumericUpDown
        {
            Minimum = -10.0,
            Maximum = 10.0,
            Increment = 0.1,
            DecimalPlaces = 2,
            Value = 0.0
        };

        Invoke(control, "StepValue", 1);
        Invoke(control, "StepValue", 1);
        Invoke(control, "StepValue", 1);

        if (control.Value != 0.3)
        {
            throw new InvalidOperationException($"Expected an exact displayed step value of 0.3, received {control.Value:R}.");
        }
    }

    /// <summary>
    /// Confirms internal edits retain and update an existing two-way value binding.
    /// </summary>
    private static void ValidateValueBindingIsPreserved()
    {
        NumericBindingSource source = new NumericBindingSource
        {
            BoundValue = 2.0
        };
        NumericUpDown control = new NumericUpDown
        {
            Minimum = 0.0,
            Maximum = 10.0
        };
        Binding binding = new Binding(nameof(NumericBindingSource.BoundValue))
        {
            Source = source,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };
        BindingOperations.SetBinding(control, NumericUpDown.ValueProperty, binding);
        TextBox textBox = GetValueTextBox(control);
        textBox.Text = "6";

        Invoke(control, "CommitTextEdit");

        if (BindingOperations.GetBindingExpression(control, NumericUpDown.ValueProperty) == null
            || System.Math.Abs(control.Value - 6.0) > double.Epsilon
            || System.Math.Abs(source.BoundValue - 6.0) > double.Epsilon)
        {
            throw new InvalidOperationException("Expected committed input to preserve and update the two-way binding.");
        }
    }

    /// <summary>
    /// Confirms arrow keys step and the mouse wheel steps only while the editor is focused.
    /// </summary>
    private static void ValidateKeyboardAndFocusedWheelStepping()
    {
        NumericUpDown control = new NumericUpDown
        {
            Minimum = 0.0,
            Maximum = 10.0,
            Increment = 1.0,
            DecimalPlaces = 0,
            Value = 2.0
        };
        TextBox textBox = GetValueTextBox(control);
        Button focusTarget = new Button();
        Grid hostGrid = new Grid();
        hostGrid.RowDefinitions.Add(new RowDefinition());
        hostGrid.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(focusTarget, 1);
        hostGrid.Children.Add(control);
        hostGrid.Children.Add(focusTarget);
        Window hostWindow = new Window
        {
            Content = hostGrid,
            Width = 160.0,
            Height = 60.0,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None
        };

        try
        {
            hostWindow.Show();
            hostWindow.Activate();
            textBox.Focus();
            Keyboard.Focus(textBox);
            PresentationSource presentationSource = PresentationSource.FromVisual(textBox)
                ?? throw new InvalidOperationException("Expected a presentation source for keyboard input.");
            textBox.Text = "4";
            KeyEventArgs keyEvent = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                presentationSource,
                Environment.TickCount,
                Key.Up)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };
            textBox.RaiseEvent(keyEvent);

            if (!keyEvent.Handled || System.Math.Abs(control.Value - 5.0) > double.Epsilon)
            {
                throw new InvalidOperationException("Expected Up to step the typed value once.");
            }

            MouseWheelEventArgs focusedWheelEvent = new MouseWheelEventArgs(
                Mouse.PrimaryDevice,
                Environment.TickCount,
                -120)
            {
                RoutedEvent = Mouse.PreviewMouseWheelEvent
            };
            textBox.RaiseEvent(focusedWheelEvent);

            if (!focusedWheelEvent.Handled || System.Math.Abs(control.Value - 4.0) > double.Epsilon)
            {
                throw new InvalidOperationException("Expected the focused mouse wheel to step down once.");
            }

            focusTarget.Focus();
            Keyboard.Focus(focusTarget);
            MouseWheelEventArgs unfocusedWheelEvent = new MouseWheelEventArgs(
                Mouse.PrimaryDevice,
                Environment.TickCount,
                120)
            {
                RoutedEvent = Mouse.PreviewMouseWheelEvent
            };
            control.RaiseEvent(unfocusedWheelEvent);

            if (unfocusedWheelEvent.Handled || System.Math.Abs(control.Value - 4.0) > double.Epsilon)
            {
                throw new InvalidOperationException("Expected an unfocused mouse wheel event to remain available for panel scrolling.");
            }
        }
        finally
        {
            hostWindow.Close();
        }
    }

    /// <summary>
    /// Confirms spinner buttons use the local style, arrow geometry, and automation labels.
    /// </summary>
    private static void ValidateSpinnerPresentation()
    {
        NumericUpDown control = new NumericUpDown();
        RepeatButton increaseButton = control.FindName("IncreaseButton") as RepeatButton
            ?? throw new InvalidOperationException("Expected an increase repeat button.");
        RepeatButton decreaseButton = control.FindName("DecreaseButton") as RepeatButton
            ?? throw new InvalidOperationException("Expected a decrease repeat button.");

        if (increaseButton.Style == null
            || decreaseButton.Style == null
            || increaseButton.Content is not Path
            || decreaseButton.Content is not Path
            || AutomationProperties.GetName(increaseButton) != "Increase value"
            || AutomationProperties.GetName(decreaseButton) != "Decrease value")
        {
            throw new InvalidOperationException("Expected styled arrow buttons with accessible automation names.");
        }
    }

    /// <summary>
    /// Creates the WPF application resources required by the control when the harness runs alone.
    /// </summary>
    private static void EnsureApplication()
    {
        Application? currentApplication = Application.Current;

        if (currentApplication == null)
        {
            Pillar.UI.App application = new Pillar.UI.App();
            application.InitializeComponent();
            currentApplication = application;
        }

        currentApplication.ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    /// <summary>
    /// Retrieves the control's editing textbox from its XAML namescope.
    /// </summary>
    private static TextBox GetValueTextBox(NumericUpDown control)
    {
        return control.FindName("ValueTextBox") as TextBox
            ?? throw new InvalidOperationException("Expected NumericUpDown to expose its editing textbox.");
    }

    /// <summary>
    /// Invokes one private interaction method so the smoke harness can exercise committed behavior directly.
    /// </summary>
    private static void Invoke(NumericUpDown control, string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(NumericUpDown).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Expected NumericUpDown method '{methodName}'.");

        try
        {
            method.Invoke(control, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    /// <summary>
    /// Confirms a dependency-property validation callback rejects an invalid assignment.
    /// </summary>
    private static void ExpectArgumentException(Action assignment)
    {
        try
        {
            assignment();
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException("Expected invalid numeric property assignment to throw ArgumentException.");
    }

    /// <summary>
    /// Records one failed numeric editor validation while continuing the remaining checks.
    /// </summary>
    private static void RunTest(List<string> failures, string name, Action test)
    {
        try
        {
            test();
        }
        catch (Exception ex)
        {
            failures.Add($"{name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Supplies a dependency property for exercising a real two-way WPF binding.
    /// </summary>
    private sealed class NumericBindingSource : DependencyObject
    {
        public static readonly DependencyProperty BoundValueProperty =
            DependencyProperty.Register(
                nameof(BoundValue),
                typeof(double),
                typeof(NumericBindingSource),
                new PropertyMetadata(0.0));

        /// <summary>
        /// Gets or sets the value connected to the numeric editor.
        /// </summary>
        public double BoundValue
        {
            get { return (double)GetValue(BoundValueProperty); }
            set { SetValue(BoundValueProperty, value); }
        }
    }
}


