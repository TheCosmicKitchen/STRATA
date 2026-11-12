using Godot;

namespace Touchdown;

/// <summary>The title screen. Any start button loads the game scene.</summary>
public partial class Title : Control
{
	public override void _Ready()
	{
		if (DevTools.Wanted()) CallDeferred(MethodName.Start);
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e.IsActionPressed("jump") || e.IsActionPressed("ui_accept")) Start();
		else if (e.IsActionPressed("back")) GetTree().Quit();
	}

	void Start() => GetTree().ChangeSceneToFile("res://main/Main.tscn");
}
