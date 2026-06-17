using Godot;
using System;

public partial class InputManager : Control
{
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        bool Obstructed = false;
        foreach (var item in GetTree().GetNodesInGroup("Obstructors"))
        {
            if (item is Window && (item as Window).Visible == true)
                Obstructed = true;
        }
        if (Obstructed == false)
        {
            foreach (var item in GetChildren())
            {
                if (!item.GetGroups().Contains("Obstructors"))
                {
                    item.ProcessMode = ProcessModeEnum.Inherit;
                }
            }
        }
        else
        {
            foreach (var item in GetChildren())
            {
                if (!item.GetGroups().Contains("Obstructors"))
                {
                    item.ProcessMode = ProcessModeEnum.Disabled;
                }
            }
        }
    }
}
