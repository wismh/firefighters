using Cysharp.Threading.Tasks;
using System;

namespace MyToolkit.UI
{
    public interface IScreenManager
    {
        UniTask<Result> OpenScreen(Type type);
        UniTask<Result> OpenScreen<T>() where T : BaseScreen;
        UniTask CloseScreen<T>() where T : BaseScreen;
        UniTask CloseAllScreens();
    }
}