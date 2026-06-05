namespace SampleDirect3D;

public interface IGraphicsDevice : IDisposable
{
	void Initialize(IApplication app, nint hWnd);
	void Render(IApplication app, float deltaTime);
}