namespace SampleDirect3D.lib;

public partial class Keyboard
{
	public struct KeyState(KeyCode code)
	{
		internal bool previous;
		internal bool current;
		internal KeyCode Code { get; } = code;
		public bool IsPressed => current;
		public bool WasPressedThisFrame => current && !previous;
		public bool WasReleasedThisFrame => !current && previous;
	}

	static readonly KeyState[] states =
	[
		new KeyState(KeyCode.Left),
		new KeyState(KeyCode.Up),
		new KeyState(KeyCode.Right),
		new KeyState(KeyCode.Down),

		new KeyState(KeyCode.A),
		new KeyState(KeyCode.B),
		new KeyState(KeyCode.C),
		new KeyState(KeyCode.D),
		new KeyState(KeyCode.E),
		new KeyState(KeyCode.F),
		new KeyState(KeyCode.G),
		new KeyState(KeyCode.H),
		new KeyState(KeyCode.I),
		new KeyState(KeyCode.J),
		new KeyState(KeyCode.K),
		new KeyState(KeyCode.L),
		new KeyState(KeyCode.M),
		new KeyState(KeyCode.N),
		new KeyState(KeyCode.O),
		new KeyState(KeyCode.P),
		new KeyState(KeyCode.Q),
		new KeyState(KeyCode.R),
		new KeyState(KeyCode.S),
		new KeyState(KeyCode.T),
		new KeyState(KeyCode.U),
		new KeyState(KeyCode.V),
		new KeyState(KeyCode.W),
		new KeyState(KeyCode.X),
		new KeyState(KeyCode.Y),
		new KeyState(KeyCode.Z),
	];
}