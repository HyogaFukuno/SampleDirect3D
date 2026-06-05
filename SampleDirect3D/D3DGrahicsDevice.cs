using Vortice.Win32;
using Vortice.Win32.Graphics.Direct3D;
using Vortice.Win32.Graphics.Direct3D11;
using Vortice.Win32.Graphics.Dxgi;
using static Vortice.Win32.Apis;
using static Vortice.Win32.Graphics.Direct2D.Apis;
using static Vortice.Win32.Graphics.Direct3D11.Apis;
using static Vortice.Win32.Graphics.Dxgi.Apis;

using FeatureLevel = Vortice.Win32.Graphics.Direct3D.FeatureLevel;
using Format = Vortice.Win32.Graphics.Dxgi.Common.Format;
using InfoQueueFilter = Vortice.Win32.Graphics.Direct3D11.InfoQueueFilter;
using Usage = Vortice.Win32.Graphics.Dxgi.Usage;
using MessageId = Vortice.Win32.Graphics.Direct3D11.MessageId;
using AlphaMode = Vortice.Win32.Graphics.Direct2D.Common.AlphaMode;
using Vortice.Win32.Numerics;
using Vortice.Win32.Graphics.Direct2D;
using System.Numerics;
using Windows.Win32;
using SampleDirect3D.lib;

namespace SampleDirect3D;

public unsafe class D3DGrahicsDevice : IGraphicsDevice
{
	ComPtr<IDXGIFactory2> factory;
	ComPtr<IDXGIAdapter1> adapter;
	ComPtr<ID3D11Device1> d3dDevice;
	ComPtr<ID3D11DeviceContext1> immediateContext;
	ComPtr<IDXGISwapChain1> swapChain;
	ComPtr<ID3D11Texture2D1> backBuffer;
	ComPtr<ID3D11RenderTargetView1> renderTargetView;
	ComPtr<ID2D1Device> d2dDevice;
	ComPtr<ID2D1DeviceContext> d2dDeviceContext;
	
	ComPtr<ID2D1Bitmap1> d2dTargetBitmap;
	ComPtr<ID2D1SolidColorBrush> d2dSolidColorBrush;

	public void Initialize(IApplication app, nint hWnd)
	{ 
		ThrowIfFailed(CreateD3DDevice());
		ThrowIfFailed(CreateSwapChain(app, hWnd));
		ThrowIfFailed(CreateBackBuffer());
		ThrowIfFailed(CreateD2DDevice());
		ThrowIfFailed(CreateRenderTargetView());
	}

	HResult CreateD3DDevice()
	{
		// DXGIファクトリを作成して、GPUアダプタを列挙する
		CreateFactoryFlags factoryFlags = CreateFactoryFlags.None;

#if DEBUG
		using ComPtr<IDXGIInfoQueue> dxgiInfoQueue = default;
		if (DXGIGetDebugInterface1(0, __uuidof<IDXGIInfoQueue>(), (void**)dxgiInfoQueue.GetAddressOf()).Success)
		{
			factoryFlags = CreateFactoryFlags.Debug;
			dxgiInfoQueue.Get()->SetBreakOnSeverity(DXGI_DEBUG_ALL, InfoQueueMessageSeverity.Error, true);
			dxgiInfoQueue.Get()->SetBreakOnSeverity(DXGI_DEBUG_ALL, InfoQueueMessageSeverity.Corruption, true);
		}
#endif

		ThrowIfFailed(CreateDXGIFactory2(factoryFlags, __uuidof<IDXGIFactory2>(), (void**)factory.GetAddressOf()));

		// GPUアダプタを列挙して、ハードウェアアダプタを選択する
		// Windows 10 以降では、GPUの優先度を指定してアダプタを列挙できるため、まずはそちらを試す
		using ComPtr<IDXGIFactory6> factory6 = default;
		if (factory.CopyTo(&factory6).Success)
		{
			for (uint adapterIndex = 0;
				factory6.Get()->EnumAdapterByGpuPreference(
					adapterIndex,
					GpuPreference.HighPerformance,
					__uuidof<IDXGIAdapter1>(),
					(void**)adapter.ReleaseAndGetAddressOf()).Success;
					adapterIndex++)
			{
				AdapterDescription1 desc = default;
				ThrowIfFailed(adapter.Get()->GetDesc1(&desc));

				if ((desc.Flags & AdapterFlags.Software) != AdapterFlags.None) { continue; }
				break;
			}
		}

		// Windows 10 より前の環境や、GPUの優先度を指定してアダプタを列挙できない環境では、従来の方法でアダプタを列挙する
		if (adapter.Get() == null)
		{
			for (uint adapterIndex = 0;
				factory.Get()->EnumAdapters1(adapterIndex, adapter.ReleaseAndGetAddressOf()).Success;
				adapterIndex++)
			{
				AdapterDescription1 desc = default;
				ThrowIfFailed(adapter.Get()->GetDesc1(&desc));

				if ((desc.Flags & AdapterFlags.Software) != AdapterFlags.None) { continue; }
				break;
			}
		}

		// アダプタが見つからなかった場合は、失敗とする
		if (adapter.Get() == null) return HResult.Fail;

		// D3D11デバイスとイミディエイトコンテキストを作成する
		using ComPtr<ID3D11Device> tempDevice = default;
		using ComPtr<ID3D11DeviceContext> tempImmediateContext = default;
		FeatureLevel featureLevel;

		ReadOnlySpan<FeatureLevel> featureLevels = [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0];
		CreateDeviceFlags creationFlags = CreateDeviceFlags.BgraSupport;

		ThrowIfFailed(D3D11CreateDevice(
			(IDXGIAdapter*)adapter.Get(),
			DriverType.Unknown,
			creationFlags,
			featureLevels,
			tempDevice.GetAddressOf(),
			&featureLevel,
			tempImmediateContext.GetAddressOf()));

#if DEBUG
		// デバッグレイヤーが有効な場合は、D3D11InfoQueueを使用して、エラーと破損のメッセージでブレークするように設定する
		using ComPtr<ID3D11Debug> d3dDebug = default;
		if (tempDevice.CopyTo(&d3dDebug).Success)
		{
			using ComPtr<ID3D11InfoQueue> d3dInfoQueue = default;
			if (d3dDebug.CopyTo(&d3dInfoQueue).Success)
			{
				d3dInfoQueue.Get()->SetBreakOnSeverity(MessageSeverity.Corruption, true);
				d3dInfoQueue.Get()->SetBreakOnSeverity(MessageSeverity.Error, true);

				var hide = stackalloc MessageId[1] { MessageId.SetPrivateDataChangingParams };

				InfoQueueFilter filter = new();
				filter.DenyList.NumIDs = 1u;
				filter.DenyList.pIDList = hide;
				d3dInfoQueue.Get()->AddStorageFilterEntries(&filter);
			}
		}
#endif

		// 作成したD3D11デバイスとイミディエイトコンテキストを、クラスのフィールドに保存する
		if (tempDevice.CopyTo(ref d3dDevice).Failure) return HResult.Fail;
		if (tempImmediateContext.CopyTo(ref immediateContext).Failure) return HResult.Fail;

		using ComPtr<IDXGIDevice1> dxgiDevice1 = default;
		if (d3dDevice.CopyTo(dxgiDevice1.GetAddressOf()).Success)
		{
			dxgiDevice1.Get()->SetMaximumFrameLatency(1);
		}

		return HResult.Ok;
	}

	HResult CreateSwapChain(IApplication app, nint hWnd)
	{
		var swapChainDesc = new SwapChainDescription1(app.WindowWidth, app.WindowHeight)
		{
			Format = Format.R8G8B8A8Unorm,
			BufferCount = 2,
			BufferUsage = Usage.RenderTargetOutput
		};
		swapChainDesc.SampleDesc.Count = 1;
		swapChainDesc.SwapEffect = SwapEffect.FlipDiscard;

		using ComPtr<IDXGISwapChain1> tempSwapChain = default;
		ThrowIfFailed(factory.Get()->CreateSwapChainForHwnd(
			(IUnknown*)d3dDevice.Get(),
			hWnd,
			&swapChainDesc,
			null,
			null,
			tempSwapChain.GetAddressOf()));

		return tempSwapChain.CopyTo(ref swapChain);
	}

	HResult CreateBackBuffer() => swapChain.Get()->GetBuffer(
		0,
		__uuidof<ID3D11Texture2D1>(),
		(void**)backBuffer.GetAddressOf());

	HResult CreateD2DDevice()
	{
		var properties = new CreationProperties
		{
			threadingMode = ThreadingMode.SingleThreaded,
			debugLevel = DebugLevel.Information,
			options = DeviceContextOptions.None
		};

		using ComPtr<IDXGIDevice> dxgiDevice = default;
		using ComPtr<ID2D1Device> tempD2DDevice = default;

		ThrowIfFailed(d3dDevice.CopyTo(dxgiDevice.GetAddressOf()));
		ThrowIfFailed(D2D1CreateDevice(
			dxgiDevice.Get(),
			&properties,
			tempD2DDevice.GetAddressOf()));

		if (tempD2DDevice.CopyTo(ref d2dDevice).Failure) return HResult.Fail;

	
		using ComPtr<ID2D1DeviceContext> tempD2DDeviceContext = default;
		ThrowIfFailed(d2dDevice.Get()->CreateDeviceContext(
			DeviceContextOptions.None,
			tempD2DDeviceContext.GetAddressOf()));

		if (tempD2DDeviceContext.CopyTo(ref d2dDeviceContext).Failure) return HResult.Fail;

		var bitmapProperties = new BitmapProperties1
		{
			pixelFormat = new()
			{
				format = Format.R8G8B8A8Unorm,
				alphaMode = AlphaMode.Ignore
			},
			dpiX = 96,
			dpiY = 96,
			bitmapOptions = BitmapOptions.Target | BitmapOptions.CannotDraw
		};
		
		using ComPtr<IDXGISurface1> dxgiBackBuffer = default;
		using ComPtr<ID2D1Bitmap1> tempD2DTargetBitmap = default;
		if (backBuffer.CopyTo(dxgiBackBuffer.GetAddressOf()).Failure) return HResult.Fail;

		ThrowIfFailed(d2dDeviceContext.Get()->CreateBitmapFromDxgiSurface(
			(IDXGISurface*)dxgiBackBuffer.Get(),
			&bitmapProperties,
			tempD2DTargetBitmap.GetAddressOf()));

		if (tempD2DTargetBitmap.CopyTo(ref d2dTargetBitmap).Failure) return HResult.Fail;

		d2dDeviceContext.Get()->SetTarget((ID2D1Image*)d2dTargetBitmap.Get());

		using ComPtr<ID2D1SolidColorBrush> tempD2DSolidColorBrush = default;	
		ThrowIfFailed(d2dDeviceContext.Get()->CreateSolidColorBrush(
			new Color4(1.0f, 1.0f, 1.0f), tempD2DSolidColorBrush.GetAddressOf()));

		if (tempD2DSolidColorBrush.CopyTo(ref d2dSolidColorBrush).Failure) return HResult.Fail;

		return HResult.Ok;
	}

	HResult CreateRenderTargetView()
	{
		using ComPtr<ID3D11RenderTargetView> rtv = default;
		ThrowIfFailed(d3dDevice.Get()->CreateRenderTargetView(
			(ID3D11Resource*)backBuffer.Get(),
			null,
			rtv.GetAddressOf()));

		return rtv.CopyTo(ref renderTargetView);
	}

	public void Render(IApplication app, float deltaTime)
	{
		var viewport = new Viewport(0, 0, app.WindowWidth, app.WindowHeight);
		immediateContext.Get()->RSSetViewports(1U, &viewport);

		var rtv = (ID3D11RenderTargetView*)renderTargetView.Get();
		immediateContext.Get()->OMSetRenderTargets(1U, &rtv, null);

		var clearColor = stackalloc float[4] { 0.0f, 0.0f, 0.0f, 0.0f };
		immediateContext.Get()->ClearRenderTargetView(rtv, clearColor);


		d2dDeviceContext.Get()->BeginDraw();

		var color = new Color4(0.0f, 0.25f, 0.25f);
		d2dDeviceContext.Get()->Clear(&color);
		var rect = new RectF(100, 100, 200, 300);

		if (Keyboard.current[KeyCode.W].IsPressed)
		{
			d2dDeviceContext.Get()->DrawRectangle(&rect, (ID2D1Brush*)d2dSolidColorBrush.Get(), 1.0f, null);
		}

		ThrowIfFailed(d2dDeviceContext.Get()->EndDraw());

		swapChain.Get()->Present(1, 0);
	}

	public void Dispose()
	{
		factory.Dispose();
		adapter.Dispose();
		d3dDevice.Dispose();
		immediateContext.Dispose();
		swapChain.Dispose();
		backBuffer.Dispose();
		renderTargetView.Dispose();
		d2dDevice.Dispose();
		d2dDeviceContext.Dispose();	
		d2dTargetBitmap.Dispose();
		d2dSolidColorBrush.Dispose();
	}
}