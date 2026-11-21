using Cysharp.Threading.Tasks;

namespace MyToolkit.UI
{
    /// <summary>
    /// Optional hook on a <see cref="BaseScreen"/> when the user selects the tab that is already active.
    /// </summary>
    public interface ITabReselectHandler
    {
        UniTask OnTabReselectedAsync();
    }
}
