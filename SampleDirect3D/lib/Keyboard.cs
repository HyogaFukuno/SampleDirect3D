using Windows.Win32;

namespace SampleDirect3D.lib;

public partial class Keyboard
{
	public static readonly Keyboard current = new();

	public KeyState this[KeyCode code] => states.FirstOrDefault(s => s.Code == code);

	public static void UpdateKeyStates()
	{
		for (int i = 0; i < states.Length; i++) 
		{
			states[i].previous = states[i].current;
			states[i].current = (PInvoke.GetKeyState((int)states[i].Code) & 0x8000) != 0;
		}
	}
}