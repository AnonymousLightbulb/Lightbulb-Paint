using Godot;
using System;

public partial class NumericSlider : Godot.Range
{
    [Export] bool IsInt = true;
    [Export] public Label Display;

    public override void _Process(double delta)
    {
        if (IsInt == true)
        {
            Display.Text = Mathf.RoundToInt(Value).ToString();
        }
        else
        {
            Display.Text = Value.ToString();
        }
        base._Process(delta);
    }
}
