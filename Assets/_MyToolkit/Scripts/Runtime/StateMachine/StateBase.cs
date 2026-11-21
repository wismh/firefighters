using Cysharp.Threading.Tasks;

namespace MyToolkit.StateMachine 
{
    public abstract class StateBase
    {
        public abstract UniTask Enter();

        public abstract UniTask Exit();
    }
}