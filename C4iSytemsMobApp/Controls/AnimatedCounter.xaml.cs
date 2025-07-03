namespace C4iSytemsMobApp.Controls;


public partial class AnimatedCounter : ContentView
{
    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(int), typeof(AnimatedCounter), 0, propertyChanged: OnValueChanged);

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public AnimatedCounter()
    {
        InitializeComponent();
        UpdateText();
    }

    private static async void OnValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (AnimatedCounter)bindable;
        await control.DigitLabel.FadeTo(0, 150);
        control.UpdateText();
        await control.DigitLabel.FadeTo(1, 150);
    }

    private void UpdateText()
    {
        DigitLabel.Text = Value.ToString("D5");
    }
}
