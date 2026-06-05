using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace SampleDirect3D;

public interface IApplication
{ 
	string WindowClassName { get; }
	uint WindowWidth { get; }
	uint WindowHeight { get; }
}

public unsafe class Application(string windowClassName, uint windowWidth, uint windowHeight, IGraphicsDevice graphicsDevice) : IApplication, IDisposable
{
	readonly IGraphicsDevice graphicsDevice = graphicsDevice;
	HWND? hwnd;
	bool running;

	public string WindowClassName { get; } = windowClassName;
	public uint WindowWidth { get; } = windowWidth;
	public uint WindowHeight { get; } = windowHeight;

	public void Initialize()
	{
		fixed (char* lpWindowClassName = WindowClassName)
		{
			var wc = new WNDCLASSEXW
			{
				cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
				lpfnWndProc = new WNDPROC(WndProc),
				hInstance = PInvoke.GetModuleHandle((PCWSTR)null),
				lpszClassName = lpWindowClassName,
				hCursor = PInvoke.LoadCursor((HINSTANCE)default, PInvoke.IDC_ARROW)
			};

			PInvoke.RegisterClassEx(wc);

			RECT desktopRect = GetDesktopRect();
			int posX = desktopRect.Width / 2 - (int)WindowWidth / 2;
			int posY = desktopRect.Height / 2 - (int)WindowHeight / 2;

			hwnd = PInvoke.CreateWindowEx(
			0,
			(PCWSTR)lpWindowClassName,
			(PCWSTR)lpWindowClassName,
			WINDOW_STYLE.WS_OVERLAPPEDWINDOW,
			posX,
			posY,
			(int)WindowWidth,
			(int)WindowHeight,
			HWND.Null,
			HMENU.Null,
			wc.hInstance,
			null);
		}

		graphicsDevice.Initialize(this, hwnd.Value);
	}

	public void Run()
	{
		if (hwnd == null) throw new InvalidOperationException("Window has not been initialized.");

		PInvoke.ShowWindow(hwnd.Value, SHOW_WINDOW_CMD.SW_SHOW);
		PInvoke.UpdateWindow(hwnd.Value);

		// メインループ
		var timestamp = Stopwatch.GetTimestamp();
		running = true;
		do 
		{
			// メッセージループ
			MSG msg;
			while (PInvoke.PeekMessage(&msg, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
			{
				if (msg.message == PInvoke.WM_QUIT)
				{
					running = false;
					break;
				}

				PInvoke.TranslateMessage(&msg);
				PInvoke.DispatchMessage(&msg);
			}

			var current = Stopwatch.GetTimestamp();
			float deltaTime = (current - timestamp)	/ (float)Stopwatch.Frequency;
			
			graphicsDevice.Render(this, deltaTime);

			timestamp = current;
		} while (running);
	}

	internal bool TryGetHwnd(out HWND hWnd)
	{
		if (hwnd.HasValue)
		{
			hWnd = hwnd.Value;
			return true;
		}
		else
		{
			hWnd = default;
			return false;
		}
	}

	public void Dispose()
	{
		graphicsDevice.Dispose();
	}

	static RECT GetDesktopRect()
	{
		RECT rect;
		var desktopHwnd = PInvoke.GetDesktopWindow();
		PInvoke.GetWindowRect(desktopHwnd, out rect);
		return rect;
	}

	static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
	{
		switch (msg)
		{
			case PInvoke.WM_DESTROY:
				PInvoke.PostQuitMessage(0);
				return (LRESULT)0;
		}

		return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

	}
}